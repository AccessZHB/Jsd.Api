-- ============================================================
-- 自动任务模块 —— Quartz 表结构补齐脚本（幂等版 v2）
--
-- ✅ 可以无限次重复执行。每条语句前都会查 information_schema，
--    已经符合目标就自动跳过，不会再报 Duplicate column name / Duplicate key name。
--    Navicat / DataGrip / Workbench / mysql 客户端整文件粘贴执行都可以，
--    不需要任何外部执行器。
--
-- ⚠️ 执行前先选中库：USE `jingsuding_order`;
--
-- 背景（以真实库为准，需求文档里没提过这些）：
--   库里现有的 qrtz_* 表是按 Quartz 1.x / 2.0 老 schema 建的，与 Quartz.NET 3.12 不匹配。
--   缺列的症状都是「应用照常启动、接口照常 200，但定时任务一个都不跑 / 一点「启动」就报错」，
--   属于最难查的一类问题。
--   1) qrtz_triggers.MISFIRE_INSTR   Quartz 2.2+ 必填
--      → 启动阶段恢复作业失败：Couldn't recover jobs: Unknown column 'MISFIRE_INSTR'
--   2) qrtz_triggers.CALENDAR_NAME   Quartz.NET 移植版特有，Java 版 qrtz_triggers 没有这列
--      → Couldn't retrieve trigger: Unknown column 'CALENDAR_NAME'
--   3) qrtz_triggers.JOB_DATA        Quartz.NET 把 JobDataMap 存在 trigger 上而不是 job 上
--      → Couldn't retrieve trigger: Unknown column 'JOB_DATA'
--   4) qrtz_job_details / qrtz_fired_triggers 的 IS_* 标志列
--      Quartz 拿 DbType.Boolean 读写，老 schema 是 VARCHAR(1)（存字符串 '1'），
--      MySqlConnector 用 GetBoolean 读会抛 String '1' was not recognized as a valid Boolean。
--      必须改成 TINYINT(1)。
--   5) sys_job_log 索引：日志页按 job_id + create_time 范围筛选，全表扫会随数据量劣化。
-- ============================================================

USE `jingsuding_order`;

-- ------------------------------------------------------------
-- 1) qrtz_triggers 补 MISFIRE_INSTR
-- ------------------------------------------------------------
SET @s := (SELECT IF(COUNT(*) > 0, 'SELECT 1',
  'ALTER TABLE `qrtz_triggers` ADD COLUMN `MISFIRE_INSTR` SMALLINT(6) NOT NULL DEFAULT 0 AFTER `END_TIME`')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'qrtz_triggers' AND COLUMN_NAME = 'MISFIRE_INSTR');
PREPARE st1 FROM @s; EXECUTE st1; DEALLOCATE PREPARE st1;

-- ------------------------------------------------------------
-- 2) qrtz_triggers 补 CALENDAR_NAME（Quartz.NET 特有列）
-- ------------------------------------------------------------
SET @s := (SELECT IF(COUNT(*) > 0, 'SELECT 1',
  'ALTER TABLE `qrtz_triggers` ADD COLUMN `CALENDAR_NAME` VARCHAR(200) NULL DEFAULT NULL AFTER `END_TIME`')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'qrtz_triggers' AND COLUMN_NAME = 'CALENDAR_NAME');
PREPARE st2 FROM @s; EXECUTE st2; DEALLOCATE PREPARE st2;

-- ------------------------------------------------------------
-- 3) qrtz_triggers 补 JOB_DATA
-- ------------------------------------------------------------
SET @s := (SELECT IF(COUNT(*) > 0, 'SELECT 1',
  'ALTER TABLE `qrtz_triggers` ADD COLUMN `JOB_DATA` MEDIUMTEXT NULL DEFAULT NULL AFTER `PRIORITY`')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'qrtz_triggers' AND COLUMN_NAME = 'JOB_DATA');
PREPARE st3 FROM @s; EXECUTE st3; DEALLOCATE PREPARE st3;

