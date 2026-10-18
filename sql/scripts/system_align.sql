-- ============================================================
-- 系统管理模块 —— 数据库对齐脚本（操作日志 / 登录日志 / 登录安全策略）
-- 目标库：jingsuding_order
--
-- 【背景】
--   sys_operation_log / sys_login_log 两张表在主脚本 Jsd_order.sql（约 1982/2011 行）
--   中已定义，且真实库已建好（菜单数据也已在 sys_menu 落地，ID 802~805）。
--   本脚本只做纯增量补齐：
--     1) sys_user 扩展 3 列：登录失败计数 / 锁定截止时间 / 密码版本号
--     2) 新建 sys_config（登录安全策略配置存这里，对应文档"配置写入 sys_config 表"）
--   幂等：可重复执行，已存在自动跳过，不动任何已有数据。
--
-- 【说明：为什么不装 Redis】
--   本项目为单机部署、无 Redis 基础设施，登录失败计数/锁定落在 sys_user 列上
--   （重启不丢失），验证码用进程内 IMemoryCache（TTL 5 分钟，一次性消耗）。
--   若未来扩容为多实例部署，再把这三处换成 Redis 即可，接口契约不变。
-- ============================================================

USE `jingsuding_order`;
SET @db = DATABASE();

-- ------------------------------------------------------------
-- 1) sys_user 补 fail_count（连续登录失败次数）
-- ------------------------------------------------------------
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='sys_user' AND COLUMN_NAME='fail_count');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `sys_user` ADD COLUMN `fail_count` INT NOT NULL DEFAULT 0 COMMENT ''连续登录失败次数（达到阈值锁定，成功登录后清零）'' AFTER `last_login_ip`',
  'SELECT ''sys_user.fail_count 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 2) sys_user 补 lock_until（锁定截止时间，NULL=未锁定）
-- ------------------------------------------------------------
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='sys_user' AND COLUMN_NAME='lock_until');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `sys_user` ADD COLUMN `lock_until` DATETIME NULL DEFAULT NULL COMMENT ''锁定截止时间（NULL-未锁定；达到失败阈值后 = 当前时间 + 锁定时长）'' AFTER `fail_count`',
  'SELECT ''sys_user.lock_until 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 3) sys_user 补 pwd_version（密码版本号：改密后 +1，使该用户所有已签发 JWT 失效）
-- ------------------------------------------------------------
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='sys_user' AND COLUMN_NAME='pwd_version');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `sys_user` ADD COLUMN `pwd_version` INT NOT NULL DEFAULT 0 COMMENT ''密码版本号（修改密码后+1，低于该版本的 JWT 一律拒绝，强制重新登录）'' AFTER `lock_until`',
  'SELECT ''sys_user.pwd_version 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 4) 新建 sys_config（系统配置表：登录安全策略等键值对配置）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `sys_config` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `config_key` VARCHAR(100) NOT NULL COMMENT '配置键（唯一）',
  `config_value` TEXT DEFAULT NULL COMMENT '配置值（JSON 或纯文本）',
  `remark` VARCHAR(255) NOT NULL DEFAULT '' COMMENT '备注说明',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_config_key` (`config_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='系统配置表';

-- 5) 登录安全策略默认配置（幂等：已存在则跳过）
--    maxLoginAttempts     连续失败锁定阈值（次）
--    lockDurationMinutes  锁定时长（分钟）
--    passwordMinLength    密码最小长度
--    passwordRequireUpper 必须含大写字母
--    passwordRequireLower 必须含小写字母
--    passwordRequireDigit 必须含数字
--    captchaEnabled       登录是否必须校验验证码
INSERT IGNORE INTO `sys_config` (`config_key`, `config_value`, `remark`)
VALUES ('security.login',
        '{"maxLoginAttempts":5,"lockDurationMinutes":30,"passwordMinLength":8,"passwordRequireUpper":true,"passwordRequireLower":true,"passwordRequireDigit":true,"captchaEnabled":true,"captchaExpireMinutes":5,"captchaLength":4,"autoUnlock":true,"passwordRequireSpecial":false,"passwordValidDays":0,"tokenExpireHours":24,"allowConcurrentLogin":false,"maxConcurrentSessions":1}',
        '登录安全策略（操作日志/登录日志/登录安全策略模块）');

SELECT 'system_align.sql 执行完成' AS result;
