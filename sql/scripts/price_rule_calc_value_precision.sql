-- ============================================================
-- P0 修复：price_rule.calc_value 精度统一为 DECIMAL(10,4)
-- 数据库：MySQL 8.0（库名 jingsuding_order）
--
-- 背景：
--   calc_value 是多态字段：
--     - calc_type=1 固定价（元，需 2 位小数，如 99.00）
--     - calc_type=2 折扣率（需 4 位小数，如 0.8765 表示 87.65 折）
--     - calc_type=3 减免金额（元，需 2 位小数）
--   原列定义 DECIMAL(10,2) 会把折扣率 0.8765 截断为 0.88，
--   造成 0.35% 价差，且与 price_rule_item.discount(DECIMAL(5,4))、
--   mem_member_level.default_discount(DECIMAL(5,4)) 不一致。
--
-- 修复：统一改为 DECIMAL(10,4)，同时覆盖 2 位金额与 4 位折扣率。
-- 注意：
--   1. 历史已入库的折扣率（被截断成 2 位）无法逆向还原 4 位精度，
--      需业务侧核对关键策略后手工修正；本脚本只修结构不修数据。
--   2. 执行前请确认线上无正在写入 price_rule 的长事务，ALTER 会锁表。
-- ============================================================

-- ---------- 执行前检查（确认当前精度） ----------
SELECT column_name, column_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'price_rule'
  AND column_name = 'calc_value';


-- ---------- 1. 修改列精度 ----------
ALTER TABLE price_rule
    MODIFY COLUMN calc_value DECIMAL(10,4) NOT NULL DEFAULT 0.0000
    COMMENT '计算值：固定价=元、折扣率=0.8500、减免额=元';


-- ---------- 2. 执行后校验 ----------
SELECT column_name, column_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'price_rule'
  AND column_name = 'calc_value';


-- ---------- 回滚 ----------
-- ALTER TABLE price_rule
--     MODIFY COLUMN calc_value DECIMAL(10,2) NOT NULL DEFAULT 0.00
--     COMMENT '计算值：固定价=元、折扣率=0.85、减免额=元';
