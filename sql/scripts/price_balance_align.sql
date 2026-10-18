-- ============================================================
-- 价格策略与会员余额模块 —— 数据库对齐脚本（增量、幂等、不破坏数据）
-- 目标库：jingsuding_order
--
-- 【为什么要这个脚本】
--   本模块的 8 张表在项目主脚本 Jsd_order.sql（约 1467~1637 行）里【已经存在】，
--   但列名/类型与需求 Word 文档不同（例如 order_price_snapshot 用 trx_order_id 而非 order_id、
--   mem_recharge 用 pay_status/pay_type 而非 status/pay_channel）。
--   因此 price_balance_module.sql 里的 CREATE TABLE IF NOT EXISTS 会全部静默跳过，
--   而实体按文档写的列名就会报 "Unknown column ..." —— 即本次事故的 root cause。
--
--   修复策略确定为【代码迁就数据库】：实体 [Column] 对齐 Jsd_order.sql 的真实列名，
--   本脚本只补齐数据库里确实缺失/过窄的列（纯增量，不动已有数据）。
--
-- 幂等：用 information_schema 判列，可重复执行。
-- ============================================================

USE `jingsuding_order`;
SET @db = DATABASE();

-- ------------------------------------------------------------
-- 1) mem_member 补 balance（可用余额）
--    其余 3 列 customer_level_id / frozen_balance / total_recharge 已存在，这里做兜底判断
-- ------------------------------------------------------------
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='balance');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `balance` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT ''可用余额（先充值后下单）'' AFTER `account_period`',
  'SELECT ''balance 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='customer_level_id');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `customer_level_id` BIGINT UNSIGNED NOT NULL DEFAULT 0 COMMENT ''关联等级ID（mem_member_level.id）''',
  'SELECT ''customer_level_id 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='frozen_balance');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `frozen_balance` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT ''冻结余额（下单占用）''',
  'SELECT ''frozen_balance 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='total_recharge');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `total_recharge` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT ''累计充值总额（仅本金）''',
  'SELECT ''total_recharge 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 2) mkt_balance_log 补 operator_id（后台调整/代充的操作人留痕）
-- ------------------------------------------------------------
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mkt_balance_log' AND COLUMN_NAME='operator_id');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mkt_balance_log` ADD COLUMN `operator_id` BIGINT UNSIGNED NULL DEFAULT NULL COMMENT ''操作人ID（sys_user.id）'' AFTER `remark`',
  'SELECT ''operator_id 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 3) order_price_snapshot.discount_info 加宽（原 VARCHAR(255) 放不下 JSON 快照）
-- ------------------------------------------------------------
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='order_price_snapshot' AND COLUMN_NAME='discount_info'
              AND CHARACTER_MAXIMUM_LENGTH >= 1000);
SET @sql = IF(@cnt=0,
  'ALTER TABLE `order_price_snapshot` MODIFY COLUMN `discount_info` VARCHAR(1000) NULL DEFAULT NULL COMMENT ''折扣详情快照（JSON，含命中策略/计算方式/是否触发底价）''',
  'SELECT ''discount_info 长度已 >=1000，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ============================================================
-- 校验（可选）：确认 mem_member 4 列与 mkt_balance_log.operator_id 已就位
-- ============================================================
SELECT COLUMN_NAME, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND ((TABLE_NAME='mem_member' AND COLUMN_NAME IN ('balance','customer_level_id','frozen_balance','total_recharge'))
       OR (TABLE_NAME='mkt_balance_log' AND COLUMN_NAME='operator_id'))
ORDER BY TABLE_NAME, COLUMN_NAME;