-- ------------------------------------------------------------
-- 4) qrtz_simple_triggers 删掉误加的 MISFIRE_INSTR
--    只有卡在这一步的人（曾手动加过）才会执行；没加过就自动跳过。
--    原因：Quartz 3.12 读 trigger 时会
--      SELECT ... FROM QRTZ_TRIGGERS t
--        LEFT JOIN QRTZ_SIMPLE_TRIGGERS st ON ...
--        LEFT JOIN QRTZ_CRON_TRIGGERS  ct ON ...
--    两张表都有 MISFIRE_INSTR 就报 "Column 'MISFIRE_INSTR' in field list is ambiguous"。
--    而且 Quartz.NET 写 simple trigger 的 INSERT 里根本没有这一列，所以这张表本来就该保持原样。
-- ------------------------------------------------------------
SET @s := (SELECT IF(COUNT(*) > 0,
  'ALTER TABLE `qrtz_simple_triggers` DROP COLUMN `MISFIRE_INSTR`', 'SELECT 1')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'qrtz_simple_triggers' AND COLUMN_NAME = 'MISFIRE_INSTR');
PREPARE st4 FROM @s; EXECUTE st4; DEALLOCATE PREPARE st4;

-- ------------------------------------------------------------
-- 5) qrtz_job_details 的 4 个 IS_* 标志列改成 TINYINT(1)
--    只要有一个还不是 tinyint 才执行（此时整表一起改，反正目标状态一致）。
-- ------------------------------------------------------------
SET @s := (SELECT IF(COUNT(*) > 0, 'SELECT 1',
  'ALTER TABLE `qrtz_job_details` MODIFY COLUMN `IS_DURABLE` TINYINT(1) NOT NULL DEFAULT 0,'
  ' MODIFY COLUMN `IS_NONCONCURRENT` TINYINT(1) NOT NULL DEFAULT 0,'
  ' MODIFY COLUMN `IS_UPDATE_DATA` TINYINT(1) NOT NULL DEFAULT 0,'
  ' MODIFY COLUMN `REQUESTS_RECOVERY` TINYINT(1) NOT NULL DEFAULT 0')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'qrtz_job_details'
    AND COLUMN_NAME IN ('IS_DURABLE','IS_NONCONCURRENT','IS_UPDATE_DATA','REQUESTS_RECOVERY')
    AND DATA_TYPE <> 'tinyint');
PREPARE st5 FROM @s; EXECUTE st5; DEALLOCATE PREPARE st5;

-- ------------------------------------------------------------
-- 6) qrtz_fired_triggers 同上的两列
-- ------------------------------------------------------------
SET @s := (SELECT IF(COUNT(*) > 0, 'SELECT 1',
  'ALTER TABLE `qrtz_fired_triggers` MODIFY COLUMN `IS_NONCONCURRENT` TINYINT(1) NULL DEFAULT NULL,'
  ' MODIFY COLUMN `REQUESTS_RECOVERY` TINYINT(1) NULL DEFAULT NULL')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'qrtz_fired_triggers'
    AND COLUMN_NAME IN ('IS_NONCONCURRENT','REQUESTS_RECOVERY')
    AND DATA_TYPE <> 'tinyint');
PREPARE st6 FROM @s; EXECUTE st6; DEALLOCATE PREPARE st6;

-- ------------------------------------------------------------
-- 7) sys_job_log 补索引（幂等：同名索引已存在就跳过）
-- ------------------------------------------------------------
SET @s := (SELECT IF(COUNT(*) > 0, 'SELECT 1',
  'ALTER TABLE `sys_job_log` ADD INDEX `idx_job_log_job_id` (`job_id`)')
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_job_log' AND INDEX_NAME = 'idx_job_log_job_id');
PREPARE st7 FROM @s; EXECUTE st7; DEALLOCATE PREPARE st7;

SET @s := (SELECT IF(COUNT(*) > 0, 'SELECT 1',
  'ALTER TABLE `sys_job_log` ADD INDEX `idx_job_log_create_time` (`create_time`)')
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_job_log' AND INDEX_NAME = 'idx_job_log_create_time');
PREPARE st8 FROM @s; EXECUTE st8; DEALLOCATE PREPARE st8;

SELECT 'qrtz_align.sql 执行完毕（无异常即为全部就绪）' AS result;
