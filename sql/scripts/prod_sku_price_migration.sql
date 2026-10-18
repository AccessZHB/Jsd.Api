-- ================================================================
-- prod_sku 价格字段变更脚本（MySQL 5.7 / 8.x）
-- 数据库：jingsuding_order
--
-- 变更点（与 ProdSku 实体一一对应）：
--   1. price          → retail_price    （Price  → RetailPrice，SKU 零售价，NOT NULL）
--   2. original_price → sale_price      （OriginalPrice → SalePrice，SKU 销售价/划线价）
--   3. cost_price     → 删除            （CostPrice 移除，不再保留 SKU 成本价）
--
-- 执行顺序（重要，别乱）：
--   ① 备份表
--   ② 新增 retail_price / sale_price 两列（允许 NULL，先不设约束）
--   ③ 把旧列数据搬过去（retail_price 取 price；sale_price 取 original_price）
--   ④ 校验数据（行数、NULL 数量）
--   ⑤ 回收旧列 NOT NULL / 默认值，再删除 price / original_price / cost_price
--   ⑥ 核对最终结构
--
-- 注意：
--   - 这是【破坏性变更】，执行后旧字段不可恢复，务必先备份；
--   - 应用侧代码必须先发布（实体/DTO 已改成 RetailPrice / SalePrice），
--     否则旧代码 INSERT/SELECT 会报 Unknown column；
--   - cost_price 若有值需要留存，请先执行第 ③ 步里的【可选】导出语句。
-- ================================================================

USE `jingsuding_order`;

-- ---------------------------------------------------------------
-- ① 备份（必做）
-- ---------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `prod_sku_bak_price_20260910` AS SELECT * FROM `prod_sku`;

-- ---------------------------------------------------------------
-- ② 新增字段（先允许 NULL，搬完数据再收紧）
-- ---------------------------------------------------------------
ALTER TABLE `prod_sku`
    ADD COLUMN `retail_price` DECIMAL(12,2) NULL COMMENT 'SKU零售价（原 price）' AFTER `spec_values`,
    ADD COLUMN `sale_price`   DECIMAL(12,2) NULL DEFAULT 0.00 COMMENT 'SKU销售价/划线价（原 original_price）' AFTER `retail_price`;

-- ---------------------------------------------------------------
-- ③ 数据搬迁
-- ---------------------------------------------------------------
UPDATE `prod_sku`
SET `retail_price`   = `price`,
    `sale_price`     = IFNULL(`original_price`, 0.00);

-- 【可选】若需要保留 SKU 成本价，先导出到临时表再删除列：
-- CREATE TABLE IF NOT EXISTS `prod_sku_cost_price_archive` AS
--     SELECT `id`, `sku_code`, `cost_price` FROM `prod_sku` WHERE `cost_price` IS NOT NULL;

-- ---------------------------------------------------------------
-- ④ 数据校验（retail_price 的 NULL 数量必须为 0，才能继续第 ⑤ 步）
-- ---------------------------------------------------------------
SELECT COUNT(*) AS total_rows,
       SUM(CASE WHEN `retail_price` IS NULL THEN 1 ELSE 0 END) AS retail_price_null,
       SUM(CASE WHEN `sale_price`   IS NULL THEN 1 ELSE 0 END) AS sale_price_null
FROM `prod_sku`;

-- ---------------------------------------------------------------
-- ⑤ 收紧约束 + 删除旧列（确认第 ④ 步没问题后再执行）
--    MySQL 一条 ALTER 里同时 MODIFY 与 DROP，避免中间态被业务读写到
-- ---------------------------------------------------------------
ALTER TABLE `prod_sku`
    MODIFY COLUMN `retail_price` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT 'SKU零售价（原 price）',
    MODIFY COLUMN `sale_price`   DECIMAL(12,2) NULL     DEFAULT 0.00 COMMENT 'SKU销售价/划线价（原 original_price）',
    DROP COLUMN `price`,
    DROP COLUMN `original_price`,
    DROP COLUMN `cost_price`;

-- ---------------------------------------------------------------
-- ⑥ 核对最终结构
-- ---------------------------------------------------------------
-- SHOW COLUMNS FROM `prod_sku`;
-- SELECT `id`, `sku_code`, `retail_price`, `sale_price` FROM `prod_sku` LIMIT 20;

-- ================================================================
-- 回滚方案（出问题时用备份表恢复，注意自增ID与后续新增数据）
-- ================================================================
-- DROP TABLE `prod_sku`;
-- CREATE TABLE `prod_sku` AS SELECT * FROM `prod_sku_bak_price_20260910`;
-- （更稳妥：只把备份数据 UPDATE 回原表，避免表结构/索引丢失）
-- ALTER TABLE `prod_sku`
--     ADD COLUMN `price` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
--     ADD COLUMN `original_price` DECIMAL(12,2) DEFAULT 0.00,
--     ADD COLUMN `cost_price` DECIMAL(12,2) DEFAULT 0.00;
-- UPDATE `prod_sku` t
--     JOIN `prod_sku_bak_price_20260910` b ON b.`id` = t.`id`
-- SET t.`price` = b.`price`, t.`original_price` = b.`original_price`, t.`cost_price` = b.`cost_price`;
