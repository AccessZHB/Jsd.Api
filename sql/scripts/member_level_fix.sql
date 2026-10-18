-- ============================================================
-- 缺陷修复：新增客户接口 500（Column 'customer_level_id' cannot be null）
--
-- 根因：
--   实体 MemMember.CustomerLevelId 定义为 long?（可空，语义：NULL = 未设置等级），
--   而真实库 mem_member.customer_level_id 为 NOT NULL。
--   新增客户时未赋值 → EF Core 显式写入 NULL → MySQL 拒绝 → DbUpdateException 500。
--
-- 修复策略（取"代码语义"为准，零 C# 改动、零编译风险）：
--   将该列改为 NULL 允许，NULL 即"未设置等级"，与 PriceService /
--   BalanceService 中 CustomerLevelId.HasValue 的判空逻辑完全一致。
--   （mem_member_level 当前为空表，0 与 NULL 均无对应等级，不存在历史数据歧义）
--
-- 执行方式（Windows）：
--   "C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -uroot -p123456 ^
--     --default-character-set=utf8mb4 jingsuding_order < member_level_fix.sql
-- ============================================================

ALTER TABLE `mem_member`
  MODIFY COLUMN `customer_level_id` BIGINT(20) UNSIGNED NULL DEFAULT NULL COMMENT '客户等级ID（NULL=未设置等级）';

-- 校验：应返回 YES / NULL
SELECT COLUMN_NAME, IS_NULLABLE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'jingsuding_order'
  AND TABLE_NAME = 'mem_member'
  AND COLUMN_NAME = 'customer_level_id';
