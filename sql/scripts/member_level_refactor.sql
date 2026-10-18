-- ============================================================
-- 客户折扣体系重构：会员等级 ↔ 价格策略 联动
-- 数据库：jingsuding_order（如库名不同，请先改下面的 USE 语句）
--
-- 重构目标：
--   1. 客户折扣率统一来源于【客户等级】基础数据（mem_member_level.default_discount），
--      客户表 mem_member 不再独立存储折扣率；
--   2. 删除冗余列：mem_member.level（旧 0/1/2 枚举）、mem_member.discount_rate（独立折扣率）；
--   3. 客户只保留 customer_level_id 关联等级，折扣在取价时【动态计算、不落库】，
--      保证"改等级折扣 → 所有该等级客户立即生效"，不会出现历史客户折扣僵死。
--
-- ⚠️ 幂等：本脚本可重复执行（列不存在时自动跳过，不会报错）。
-- ⚠️ 执行顺序：建议先跑第 0 段的【体检查询】，确认无异常后再放开第 1/2 段的删除语句。
-- ============================================================

USE jingsuding_order;

-- ============================================================
-- 0. 执行前体检（只读，先跑这段，确认影响面）
-- ============================================================

-- 0.1 现有客户数、其中未设置等级的数量（删除旧 level 列后这些客户将变为"未分级"→按标准售价）
SELECT
    COUNT(*)                                                   AS total_member,
    SUM(CASE WHEN customer_level_id IS NULL OR customer_level_id = 0 THEN 1 ELSE 0 END) AS no_level_member,
    SUM(CASE WHEN discount_rate IS NOT NULL AND discount_rate < 1 THEN 1 ELSE 0 END)     AS had_discount_member
FROM mem_member
WHERE is_deleted = 0;

-- 0.2 客户等级表现状（确认已有等级数据，否则删除旧 level 后所有客户都无折扣）
SELECT id, level_code, level_name, default_discount, status
FROM mem_member_level
ORDER BY sort_order DESC, id;

-- 0.3 【可选】历史等级值迁移提示：
--     旧 level 是 0/1/2 的枚举（普通/VIP/核心），与新表 mem_member_level.id 无对应关系。
--     如需保留历史等级含义，请先手工在新表建好等级（拿到 id），再执行类似下面的映射更新：
-- UPDATE mem_member SET customer_level_id = <新等级ID> WHERE level = 1 AND is_deleted = 0;  -- 例：旧 VIP → 新等级
-- UPDATE mem_member SET customer_level_id = <新等级ID> WHERE level = 2 AND is_deleted = 0;  -- 例：旧核心 → 新等级


-- ============================================================
-- 1. 删除旧等级枚举列 mem_member.level
-- ============================================================
SET @db := DATABASE();
SET @sql := (
    SELECT IF(
        (SELECT COUNT(*) FROM information_schema.COLUMNS
          WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'mem_member' AND COLUMN_NAME = 'level') > 0,
        'ALTER TABLE mem_member DROP COLUMN `level`',
        "SELECT 'skip: mem_member.level 不存在，无需删除' AS msg"
    )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- ============================================================
-- 2. 删除独立折扣率列 mem_member.discount_rate
--    折扣统一由 customer_level_id → mem_member_level.default_discount 决定
-- ============================================================
SET @sql := (
    SELECT IF(
        (SELECT COUNT(*) FROM information_schema.COLUMNS
          WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'mem_member' AND COLUMN_NAME = 'discount_rate') > 0,
        'ALTER TABLE mem_member DROP COLUMN `discount_rate`',
        "SELECT 'skip: mem_member.discount_rate 不存在，无需删除' AS msg"
    )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- ============================================================
-- 3. 确认索引存在（等级筛选 + 取价定位）
--    AppDbContext 已声明，若库里缺失可手工补（名字可自定义，MySQL 无 IF NOT EXISTS 语法，重复执行会报错属正常）
-- ============================================================
-- ALTER TABLE mem_member  ADD INDEX idx_member_level (customer_level_id);
-- ALTER TABLE price_rule  ADD INDEX idx_rule_level  (customer_level_id);


-- ============================================================
-- 4. 执行后校验
-- ============================================================
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'mem_member'
  AND COLUMN_NAME IN ('level', 'discount_rate', 'customer_level_id');
-- 预期：level / discount_rate 无结果（已删除），仅剩 customer_level_id
