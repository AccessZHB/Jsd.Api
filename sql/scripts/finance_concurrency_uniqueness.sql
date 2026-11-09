-- ============================================================
-- P0 修复：核销明细表唯一约束 + 发票单订单唯一约束
-- 数据库：MySQL 8.0（库名 jingsuding_order）
--
-- 背景：
--   1) mkt_payment_item 缺少 (payment_id, receivable_id) 唯一索引，
--      并发核销同一笔(收款单, 应收单)会写入重复明细 → 资金重复扣减。
--   2) order_invoice 缺少"同一订单只能有一张有效票"约束，
--      并发申请开票会写入重复记录 → 重复向税控平台开票（税务事故）。
--
-- 注意：
--   - 脚本执行前请先清理历史重复数据（见下方预检查 SQL），否则 ALTER ADD UNIQUE 会失败。
--   - order_invoice 采用【生成列】方案：active_order_id 在作废时为 NULL，
--     仅对有效票(申请中/已开票)做唯一约束，从而保留"作废后可重开"的业务能力。
-- ============================================================


-- ---------- 预检查 1：mkt_payment_item 是否存在重复 (payment_id, receivable_id) ----------
SELECT payment_id, receivable_id, COUNT(*) AS cnt
FROM mkt_payment_item
GROUP BY payment_id, receivable_id
HAVING cnt > 1;
-- 若有结果，需先人工清理（保留 id 最小的一条，删除其余）：
-- DELETE p1 FROM mkt_payment_item p1
-- INNER JOIN mkt_payment_item p2
--   ON p1.payment_id = p2.payment_id AND p1.receivable_id = p2.receivable_id AND p1.id > p2.id;


-- ---------- 1. mkt_payment_item：核销唯一约束 ----------
ALTER TABLE mkt_payment_item
    ADD UNIQUE KEY uk_payment_receivable (payment_id, receivable_id);


-- ---------- 预检查 2：order_invoice 是否存在同一订单多张有效票（status != 2） ----------
SELECT order_id, COUNT(*) AS cnt
FROM order_invoice
WHERE status <> 2
GROUP BY order_id
HAVING cnt > 1;
-- 若有结果，需先把重复的作废（保留最新一张），再执行下方 ALTER。


-- ---------- 2. order_invoice：单订单有效票唯一约束 ----------
-- 2.1 新增生成列 active_order_id：
--     status=2(已作废) → NULL（不参与唯一约束，允许作废后重开）
--     其它状态(申请中/已开票) → order_id
ALTER TABLE order_invoice
    ADD COLUMN active_order_id BIGINT
        GENERATED ALWAYS AS (CASE WHEN status = 2 THEN NULL ELSE order_id END) VIRTUAL
        COMMENT '有效票订单ID（作废时为NULL，用于唯一约束：同一订单只能一张有效票）';

-- 2.2 在生成列上建唯一索引
ALTER TABLE order_invoice
    ADD UNIQUE KEY uk_order_active (active_order_id);


-- ---------- 执行后校验 ----------
SHOW INDEX FROM mkt_payment_item WHERE Key_name = 'uk_payment_receivable';
SHOW INDEX FROM order_invoice WHERE Key_name = 'uk_order_active';


-- ---------- 回滚 ----------
-- ALTER TABLE mkt_payment_item DROP INDEX uk_payment_receivable;
-- ALTER TABLE order_invoice DROP INDEX uk_order_active;
-- ALTER TABLE order_invoice DROP COLUMN active_order_id;
