-- ============================================================
-- 微信支付 T+1 对账 模块 增量脚本（已有库专用，纯增量、幂等）
-- 执行前请在 mysql 客户端加 --default-character-set=utf8mb4
--   "C:/Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -uroot -p123456 --default-character-set=utf8mb4 jingsuding_order < reconciliation_align.sql
-- ============================================================

-- ------------------------------------------------------------
-- 1. 微信对账单主表 wechat_daily_bill
--    一天 + 一种账单类型 一条记录（uk_bill_date_type 保证重复下载是 UPDATE 而非 INSERT）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `wechat_daily_bill` (
  `id`              BIGINT          NOT NULL AUTO_INCREMENT                COMMENT 'ID（主键）',
  `bill_date`       DATE            NOT NULL                               COMMENT '账单日期（T日，如 2026-09-27；微信当日账单次日可下载）',
  `bill_type`       VARCHAR(16)     NOT NULL DEFAULT 'ALL'                 COMMENT '对账账单类型：ALL-所有交易 SUCCESS-支付成功 REFUND-退款交易',
  `download_url`    VARCHAR(1024)   NULL                                   COMMENT '文件下载地址（微信申请账单接口返回，有效期很短，仅作留痕）',
  `hash_type`       VARCHAR(16)     NULL                                   COMMENT '账单摘要算法（微信返回，一般为 SHA1）',
  `hash_value`      VARCHAR(255)    NULL                                   COMMENT '账单原始摘要值（可用于校验文件完整性）',
  `total_count`     INT             NOT NULL DEFAULT 0                     COMMENT '总交易笔数（从账单明细行统计）',
  `total_amount`    DECIMAL(18,2)   NOT NULL DEFAULT 0.00                  COMMENT '总金额（单位：元，账单所有明细「订单金额」合计）',
  `refund_amount`   DECIMAL(18,2)   NOT NULL DEFAULT 0.00                  COMMENT '退款金额合计（单位：元）',
  `total_fee`       DECIMAL(18,2)   NOT NULL DEFAULT 0.00                  COMMENT '手续费合计（单位：元）',
  `recon_status`    TINYINT         NOT NULL DEFAULT 0                     COMMENT '对账状态：0-待下载 1-处理中 2-对账完成 3-异常',
  `recon_result`    VARCHAR(1000)   NULL                                   COMMENT '对账结论摘要（如 总笔数/总金额/异常笔数）',
  `error_msg`       TEXT            NULL                                   COMMENT '异常信息（下载失败/解析失败/对账失败原因）',
  `create_time`     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP     COMMENT '创建时间',
  `update_time`     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_bill_date_type` (`bill_date`, `bill_type`),
  KEY `idx_recon_status` (`recon_status`),
  KEY `idx_bill_date` (`bill_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='微信对账单主表（T+1对账模块）';

-- ------------------------------------------------------------
-- 2. 对账单明细表 wechat_bill_detail
--    每一条 = 微信账单 CSV 的一行数据（含支付+退款+手续费，单位统一「元」）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `wechat_bill_detail` (
  `id`             BIGINT          NOT NULL AUTO_INCREMENT                COMMENT 'ID（主键）',
  `bill_id`        BIGINT          NOT NULL DEFAULT 0                     COMMENT '关联主表ID（wechat_daily_bill.id）',
  `transaction_id` VARCHAR(64)     NULL                                   COMMENT '微信交易号（账单列「微信订单号」）',
  `out_trade_no`   VARCHAR(64)     NULL                                   COMMENT '商户订单号（对账与系统订单的唯一连接键）',
  `trade_time`     DATETIME        NULL                                   COMMENT '交易时间（账单列「交易时间」）',
  `trade_state`    VARCHAR(32)     NULL                                   COMMENT '交易状态：SUCCESS-支付成功 REFUND-转入退款 REVOKED-已撤销 NOTPAY-未支付 CLOSED-已关闭 PAYERROR-支付失败',
  `total_fee`      DECIMAL(18,2)   NOT NULL DEFAULT 0.00                  COMMENT '订单金额（单位：元，账单列「订单金额」）',
  `refund_fee`     DECIMAL(18,2)   NOT NULL DEFAULT 0.00                  COMMENT '退款金额（单位：元，账单列「退款金额」）',
  `poundage`       DECIMAL(18,2)   NOT NULL DEFAULT 0.00                  COMMENT '手续费（单位：元，账单列「手续费」）',
  `create_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP     COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_bill_id` (`bill_id`),
  KEY `idx_out_trade_no` (`out_trade_no`),
  KEY `idx_transaction_id` (`transaction_id`),
  KEY `idx_trade_time` (`trade_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='微信对账单明细表（T+1对账模块）';

-- ------------------------------------------------------------
-- 3. 对账差异核查表 reconciliation_check
--    每一条 = 一处账账差异（长款/短款/金额不符/状态不符）
--    ⚠️ 重跑对账会先物理清除该账单日的旧差异，再重新写入（保证幂等）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `reconciliation_check` (
  `id`             BIGINT          NOT NULL AUTO_INCREMENT                COMMENT 'ID（主键）',
  `bill_id`        BIGINT          NOT NULL DEFAULT 0                     COMMENT '关联对账单主表ID（wechat_daily_bill.id，手动新增时可填0）',
  `order_id`       BIGINT          NOT NULL DEFAULT 0                     COMMENT '关联订单ID（payment_order.id；长款-仅微信有时为0）',
  `out_trade_no`   VARCHAR(64)     NULL                                   COMMENT '商户订单号（定位差异的核心键）',
  `transaction_id` VARCHAR(64)     NULL                                   COMMENT '微信交易号',
  `diff_type`      TINYINT         NOT NULL DEFAULT 0                     COMMENT '差异类型：1-长款(仅微信有) 2-短款(仅系统有) 3-金额不符 4-状态不符',
  `wechat_amount`  DECIMAL(18,2)   NOT NULL DEFAULT 0.00                  COMMENT '微信侧金额（单位：元；短款时无此记录填0）',
  `system_amount`  DECIMAL(18,2)   NOT NULL DEFAULT 0.00                  COMMENT '系统侧金额（单位：元；长款时系统无记录填0）',
  `wechat_status`  VARCHAR(32)     NULL                                   COMMENT '微信侧交易状态（SUCCESS/REFUND/CLOSED…）',
  `system_status`  INT             NULL                                   COMMENT '系统侧支付状态（payment_order.status：0-INIT 1-PAYING 2-SUCCESS 3-FAIL 4-CLOSED）',
  `handle_status`  TINYINT         NOT NULL DEFAULT 0                     COMMENT '处理状态：0-待处理 1-已处理 2-挂账',
  `handler`        VARCHAR(64)     NULL                                   COMMENT '处理人',
  `remark`         VARCHAR(500)    NULL                                   COMMENT '处理备注',
  `create_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP     COMMENT '核查时间',
  `update_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_bill_id` (`bill_id`),
  KEY `idx_diff_type` (`diff_type`),
  KEY `idx_handle_status` (`handle_status`),
  KEY `idx_out_trade_no` (`out_trade_no`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='对账差异核查表（T+1对账模块）';

-- ------------------------------------------------------------
-- 4. 注册定时任务：DailyReconciliationJob
--    T+1 特性：微信当日账单次日（约 9 点后）才可下载，这里建议每天凌晨 3 点跑【昨日】账单。
--    cron = 0 0 3 * * ?（Quartz 7 位含秒语法，每天 03:00:00）
--    concurrent=0 禁止并发（避免重复下载/重复写差异）；status=1 启动即注册。
--    用 INSERT ... SELECT ... WHERE NOT EXISTS 保证幂等（不依赖 sys_job 上的唯一索引）。
-- ------------------------------------------------------------
INSERT INTO `sys_job`
  (`job_name`, `job_group`, `job_type`, `cron_expression`, `job_params`, `status`, `misfire_policy`, `concurrent`, `timeout`, `retry_count`, `retry_interval`, `create_by`, `update_by`, `remark`)
SELECT
  '微信支付T+1对账任务', 'DEFAULT', 'Jsd.Api.Jobs.DailyReconciliationJob', '0 0 3 * * ?', NULL, 1, 1, 0, 1800, 0, 0, 'system', 'system',
  '每天凌晨3点拉取昨日微信账单（gzip）并自动对账：比对 payment_order 支付成功记录与 wechat_bill_detail 明细，差异写入 reconciliation_check'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM (SELECT job_type FROM `sys_job`) t WHERE t.job_type = 'Jsd.Api.Jobs.DailyReconciliationJob'
);

-- ------------------------------------------------------------
-- 验证
-- ------------------------------------------------------------
SELECT
  (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE()
     AND table_name IN ('wechat_daily_bill','wechat_bill_detail','reconciliation_check')) AS table_count,
  (SELECT COUNT(*) FROM sys_job WHERE job_type = 'Jsd.Api.Jobs.DailyReconciliationJob') AS job_count;
