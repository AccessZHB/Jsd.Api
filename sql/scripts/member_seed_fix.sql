-- ============================================================
-- BUG-05 配套修复②：补齐「下单买家（会员）」种子数据
--
-- 背景：
--   trx_order.buyer_id 指向 mem_member.id（会员=买家，与后台 sys_user 物理隔离，
--   见 customer_init.sql 建表注释）。但 order_test_data.sql 只插了订单/明细，
--   **没有插入任何 mem_member 数据**，导致种子订单 buyer_id = 1001~1006 全是孤儿引用，
--   后续退款执行、余额扣减、应收生成、开票归属一律报「会员不存在」。
--
-- 本脚本：按种子订单的 buyer_id 补齐 6 个会员（id 1001~1006），使既有订单可正常流转。
-- 幂等：仅当该 id 不存在时插入，重复执行无副作用。
--
-- 执行方式（Windows）：
--   "C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -uroot -p123456 ^
--     --default-character-set=utf8mb4 jingsuding_order < member_seed_fix.sql
--
-- ⚠️ 说明：password 留空（客户前台登录需先在客户管理中重置密码）；
--          balance 预置 10000.00 仅为方便下单/退款联调，正式环境请改为 0。
-- ============================================================

INSERT INTO `mem_member`
  (`id`, `member_no`, `name`, `company_name`, `level`, `discount_rate`,
   `balance`, `credit_limit`, `account_period`, `username`, `password`,
   `phone`, `contact_person`, `address`, `remark`, `status`, `is_deleted`)
SELECT 1001, 'M1001', '示例客户-陈晓明', '明视光学有限公司',   1, 0.95, 10000.00, 0.00, 0, 'buyer1001', '', '13800001001', '陈晓明', '杭州市西湖区文三路 100 号', 'BUG-05 补录的下单买家（种子订单 9001）', 1, 0
WHERE NOT EXISTS (SELECT 1 FROM `mem_member` WHERE `id` = 1001);

INSERT INTO `mem_member`
  (`id`, `member_no`, `name`, `company_name`, `level`, `discount_rate`,
   `balance`, `credit_limit`, `account_period`, `username`, `password`,
   `phone`, `contact_person`, `address`, `remark`, `status`, `is_deleted`)
SELECT 1002, 'M1002', '示例客户-李慧', '慧眼眼镜连锁',         1, 0.95, 10000.00, 0.00, 0, 'buyer1002', '', '13800001002', '李慧',   '宁波市鄞州区中山东路 200 号', 'BUG-05 补录的下单买家（种子订单 9002）', 1, 0
WHERE NOT EXISTS (SELECT 1 FROM `mem_member` WHERE `id` = 1002);

INSERT INTO `mem_member`
  (`id`, `member_no`, `name`, `company_name`, `level`, `discount_rate`,
   `balance`, `credit_limit`, `account_period`, `username`, `password`,
   `phone`, `contact_person`, `address`, `remark`, `status`, `is_deleted`)
SELECT 1003, 'M1003', '示例客户-王建国', '建国视光中心',       2, 0.90, 10000.00, 0.00, 0, 'buyer1003', '', '13800001003', '王建国', '温州市鹿城区人民路 300 号',   'BUG-05 补录的下单买家（种子订单 9003）', 1, 0
WHERE NOT EXISTS (SELECT 1 FROM `mem_member` WHERE `id` = 1003);

INSERT INTO `mem_member`
  (`id`, `member_no`, `name`, `company_name`, `level`, `discount_rate`,
   `balance`, `credit_limit`, `account_period`, `username`, `password`,
   `phone`, `contact_person`, `address`, `remark`, `status`, `is_deleted`)
SELECT 1004, 'M1004', '示例客户-赵敏', '敏行眼镜有限公司',     0, 1.00, 10000.00, 0.00, 0, 'buyer1004', '', '13800001004', '赵敏',   '嘉兴市南湖区中山路 400 号',   'BUG-05 补录的下单买家（种子订单 9004）', 1, 0
WHERE NOT EXISTS (SELECT 1 FROM `mem_member` WHERE `id` = 1004);

INSERT INTO `mem_member`
  (`id`, `member_no`, `name`, `company_name`, `level`, `discount_rate`,
   `balance`, `credit_limit`, `account_period`, `username`, `password`,
   `phone`, `contact_person`, `address`, `remark`, `status`, `is_deleted`)
SELECT 1005, 'M1005', '示例客户-孙立', '立信光学科技有限公司', 1, 0.95, 10000.00, 0.00, 0, 'buyer1005', '', '13800001005', '孙立',   '金华市婺城区八一南街 500 号', 'BUG-05 补录的下单买家（种子订单 9005）', 1, 0
WHERE NOT EXISTS (SELECT 1 FROM `mem_member` WHERE `id` = 1005);

INSERT INTO `mem_member`
  (`id`, `member_no`, `name`, `company_name`, `level`, `discount_rate`,
   `balance`, `credit_limit`, `account_period`, `username`, `password`,
   `phone`, `contact_person`, `address`, `remark`, `status`, `is_deleted`)
SELECT 1006, 'M1006', '示例客户-周涛', '涛声眼镜商行',         0, 1.00, 10000.00, 0.00, 0, 'buyer1006', '', '13800001006', '周涛',   '绍兴市越城区解放路 600 号',   'BUG-05 补录的下单买家（种子订单 9006）', 1, 0
WHERE NOT EXISTS (SELECT 1 FROM `mem_member` WHERE `id` = 1006);

-- ============================================================
-- 校验：应返回 6 行，且「孤儿订单数」为 0
-- ============================================================
SELECT COUNT(*) AS member_seeded FROM `mem_member` WHERE `id` BETWEEN 1001 AND 1006;

SELECT COUNT(*) AS orphan_orders
FROM `trx_order` o
LEFT JOIN `mem_member` m ON m.`id` = o.`buyer_id`
WHERE m.`id` IS NULL;
