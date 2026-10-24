-- =============================================================
-- 售后模块：小程序端申请售后 配套 SQL
-- 适用：Jsd（4L 甄选产品订货系统）
--
-- 【现状】售后主表 trx_refund **已存在**（实体 Jsd.Api/Entities/TrxRefund.cs），
--        字段已覆盖：订单ID、退款金额、退款原因、凭证图片、状态(0待审核 1待退款 2已退款 3已驳回)、
--        类型(1仅退款 2退货退款)。因此本脚本**不需要建表**，只需补 2 个新列。
--
-- 【本次新增】
--   1) original_order_status  申请售后前的订单状态，用于「审核驳回」时恢复订单原状
--   2) source                 售后单来源：1-后台人工创建  2-小程序会员自助申请
--
-- 【订单状态变化】本次同时新增订单状态 5=售后中（OrderStatuses.AfterSale）：
--   会员申请售后 → 订单 order_status 置 5（售后中），并把原状态写入本表 original_order_status
--   审核通过+退款完成 → 订单置 4（已关闭）
--   审核驳回 → 订单恢复 original_order_status
-- =============================================================

-- -------------------------------------------------------------
-- 一、补齐新列（已存在则跳过，MySQL 不支持 IF NOT EXISTS，用存储过程兜底）
-- -------------------------------------------------------------
SET @db = DATABASE();

-- 1. original_order_status
SET @exist = (SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'trx_refund' AND COLUMN_NAME = 'original_order_status');
SET @sql = IF(@exist = 0,
  'ALTER TABLE trx_refund ADD COLUMN original_order_status TINYINT NULL DEFAULT NULL COMMENT ''申请售前订单状态（驳回时恢复）'' AFTER order_no',
  'SELECT ''original_order_status 已存在，跳过''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 2. source
SET @exist = (SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'trx_refund' AND COLUMN_NAME = 'source');
SET @sql = IF(@exist = 0,
  'ALTER TABLE trx_refund ADD COLUMN source TINYINT NOT NULL DEFAULT 1 COMMENT ''来源：1-后台 2-小程序'' AFTER original_order_status',
  'SELECT ''source 已存在，跳过''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -------------------------------------------------------------
-- 二、建表 SQL（仅供全新环境/迁移参考；当前库已有本表，请勿重复执行）
-- -------------------------------------------------------------
-- DROP TABLE IF EXISTS trx_refund;
-- CREATE TABLE `trx_refund` (
--   `id`                    BIGINT       NOT NULL AUTO_INCREMENT COMMENT '主键',
--   `refund_no`             VARCHAR(32)  NOT NULL                COMMENT '退款单号（唯一，TK+日期+序号）',
--   `order_id`              BIGINT       NOT NULL                COMMENT '订单ID（trx_order.id）',
--   `order_no`              VARCHAR(50)  DEFAULT NULL            COMMENT '订单号（冗余）',
--   `original_order_status` TINYINT      DEFAULT NULL            COMMENT '申请售前订单状态（驳回时恢复）',
--   `source`                TINYINT      NOT NULL DEFAULT 1      COMMENT '来源：1-后台 2-小程序',
--   `member_id`             BIGINT       NOT NULL                COMMENT '申请人（mem_member.id）',
--   `refund_amount`         DECIMAL(12,2) NOT NULL               COMMENT '退款金额（元）',
--   `refund_type`           TINYINT      NOT NULL                COMMENT '1-仅退款 2-退货退款',
--   `refund_reason`         VARCHAR(255) DEFAULT NULL            COMMENT '退款原因',
--   `voucher_images`        VARCHAR(500) DEFAULT NULL            COMMENT '凭证图片（多个逗号分隔）',
--   `status`                TINYINT      NOT NULL DEFAULT 0      COMMENT '0待审核 1待退款 2已退款 3已驳回',
--   `admin_remark`          VARCHAR(255) DEFAULT NULL            COMMENT '处理备注',
--   `operator_id`           BIGINT       DEFAULT NULL            COMMENT '处理人ID',
--   `handle_time`           DATETIME     DEFAULT NULL            COMMENT '审核处理时间',
--   `refund_time`           DATETIME     DEFAULT NULL            COMMENT '实际打款时间',
--   `create_time`           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
--   `update_time`           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
--   PRIMARY KEY (`id`),
--   UNIQUE KEY `uk_refund_no` (`refund_no`),
--   KEY `idx_order_id` (`order_id`),
--   KEY `idx_member_id` (`member_id`),
--   KEY `idx_status` (`status`)
-- ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='退款申请表';

-- -------------------------------------------------------------
-- 三、后台菜单（如尚未执行过 after_sale_menu.sql，请执行它）
--    路径：Jsd.Api/sql/scripts/after_sale_menu.sql
--    菜单：售后管理(150) → 退款申请(151) / 退货管理(152)，按钮权限 aftersale:refund:*
--    后台「退款申请」页会自动展示小程序发起的申请（source=2），审核流程完全一致。
-- -------------------------------------------------------------
