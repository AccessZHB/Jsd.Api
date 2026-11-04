-- ============================================================================
-- 阶段九 · trx_order 增加「本单冻结金额」字段（frozen_amount）
--
-- 背景：mem_member.frozen_balance 是该会员【所有订单共用一个池子】，
--       仅凭订单应付金额无法判断这笔钱是不是本单冻结的。
--       原来的取消逻辑用 UnfreezeForOrderAsync(PayAmount) 去解冻，
--       内部按 min(应付, 池子) 释放 —— 一旦会员同时有「冻结单」和「未冻结单」，
--       取消后者会把前者的冻结额退掉，导致前者支付时报"冻结余额不足"。
--
-- 本字段语义：>0 = 本单仍持有这么多冻结额；解冻 / 转扣减后必须归零（防重复释放）。
--
-- 幂等：可重复执行。
-- ============================================================================

-- ① 加列（已存在则跳过）
SET @col_exists := (
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'trx_order'
      AND column_name = 'frozen_amount'
);

SET @ddl := IF(@col_exists = 0,
    'ALTER TABLE trx_order ADD COLUMN frozen_amount DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT ''本单冻结金额（解冻/转扣减后归零）'' AFTER pay_amount',
    'SELECT ''trx_order.frozen_amount 已存在，跳过加列'' AS msg');

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ② 回填历史数据：待付款订单的真实冻结额 = Σ冻结(change_type=7) - Σ解冻(8)
--    仅回填 order_status=0（待付款）的订单——已支付/已关闭的订单不存在"待释放的冻结"。
--    没有流水、或净额为 0/负 的订单保持 0（不会凭空解冻）。
UPDATE trx_order o
JOIN (
    SELECT l.related_id AS oid,
           SUM(CASE WHEN l.change_type = 7 THEN l.change_amount
                    WHEN l.change_type = 8 THEN -l.change_amount
                    ELSE 0 END) AS net
    FROM mkt_balance_log l
    WHERE l.related_type = 1          -- 1 = 关联订单
    GROUP BY l.related_id
) t ON t.oid = o.id
SET o.frozen_amount = GREATEST(t.net, 0)
WHERE o.order_status = 0
  AND o.frozen_amount = 0
  AND t.net > 0;

-- ③ 自检
SELECT id, order_no, order_status, pay_amount, frozen_amount
FROM trx_order
WHERE order_status = 0
ORDER BY id DESC
LIMIT 10;
