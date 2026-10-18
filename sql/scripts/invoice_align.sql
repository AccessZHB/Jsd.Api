-- ============================================================
-- 发票与税务管理 模块 增量脚本（已有库专用，纯增量、幂等）
-- 执行前请在 mysql 客户端加 --default-character-set=utf8mb4
--   "C:/Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -uroot -p123456 --default-character-set=utf8mb4 jingsuding_order < invoice_align.sql
-- ============================================================

-- ------------------------------------------------------------
-- 1. 客户开票信息表 customer_invoice_info
--    一个客户可保存多套开票抬头（个人/企业），其中一套可设为默认。
--    ⚠️ 抬头信息在开票时会被【快照】进 order_invoice，客户后续改抬头不影响历史发票。
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `customer_invoice_info` (
  `id`           BIGINT        NOT NULL AUTO_INCREMENT                COMMENT 'ID（主键）',
  `customer_id`  BIGINT        NOT NULL DEFAULT 0                     COMMENT '关联客户ID（mem_member.id）',
  `title_type`   TINYINT       NOT NULL DEFAULT 2                     COMMENT '抬头类型：1-个人 2-企业',
  `title_name`   VARCHAR(100)  NOT NULL                               COMMENT '抬头名称（个人=姓名，企业=营业执照上的公司全称）',
  `tax_no`       VARCHAR(32)   NULL                                   COMMENT '纳税人识别号/统一社会信用代码（企业必填，15/17/18/20 位）',
  `bank_name`    VARCHAR(100)  NULL                                   COMMENT '开户银行（开具增值税专用发票时必填）',
  `bank_account` VARCHAR(64)   NULL                                   COMMENT '银行账号（开具增值税专用发票时必填）',
  `address`      VARCHAR(200)  NULL                                   COMMENT '注册地址（开具增值税专用发票时必填）',
  `phone`        VARCHAR(32)   NULL                                   COMMENT '注册电话/联系电话（开具增值税专用发票时必填）',
  `is_default`   TINYINT       NOT NULL DEFAULT 0                     COMMENT '是否默认开票信息：0-否 1-是（同一客户至多一条为 1，由 Service 层维护）',
  `status`       TINYINT       NOT NULL DEFAULT 0                     COMMENT '状态：0-正常 1-停用',
  `create_by`    VARCHAR(64)   NULL                                   COMMENT '创建人',
  `create_time`  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP     COMMENT '创建时间',
  `update_time`  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_customer_id` (`customer_id`),
  KEY `idx_is_default` (`is_default`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='客户开票信息表（发票与税务管理模块）';

-- ------------------------------------------------------------
-- 2. 订单开票记录表 order_invoice
--    一笔订单原则上只开一张票；重开需先把原票作废（status=2）再重新申请。
--    status：0-申请中 1-已开票 2-已作废（含红冲）
--    ⚠️ uk_invoice_no：发票号码唯一，防止财务重复录入同一张票（NULL 不参与唯一约束，
--       正好允许"申请中"阶段发票号为空）。
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `order_invoice` (
  `id`             BIGINT         NOT NULL AUTO_INCREMENT                COMMENT 'ID（主键）',
  `order_id`       BIGINT         NOT NULL DEFAULT 0                     COMMENT '关联订单ID（trx_order.id）',
  `payment_id`     BIGINT         NULL                                   COMMENT '关联支付单ID（payment_order.id，可空：线下付款/信用订单无支付单）',
  `invoice_type`   TINYINT        NOT NULL DEFAULT 1                     COMMENT '发票类型：1-增值税电子普通发票 2-增值税专用发票',
  `title_type`     TINYINT        NOT NULL DEFAULT 2                     COMMENT '抬头类型：1-个人 2-企业（开票时快照）',
  `title_name`     VARCHAR(100)   NOT NULL                               COMMENT '抬头名称（开票时快照）',
  `tax_no`         VARCHAR(32)    NULL                                   COMMENT '纳税人识别号（开票时快照）',
  `bank_name`      VARCHAR(100)   NULL                                   COMMENT '开户银行（开票时快照，专票必填）',
  `bank_account`   VARCHAR(64)    NULL                                   COMMENT '银行账号（开票时快照，专票必填）',
  `address`        VARCHAR(200)   NULL                                   COMMENT '注册地址（开票时快照，专票必填）',
  `phone`          VARCHAR(32)    NULL                                   COMMENT '联系电话（开票时快照，专票必填）',
  `invoice_code`   VARCHAR(32)    NULL                                   COMMENT '发票代码（财务开票后回写；全电发票无代码可留空）',
  `invoice_no`     VARCHAR(64)    NULL                                   COMMENT '发票号码（财务开票后回写）',
  `invoice_date`   DATE           NULL                                   COMMENT '开票日期',
  `total_amount`   DECIMAL(12,2)  NOT NULL DEFAULT 0.00                  COMMENT '发票总金额（价税合计，单位：元）',
  `tax_amount`     DECIMAL(12,2)  NOT NULL DEFAULT 0.00                  COMMENT '税额（单位：元）',
  `status`         TINYINT        NOT NULL DEFAULT 0                     COMMENT '开票状态：0-申请中 1-已开票 2-已作废',
  `red_reason`     VARCHAR(200)   NULL                                   COMMENT '红冲/作废原因',
  `blue_invoice_no` VARCHAR(64)   NULL                                   COMMENT '关联蓝票发票号码（红冲时必填：被红冲的那张蓝票号码）',
  `applicant_id`   BIGINT         NOT NULL DEFAULT 0                     COMMENT '申请人ID（0 表示系统/后台代客申请）',
  `applicant_name` VARCHAR(64)    NULL                                   COMMENT '申请人名称',
  `operator_id`    BIGINT         NOT NULL DEFAULT 0                     COMMENT '开票/作废操作人ID（财务）',
  `operator_name`  VARCHAR(64)    NULL                                   COMMENT '操作人名称',
  `remark`         VARCHAR(500)   NULL                                   COMMENT '备注（如开票平台返回、人工开票说明）',
  `create_time`    DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP     COMMENT '创建时间（申请时间）',
  `update_time`    DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_invoice_no` (`invoice_no`),
  KEY `idx_order_id` (`order_id`),
  KEY `idx_payment_id` (`payment_id`),
  KEY `idx_status` (`status`),
  KEY `idx_invoice_date` (`invoice_date`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='订单开票记录表（发票与税务管理模块）';

-- ------------------------------------------------------------
-- 验证
-- ------------------------------------------------------------
SELECT
  (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE()
     AND table_name IN ('customer_invoice_info','order_invoice')) AS table_count;
