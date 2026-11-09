-- ============================================================
-- 订单列表分页（游标 / 深翻页）缺失索引补齐
-- 数据库：MySQL 8.0（InnoDB / utf8mb4，库名 jingsuding_order）
-- 表结构来源：sql/scripts/Jsd_order.sql（trx_order）
--
-- 背景：
--   GET /api/order/page 已支持两种模式：
--     1) page 模式：ORDER BY id DESC + OFFSET（兼容旧前端）；
--     2) 游标模式：WHERE id < lastId ORDER BY id DESC LIMIT n（无 COUNT/OFFSET）。
--   现有索引只有单列 idx_order_status / idx_create_time / idx_buyer_id，
--   等值过滤后仍要回表按 id 排序，百万级数据下深翻页依然会走 filesort。
--
-- 说明：
-- 1. 联合索引列顺序遵循「等值条件在前、主键在后」：
--    (is_deleted, order_status, id) / (is_deleted, buyer_id, id) / (is_deleted, create_time, id)，
--    InnoDB 二级索引叶子天然包含主键，id 作为末列可直接支撑 ORDER BY id DESC 的扫描方向，
--    无需额外排序；id 单独索引无意义（主键本身已是有序聚簇索引）。
-- 2. MySQL 8.0 创建索引默认 INPLACE，但大表仍建议低峰执行。
-- 3. 本脚本非幂等，重复执行报 1061 Duplicate key name；先执行步骤 0 检查。
-- ============================================================


-- ---------- 0. 执行前检查 ----------
SELECT index_name,
       GROUP_CONCAT(column_name ORDER BY seq_in_index) AS indexed_columns
FROM information_schema.statistics
WHERE table_schema = DATABASE()
  AND table_name = 'trx_order'
GROUP BY index_name
ORDER BY index_name;


-- ---------- 1. 状态筛选 + 游标分页 ----------
-- 支撑：WHERE is_deleted = 0 AND order_status = ? AND id < ? ORDER BY id DESC
CREATE INDEX idx_trx_order_status_cursor
    ON trx_order (is_deleted, order_status, id);


-- ---------- 2. 买家维度 + 游标分页 ----------
-- 支撑：后台按买家筛选、会员「我的订单」（WHERE is_deleted = 0 AND buyer_id = ? AND id < ?）
CREATE INDEX idx_trx_order_buyer_cursor
    ON trx_order (is_deleted, buyer_id, id);


-- ---------- 3. 下单时间区间 + 游标分页 ----------
-- 支撑：WHERE is_deleted = 0 AND create_time BETWEEN ? AND ? AND id < ? ORDER BY id DESC
CREATE INDEX idx_trx_order_time_cursor
    ON trx_order (is_deleted, create_time, id);


-- ---------- 4. 冗余单列索引清理（可选，建议执行） ----------
-- 新联合索引的最左前缀（is_deleted 之外的 order_status / buyer_id / create_time）
-- 已覆盖原有单列索引的查询能力，保留会让每次写入多维护 3 棵 B+ 树。
ALTER TABLE trx_order DROP INDEX idx_order_status;
ALTER TABLE trx_order DROP INDEX idx_buyer_id;
ALTER TABLE trx_order DROP INDEX idx_create_time;


-- ---------- 5. 执行后校验 ----------
SELECT index_name, non_unique,
       GROUP_CONCAT(column_name ORDER BY seq_in_index) AS indexed_columns
FROM information_schema.statistics
WHERE table_schema = DATABASE()
  AND table_name = 'trx_order'
GROUP BY index_name, non_unique
ORDER BY index_name;


-- ---------- 回滚 ----------
-- ALTER TABLE trx_order DROP INDEX idx_trx_order_status_cursor;
-- ALTER TABLE trx_order DROP INDEX idx_trx_order_buyer_cursor;
-- ALTER TABLE trx_order DROP INDEX idx_trx_order_time_cursor;
-- ALTER TABLE trx_order ADD INDEX idx_order_status (order_status);
-- ALTER TABLE trx_order ADD INDEX idx_buyer_id (buyer_id);
-- ALTER TABLE trx_order ADD INDEX idx_create_time (create_time);
