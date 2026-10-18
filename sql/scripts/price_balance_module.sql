-- ============================================================
-- 价格策略与会员余额模块 —— 数据库脚本
-- 目标库：jingsuding_order
--
-- 内容：
--   1) 8 张新建表：mem_member_level / price_strategy / price_rule / price_rule_item
--                 price_change_log / order_price_snapshot / mem_recharge / mkt_balance_log
--   2) mem_member 扩展 4 列：customer_level_id / balance / frozen_balance / total_recharge
--
-- 幂等：CREATE TABLE IF NOT EXISTS + information_schema 判列，可重复执行，不破坏现有数据。
-- 用法：全选执行即可（Navicat / DBeaver / mysql CLI 均可）。
--
-- ⚠️⚠️ 重要（2026-09-25 修订）
--   本脚本按【需求 Word 文档】的表结构编写，但项目主脚本 Jsd_order.sql（约 1467~1637 行）
--   里这 8 张表【已经存在且列名不同】，例如：
--       order_price_snapshot: trx_order_id / trx_order_item_id / prod_info_id / sku_id（不是 order_id/material_id）
--       price_rule_item     : prod_info_id + sku_id（不是 material_id）
--       price_strategy      : remark（不是 description）
--       mem_recharge        : pay_status + pay_type + recharge_channel（不是 status/pay_channel）
--       mkt_balance_log     : related_type 是 TINYINT（不是 varchar），且原表无 operator_id
--   所以在已有库上执行本脚本，CREATE TABLE IF NOT EXISTS 会【全部静默跳过】，
--   实体按文档列名访问就会报 "Unknown column ... in 'field list'"。
--
--   ✅ 最终方案：以【数据库 / Jsd_order.sql】为准，实体 [Column] 已对齐真实列名；
--      本脚本仅用于「全新空库」初始化，已有库请改用 price_balance_align.sql（只补缺失列）。
-- ============================================================

USE `jingsuding_order`;

