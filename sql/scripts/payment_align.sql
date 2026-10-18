-- ============================================================
-- 微信支付 主动查单与补单 模块 增量脚本（已有库专用，纯增量、幂等）
-- 执行前请在 mysql 客户端加 --default-character-set=utf8mb4
--   "C:/Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -uroot -p123456 --default-character-set=utf8mb4 jingsuding_order < payment_align.sql
-- ============================================================

-- ------------------------------------------------------------
-- 1. 支付单表 payment_order（CREATE TABLE IF NOT EXISTS，可重复执行）
--    状态机：INIT(0) → PAYING(1) → SUCCESS(2) / FAIL(3) / CLOSED(4)
--    ⚠️ out_trade_no 唯一索引：拦截重复支付请求的最后一道防线
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `payment_order` (
  `id`             BIGINT          NOT NULL AUTO_INCREMENT                COMMENT '支付单ID（主键）',
  `biz_order_id`   BIGINT          NOT NULL DEFAULT 0                     COMMENT '关联业务订单ID（如 trx_order.id，支付成功后推进业务订单）',
  `out_trade_no`   VARCHAR(64)     NOT NULL                               COMMENT '商户订单号（业务系统生成，唯一，拦截重复请求）',
  `transaction_id` VARCHAR(64)     NULL                                   COMMENT '微信交易号（支付成功后回填；未支付为空）',
  `amount`         BIGINT          NOT NULL DEFAULT 0                     COMMENT '支付金额（单位：分，与微信 amount.total 同量纲）',
  `pay_channel`    TINYINT         NOT NULL DEFAULT 1                     COMMENT '支付渠道：1-JSAPI 2-APP 3-Native 4-H5',
  `status`         TINYINT         NOT NULL DEFAULT 0                     COMMENT '支付状态：0-INIT 1-PAYING 2-SUCCESS 3-FAIL 4-CLOSED',
  `expire_time`    DATETIME        NOT NULL                               COMMENT '关单/超时时间（超过后定时任务关单）',
  `create_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP     COMMENT '创建时间',
  `update_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_out_trade_no` (`out_trade_no`),
  UNIQUE KEY `uk_transaction_id` (`transaction_id`),
  KEY `idx_biz_order_id` (`biz_order_id`),
  KEY `idx_status` (`status`),
  KEY `idx_expire_time` (`expire_time`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='支付单表（微信支付主动查单与补单模块）';

-- ------------------------------------------------------------
-- 2. 微信支付回调日志表 pay_callback_log
--    每次微信回调都原样落库请求体 JSON，便于排查「掉单/重复推送/验签失败」
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `pay_callback_log` (
  `id`              BIGINT          NOT NULL AUTO_INCREMENT                COMMENT '记录ID（主键）',
  `request_body`    TEXT                                                    COMMENT '回调请求体原始 JSON（排查问题用）',
  `transaction_id`  VARCHAR(64)     NULL                                   COMMENT '微信订单号（解密后；验签失败则为空）',
  `handle_status`   TINYINT         NOT NULL DEFAULT 0                     COMMENT '处理状态：0-待处理 1-成功 2-失败 3-已忽略(幂等)',
  `response_result` TEXT                                                   COMMENT '响应结果（SUCCESS / 失败原因 / 幂等说明）',
  `create_time`     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP     COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_transaction_id` (`transaction_id`),
  KEY `idx_handle_status` (`handle_status`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='微信支付回调日志表';

-- ------------------------------------------------------------
-- 3. 注册定时任务：CheckPendingPaymentJob（每 5 分钟主动查单/补单/关单）
--    job_type 用全限定类名（JobService.ResolveJobType 也支持只写类名自动补程序集名）。
--    cron = 0 0/5 * * * ?（Quartz 7 位含秒语法，每 5 分钟）。
--    concurrent=0 禁止并发（避免重复查单放大微信 API 压力）；status=1 启动即运行。
-- ------------------------------------------------------------
INSERT IGNORE INTO `sys_job`
  (`job_name`, `job_group`, `job_type`, `cron_expression`, `job_params`, `status`, `misfire_policy`, `concurrent`, `timeout`, `retry_count`, `retry_interval`, `create_by`, `update_by`, `remark`)
VALUES
  ('微信支付待支付查单任务', 'DEFAULT', 'Jsd.Api.Jobs.CheckPendingPaymentJob', '0 0/5 * * * ?', NULL, 1, 1, 0, 300, 0, 0, 'system', 'system',
   '每5分钟扫描 payment_order 中 PAYING 订单：未超时逐单调微信查单补单；已超时且微信未支付则关单');

-- ------------------------------------------------------------
-- 验证
-- ------------------------------------------------------------
SELECT 'payment_order / pay_callback_log 已就绪' AS result, COUNT(*) AS job_count
FROM sys_job WHERE job_type = 'Jsd.Api.Jobs.CheckPendingPaymentJob';
