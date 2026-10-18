-- ============================================================
-- 确认订单页落地：会员地址表 + 订单明细扩展列
-- 适用库：jingsuding_order（root/123456，MySQL 8.0）
-- 风格：幂等、纯增量；已存在则跳过，可重复执行。
-- 执行方式：
--   "/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -uroot -p123456 jingsuding_order < order_confirm_align.sql
-- ============================================================

-- ------------------------------------------
-- 1) 会员收货地址表 mem_member_address
--    小程序确认订单页「新增/修改收货地址」落库用，会员维度隔离（member_id）。
--    字段对齐前端 Address 模型：province/city/district/detail + name/phone/is_default。
-- ------------------------------------------
CREATE TABLE IF NOT EXISTS `mem_member_address` (
  `id`          BIGINT       NOT NULL AUTO_INCREMENT,
  `member_id`   BIGINT       NOT NULL                  COMMENT '会员ID，关联 mem_member.id',
  `name`        VARCHAR(50)  NOT NULL DEFAULT ''       COMMENT '收货人姓名',
  `phone`       VARCHAR(20)  NOT NULL DEFAULT ''       COMMENT '收货人电话',
  `province`    VARCHAR(50)  NOT NULL DEFAULT ''       COMMENT '省',
  `city`        VARCHAR(50)  NOT NULL DEFAULT ''       COMMENT '市',
  `district`    VARCHAR(50)  NOT NULL DEFAULT ''       COMMENT '区/县',
  `detail`      VARCHAR(255) NOT NULL DEFAULT ''       COMMENT '详细地址',
  `is_default`  TINYINT(1)   NOT NULL DEFAULT 0        COMMENT '是否默认地址 0-否 1-是',
  `is_deleted`  TINYINT      NOT NULL DEFAULT 0        COMMENT '逻辑删除 0-正常 1-删除',
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_member_id` (`member_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='会员收货地址表';

-- ------------------------------------------
-- 2) trx_order_item 扩展列
--    spec_json：镜片规格 + 验光数据 JSON（确认页传入的 spec_json 落库用）
--    processing_status：加工状态（unprocessed-待加工 / processed-已加工）
-- ------------------------------------------
SET @db = DATABASE();

SET @c1 = (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA=@db AND TABLE_NAME='trx_order_item' AND COLUMN_NAME='spec_json');
SET @sql1 = IF(@c1=0,
  "ALTER TABLE `trx_order_item` ADD COLUMN `spec_json` VARCHAR(2000) NULL COMMENT '镜片规格+验光数据JSON（确认页传入）'",
  'SELECT "spec_json 已存在，跳过" AS skip_reason');
PREPARE stmt1 FROM @sql1; EXECUTE stmt1; DEALLOCATE PREPARE stmt1;

SET @c2 = (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA=@db AND TABLE_NAME='trx_order_item' AND COLUMN_NAME='processing_status');
SET @sql2 = IF(@c2=0,
  "ALTER TABLE `trx_order_item` ADD COLUMN `processing_status` VARCHAR(20) NOT NULL DEFAULT 'processed' COMMENT '加工状态:unprocessed-待加工/processed-已加工'",
  'SELECT "processing_status 已存在，跳过" AS skip_reason');
PREPARE stmt2 FROM @sql2; EXECUTE stmt2; DEALLOCATE PREPARE stmt2;

SELECT 'order_confirm_align 执行完成' AS result;