-- ------------------------------------------------------------
-- 1) mem_member_level 客户（会员）等级表
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `mem_member_level` (
  `id`               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `level_name`       VARCHAR(50)     NOT NULL                COMMENT '等级名称',
  `level_code`       VARCHAR(20)     NOT NULL                COMMENT '等级编码（唯一）',
  `sort_order`       INT             NOT NULL DEFAULT 0      COMMENT '排序权重（数值越大越靠前）',
  `default_discount` DECIMAL(5,4)    NOT NULL DEFAULT 1.0000 COMMENT '默认折扣率（0.8500 表示 85 折）',
  `description`      VARCHAR(255)    DEFAULT NULL            COMMENT '描述',
  `status`           TINYINT         NOT NULL DEFAULT 1      COMMENT '状态：0-停用 1-启用',
  `create_time`      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_level_code` (`level_code`),
  KEY `idx_status`     (`status`),
  KEY `idx_sort_order` (`sort_order`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='客户（会员）等级表';

-- ------------------------------------------------------------
-- 2) price_strategy 价格策略主表
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `price_strategy` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `strategy_no`    VARCHAR(32)     NOT NULL                COMMENT '策略编号（唯一）',
  `strategy_name`  VARCHAR(100)    NOT NULL                COMMENT '策略名称',
  `strategy_type`  TINYINT         NOT NULL DEFAULT 1      COMMENT '1-客户等级价 2-客户专属价 3-批量阶梯价 4-促销价',
  `priority`       INT             NOT NULL DEFAULT 0      COMMENT '优先级（数值越大越优先）',
  `effective_date` DATETIME        NOT NULL                COMMENT '生效时间',
  `expire_date`    DATETIME        NOT NULL                COMMENT '失效时间',
  `status`         TINYINT         NOT NULL DEFAULT 0      COMMENT '状态：0-草稿 1-启用 2-停用',
  `description`    VARCHAR(255)    DEFAULT NULL            COMMENT '描述',
  `create_by`      BIGINT UNSIGNED NOT NULL DEFAULT 0      COMMENT '创建人ID（sys_user.id）',
  `create_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_strategy_no` (`strategy_no`),
  KEY `idx_strategy_type` (`strategy_type`),
  KEY `idx_status`        (`status`),
  KEY `idx_effective`     (`effective_date`),
  KEY `idx_expire`        (`expire_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格策略主表';

-- ------------------------------------------------------------
-- 3) price_rule 价格规则表
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `price_rule` (
  `id`                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `strategy_id`       BIGINT UNSIGNED NOT NULL                COMMENT '关联策略ID（price_strategy.id）',
  `mem_member_id`     BIGINT UNSIGNED DEFAULT NULL            COMMENT '会员ID（客户专属价时填）',
  `customer_level_id` BIGINT UNSIGNED DEFAULT NULL            COMMENT '等级ID（客户等级价时填，mem_member_level.id）',
  `apply_scope`       TINYINT         NOT NULL DEFAULT 1      COMMENT '1-全部商品 2-指定分类 3-指定商品',
  `category_id`       BIGINT UNSIGNED DEFAULT NULL            COMMENT '分类ID（apply_scope=2 时填，prod_category.id）',
  `calc_type`         TINYINT         NOT NULL DEFAULT 2      COMMENT '1-固定价 2-折扣率 3-减免金额',
  `calc_value`        DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '计算值（固定价=单价 / 折扣率=0.85 / 减免=金额）',
  `min_price`         DECIMAL(10,2)   DEFAULT NULL            COMMENT '底价保护（低于此值仅打标不拦截）',
  `status`            TINYINT         NOT NULL DEFAULT 1      COMMENT '状态：0-停用 1-启用',
  `create_time`       DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`       DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_strategy_id`       (`strategy_id`),
  KEY `idx_mem_member_id`     (`mem_member_id`),
  KEY `idx_customer_level_id` (`customer_level_id`),
  KEY `idx_status`            (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格规则表';

-- ------------------------------------------------------------
-- 4) price_rule_item 价格规则明细表（指定商品 / 阶梯价）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `price_rule_item` (
  `id`            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `rule_id`       BIGINT UNSIGNED NOT NULL                COMMENT '关联规则ID（price_rule.id）',
  `material_id`   BIGINT UNSIGNED NOT NULL                COMMENT '商品物料ID（prod_sku.id）',
  `tier_no`       INT             NOT NULL DEFAULT 1      COMMENT '阶梯序号（从 1 开始）',
  `min_quantity`  INT             NOT NULL DEFAULT 0      COMMENT '最小数量（含）',
  `max_quantity`  INT             DEFAULT NULL            COMMENT '最大数量（含；NULL/0 表示不封顶）',
  `price`         DECIMAL(10,2)   DEFAULT NULL            COMMENT '阶梯单价（固定价）',
  `discount`      DECIMAL(5,4)    DEFAULT NULL            COMMENT '阶梯折扣（折扣率）',
  `reduce_amount` DECIMAL(10,2)   DEFAULT NULL            COMMENT '阶梯减免金额',
  PRIMARY KEY (`id`),
  KEY `idx_rule_id`     (`rule_id`),
  KEY `idx_material_id` (`material_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格规则明细表（指定商品/阶梯价）';

-- ------------------------------------------------------------
-- 5) price_change_log 价格变更日志表（审计）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `price_change_log` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `strategy_id`    BIGINT UNSIGNED DEFAULT NULL            COMMENT '策略ID',
  `rule_id`        BIGINT UNSIGNED DEFAULT NULL            COMMENT '规则ID',
  `operation_type` TINYINT         NOT NULL DEFAULT 1      COMMENT '1-新增 2-修改 3-停用 4-删除',
  `field_name`     VARCHAR(50)     DEFAULT NULL            COMMENT '变更字段',
  `old_value`      VARCHAR(255)    DEFAULT NULL            COMMENT '旧值',
  `new_value`      VARCHAR(255)    DEFAULT NULL            COMMENT '新值',
  `operator_id`    BIGINT UNSIGNED DEFAULT NULL            COMMENT '操作人ID（sys_user.id）',
  `operate_time`   DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '操作时间',
  `reason`         VARCHAR(255)    DEFAULT NULL            COMMENT '变更原因',
  PRIMARY KEY (`id`),
  KEY `idx_strategy_id`    (`strategy_id`),
  KEY `idx_rule_id`        (`rule_id`),
  KEY `idx_operation_type` (`operation_type`),
  KEY `idx_operate_time`   (`operate_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格变更日志表';

-- ------------------------------------------------------------
-- 6) order_price_snapshot 订单价格快照表（历史价格追溯唯一依据）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `order_price_snapshot` (
  `id`                    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `order_id`              BIGINT UNSIGNED NOT NULL                COMMENT '订单ID（trx_order.id）',
  `order_item_id`         BIGINT UNSIGNED NOT NULL                COMMENT '订单明细ID（trx_order_item.id）',
  `material_id`           BIGINT UNSIGNED NOT NULL                COMMENT '商品物料ID（prod_sku.id）',
  `standard_price`        DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '标准售价',
  `matched_strategy_id`   BIGINT UNSIGNED DEFAULT NULL            COMMENT '命中策略ID',
  `matched_strategy_type` TINYINT         DEFAULT NULL            COMMENT '命中策略类型（1-4；0=未命中）',
  `matched_rule_id`       BIGINT UNSIGNED DEFAULT NULL            COMMENT '命中规则ID',
  `final_price`           DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '最终成交价',
  `discount_info`         JSON            DEFAULT NULL            COMMENT '折扣详情快照（JSON）',
  PRIMARY KEY (`id`),
  KEY `idx_order_id`      (`order_id`),
  KEY `idx_order_item_id` (`order_item_id`),
  KEY `idx_material_id`   (`material_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='订单价格快照表';

-- ------------------------------------------------------------
-- 7) mem_recharge 充值订单表（资金入口）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `mem_recharge` (
  `id`              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `recharge_no`     VARCHAR(32)     NOT NULL                COMMENT '充值单号（唯一）',
  `mem_member_id`   BIGINT UNSIGNED NOT NULL                COMMENT '会员ID（mem_member.id）',
  `recharge_amount` DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '充值本金',
  `gift_amount`     DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '赠送金额',
  `pay_channel`     VARCHAR(20)     DEFAULT NULL            COMMENT '支付渠道（WECHAT / ALIPAY / BANK / OFFLINE）',
  `transaction_id`  VARCHAR(64)     DEFAULT NULL            COMMENT '支付流水号（唯一，幂等键）',
  `status`          TINYINT         NOT NULL DEFAULT 0      COMMENT '0-待支付 1-已支付 2-已退款 3-失败',
  `pay_time`        DATETIME        DEFAULT NULL            COMMENT '支付时间',
  `create_time`     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_recharge_no`    (`recharge_no`),
  UNIQUE KEY `uk_transaction_id` (`transaction_id`),
  KEY `idx_mem_member_id` (`mem_member_id`),
  KEY `idx_status`        (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='充值订单表';

-- ------------------------------------------------------------
-- 8) mkt_balance_log 余额流水表（余额真值台账）
--    约定：change_amount 一律记正数，方向由 change_type 决定；
--          after_balance 恒等于「操作后 mem_member.balance」。
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `mkt_balance_log` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `mem_member_id`  BIGINT UNSIGNED NOT NULL                COMMENT '会员ID（mem_member.id）',
  `change_type`    TINYINT         NOT NULL DEFAULT 1      COMMENT '1-充值 2-下单扣减 3-退款退回 4-后台调整 5-冻结 6-解冻',
  `change_amount`  DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '变动金额（正数）',
  `before_balance` DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '变动前可用余额',
  `after_balance`  DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '变动后可用余额（对账真值）',
  `related_type`   VARCHAR(30)     DEFAULT NULL            COMMENT '关联业务类型（recharge/order/refund/adjust）',
  `related_id`     BIGINT UNSIGNED DEFAULT NULL            COMMENT '关联业务ID',
  `remark`         VARCHAR(255)    DEFAULT NULL            COMMENT '备注',
  `operator_id`    BIGINT UNSIGNED DEFAULT NULL            COMMENT '操作人ID',
  `create_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_mem_member_id` (`mem_member_id`),
  KEY `idx_change_type`   (`change_type`),
  KEY `idx_related`       (`related_type`, `related_id`),
  KEY `idx_create_time`   (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='余额流水表（余额真值台账）';

-- ============================================================
-- 9) mem_member 扩展 4 列（价格策略与会员余额模块）
--    纯动态 SQL，无需 DELIMITER；列已存在则自动跳过。
-- ============================================================
SET @db = DATABASE();

-- 9.1 customer_level_id
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='customer_level_id');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `customer_level_id` BIGINT UNSIGNED DEFAULT NULL COMMENT ''关联等级ID（mem_member_level.id）''',
  'SELECT ''customer_level_id 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 9.2 balance（可用余额）
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='balance');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `balance` DECIMAL(10,2) NOT NULL DEFAULT 0.00 COMMENT ''可用余额（先充值后下单）''',
  'SELECT ''balance 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 9.3 frozen_balance（冻结余额）
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='frozen_balance');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `frozen_balance` DECIMAL(10,2) NOT NULL DEFAULT 0.00 COMMENT ''冻结余额（下单占用）''',
  'SELECT ''frozen_balance 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 9.4 total_recharge（累计充值总额，只统计本金）
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='total_recharge');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `total_recharge` DECIMAL(10,2) NOT NULL DEFAULT 0.00 COMMENT ''累计充值总额（仅本金）''',
  'SELECT ''total_recharge 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ============================================================
-- 10) 初始化示例数据（可选，重复执行会跳过已存在的等级编码）
-- ============================================================
INSERT INTO `mem_member_level` (`level_name`, `level_code`, `sort_order`, `default_discount`, `description`, `status`)
SELECT '普通客户', 'NORMAL', 1, 1.0000, '默认等级，无折扣', 1
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `mem_member_level` WHERE `level_code`='NORMAL'));

INSERT INTO `mem_member_level` (`level_name`, `level_code`, `sort_order`, `default_discount`, `description`, `status`)
SELECT '银牌客户', 'SILVER', 2, 0.9500, '95 折', 1
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `mem_member_level` WHERE `level_code`='SILVER'));

INSERT INTO `mem_member_level` (`level_name`, `level_code`, `sort_order`, `default_discount`, `description`, `status`)
SELECT '金牌客户', 'GOLD', 3, 0.9000, '9 折', 1
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `mem_member_level` WHERE `level_code`='GOLD'));

-- ============================================================
-- 执行后校验（可选）
-- ============================================================
-- SELECT TABLE_NAME FROM information_schema.TABLES
--  WHERE TABLE_SCHEMA=DATABASE()
--    AND TABLE_NAME IN ('mem_member_level','price_strategy','price_rule','price_rule_item',
--                       'price_change_log','order_price_snapshot','mem_recharge','mkt_balance_log');
-- SELECT COLUMN_NAME FROM information_schema.COLUMNS
--  WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mem_member'
--    AND COLUMN_NAME IN ('customer_level_id','balance','frozen_balance','total_recharge');
