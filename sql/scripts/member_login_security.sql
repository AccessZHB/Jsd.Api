-- ============================================================
-- F-25：会员登录安全（失败计数 + 账号锁定）
-- 数据库：jingsuding_order（MySQL 8.0）
-- 说明：与后台 sys_user 的 fail_count/lock_until 保持一致的口径，
--       让小程序端登录也具备"连续失败 N 次锁定"的防撞库能力。
-- 本脚本幂等，可重复执行。
-- ============================================================

SET @db = DATABASE();

-- 1) fail_count：连续登录失败次数
SET @c1 = (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'mem_member' AND COLUMN_NAME = 'fail_count');
SET @sql1 = IF(@c1 = 0,
  'ALTER TABLE `mem_member` ADD COLUMN `fail_count` INT NOT NULL DEFAULT 0 COMMENT ''连续登录失败次数（成功登录清零）''',
  'SELECT ''fail_count 已存在，跳过'' AS skip1');
PREPARE s1 FROM @sql1; EXECUTE s1; DEALLOCATE PREPARE s1;

-- 2) lock_until：锁定截止时间
SET @c2 = (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'mem_member' AND COLUMN_NAME = 'lock_until');
SET @sql2 = IF(@c2 = 0,
  'ALTER TABLE `mem_member` ADD COLUMN `lock_until` DATETIME NULL COMMENT ''账号锁定截止时间（NULL=未锁定）''',
  'SELECT ''lock_until 已存在，跳过'' AS skip2');
PREPARE s2 FROM @sql2; EXECUTE s2; DEALLOCATE PREPARE s2;

-- 3) 自检
SELECT `id`, `username`, `fail_count`, `lock_until` FROM `mem_member` LIMIT 3;

SELECT 'mem_member 登录安全字段就绪' AS result;
