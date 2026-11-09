-- ============================================================
-- 库存模块 P1 性能优化：缺失索引补齐
-- 数据库：MySQL 8.0（InnoDB / utf8mb4，库名 jingsuding_order）
-- 表结构来源：sql/scripts/Jsd_order.sql（prod_sku / log_stock_log）
--
-- 说明：
-- 1. 单仓库模式，prod_sku.stock 即库存唯一落点，无 warehouse 维度参与索引；
-- 2. log_stock_log 现有索引：idx_prod_info_id / idx_sku_id / idx_change_type / idx_create_time，
--    本脚本只补 2 个联合索引，并清理被联合索引最左前缀覆盖的冗余单列索引；
-- 3. prod_sku 目前只有 PRIMARY KEY(id)，无 prod_info_id 索引；
-- 4. MySQL 8.0 二次索引默认在线构建（INPLACE），但大表仍建议低峰执行。
-- ============================================================


-- ---------- 0. 执行前检查：确认索引是否已存在 ----------
SELECT table_name,
       index_name,
       GROUP_CONCAT(column_name ORDER BY seq_in_index) AS indexed_columns
FROM information_schema.statistics
WHERE table_schema = DATABASE()
  AND table_name IN ('prod_sku', 'log_stock_log')
GROUP BY table_name, index_name
ORDER BY table_name, index_name;


-- ---------- 1. prod_sku：商品 ID 索引 ----------
-- 支撑：按商品加载/删除/统计其全部 SKU（商品详情、聚合保存、商品删除级联）
CREATE INDEX idx_prod_sku_prod_info_id
    ON prod_sku (prod_info_id);


-- ---------- 2. log_stock_log：单据追溯联合索引 ----------
-- 支撑：按来源单据反查流水（WHERE ref_type = ? AND ref_id = ?）
CREATE INDEX idx_log_stock_log_ref_type_ref_id
    ON log_stock_log (ref_type, ref_id);


-- ---------- 3. log_stock_log：SKU + 时间联合索引 ----------
-- 支撑：单个 SKU 的库存变动历史按时间区间查询/倒序翻页
CREATE INDEX idx_log_stock_log_sku_id_create_time
    ON log_stock_log (sku_id, create_time);


-- ---------- 4. 冗余索引清理（可选，建议执行） ----------
-- (sku_id, create_time) 的最左前缀已覆盖 idx_sku_id，
-- 保留会让每次写入多维护一棵 B+ 树，故删除单列索引。
ALTER TABLE log_stock_log DROP INDEX idx_sku_id;


-- ---------- 5. 执行后校验 ----------
SELECT table_name, index_name, non_unique,
       GROUP_CONCAT(column_name ORDER BY seq_in_index) AS indexed_columns
FROM information_schema.statistics
WHERE table_schema = DATABASE()
  AND table_name IN ('prod_sku', 'log_stock_log')
GROUP BY table_name, index_name, non_unique
ORDER BY table_name, index_name;


-- ---------- 回滚（如需撤销本次变更） ----------
-- ALTER TABLE prod_sku       DROP INDEX idx_prod_sku_prod_info_id;
-- ALTER TABLE log_stock_log  DROP INDEX idx_log_stock_log_ref_type_ref_id;
-- ALTER TABLE log_stock_log  DROP INDEX idx_log_stock_log_sku_id_create_time;
-- ALTER TABLE log_stock_log  ADD  INDEX idx_sku_id (sku_id);

-- 注意：本脚本非幂等。重复执行会报 1061 Duplicate key name，
-- 可先执行步骤 0 检查，或先 DROP INDEX 再重建。
