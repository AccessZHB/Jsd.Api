-- ============================================================
-- 售后管理模块 —— 数据库对齐脚本（纯语句版，无 DELIMITER / 无存储过程）
-- 目标库：jingsuding_order
--
-- 报错背景：Table 'jingsuding_order.trx_refund' doesn't exist
--   你已新建 3 张表：trx_refund_log / trx_return / trx_return_item，
--   但 trx_refund（退款资金流主表）在项目中【从未有过建表脚本】，仅被代码引用，
--   所以只要打开"退款申请"页就会报 Unknown table。本脚本第 1 段即解决它。
--
-- 幂等：CREATE TABLE IF NOT EXISTS + information_schema 判列，可重复执行，不破坏现有数据。
-- 用法：全选执行即可（Navicat / DBeaver / mysql CLI 均可）。
-- ============================================================

USE `jingsuding_order`;

-- ------------------------------------------------------------
-- 1) 创建 trx_refund（退款申请表，售后资金流主表）
--    结构严格以后端实体 Jsd.Api/Entities/TrxRefund.cs 为准
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `trx_refund` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `refund_no`      VARCHAR(32)     NOT NULL                COMMENT '退款单号（业务唯一，TK+日期+序号）',
  `order_id`       BIGINT UNSIGNED NOT NULL                COMMENT '订单ID，关联 trx_order.id',
  `order_no`       VARCHAR(50)     DEFAULT NULL            COMMENT '订单号（冗余，便于列表检索）',
  `member_id`      BIGINT UNSIGNED NOT NULL                COMMENT '申请人ID，关联 mem_member.id',
  `refund_amount`  DECIMAL(12,2)   NOT NULL DEFAULT 0.00   COMMENT '退款金额（元）',
  `refund_type`    TINYINT         NOT NULL DEFAULT 1      COMMENT '退款类型：1-仅退款 2-退货退款',
  `refund_reason`  VARCHAR(255)    DEFAULT NULL            COMMENT '退款原因',
  `voucher_images` VARCHAR(500)    DEFAULT NULL            COMMENT '凭证图片（多个用逗号分隔的相对路径）',
  `status`         TINYINT         NOT NULL DEFAULT 0      COMMENT '状态：0-待审核 1-待退款(已通过) 2-已退款 3-已驳回',
  `admin_remark`   VARCHAR(255)    DEFAULT NULL            COMMENT '客服/财务处理备注（审核意见）',
  `operator_id`    BIGINT UNSIGNED DEFAULT NULL            COMMENT '处理人ID（审核/执行人）',
  `handle_time`    DATETIME        DEFAULT NULL            COMMENT '审核处理时间',
  `refund_time`    DATETIME        DEFAULT NULL            COMMENT '实际打款时间（执行退款写入）',
  `create_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_refund_no` (`refund_no`),
  KEY `idx_order_id`    (`order_id`),
  KEY `idx_member_id`   (`member_id`),
  KEY `idx_status`      (`status`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='退款申请表（售后资金流主表）';

-- ------------------------------------------------------------
-- 2) trx_return_item 补 received_quantity（实收数量）
--    仓库收货时要按"实收数量"回滚库存，原建表脚本缺此列。
--    纯动态 SQL，无需 DELIMITER；列已存在则跳过。
-- ------------------------------------------------------------
SET @db  = DATABASE();
SET @cnt = (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'trx_return_item' AND COLUMN_NAME = 'received_quantity'
);
SET @sql = IF(@cnt = 0,
  'ALTER TABLE `trx_return_item` ADD COLUMN `received_quantity` INT NOT NULL DEFAULT 0 COMMENT ''实收数量（仓库收货写入；0=拒收）''',
  'SELECT ''received_quantity 已存在，跳过'' AS skip_reason'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 3) 执行结果自检（应看到 trx_refund / trx_return / trx_return_item / trx_refund_log 四张表）
-- ------------------------------------------------------------
SELECT TABLE_NAME, TABLE_COMMENT
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('trx_refund', 'trx_return', 'trx_return_item', 'trx_refund_log')
ORDER BY TABLE_NAME;

-- 确认 trx_refund 列结构与实体一致（应返回 16 行）
-- SHOW COLUMNS FROM `trx_refund`;
