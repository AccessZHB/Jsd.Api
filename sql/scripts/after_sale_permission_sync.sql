-- ============================================================
-- 售后模块「按钮级权限」同步脚本（幂等）
-- 数据库：MySQL 8.0（库名 jingsuding_order）
--
-- 背景：
--   RefundController / ReturnController 已全部加 [Permission]：
--     POST   /api/after-sale/refund            aftersale:refund:create
--     GET    /api/after-sale/refund/list       aftersale:refund:list
--     GET    /api/after-sale/refund/{id}       aftersale:refund:list
--     PUT    /api/after-sale/refund/{id}/approve  aftersale:refund:approve
--     POST   /api/after-sale/refund/{id}/execute  aftersale:refund:execute
--     POST   /api/after-sale/return            aftersale:return:create
--     GET    /api/after-sale/return/list       aftersale:return:list
--     GET    /api/after-sale/return/{id}       aftersale:return:list
--     PUT    /api/after-sale/return/{id}/receive  aftersale:return:receive
--     GET    /api/after-sale/return/by-refund/{id} aftersale:return:list
--
-- 线上库现状（执行前）：sys_menu 只有 502(退款申请, refund:list) /
--   503(退货管理, return:list)，缺失 create / approve / execute / receive，
--   且 sys_role_menu 中没有任何售后授权 → 非超管账号调用会被 403。
--
-- 本脚本：
--   ① 补 5 个按钮级菜单（menu_type=3，挂在 502 / 503 下）；
--   ② 按最小权限原则授权：
--        运营专员(2)  ：退款/退货 列表 + 创建
--        仓库管理员(3)：退货 列表 + 确认收货
--        财务(4)     ：退款/退货 列表 + 退款审核 + 执行打款
--   ③ 全脚本幂等（NOT EXISTS 去重），可重复执行。
-- ============================================================


-- ---------- ① 按钮级权限菜单 ----------
INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 504, 502, '新增退款', '', '', '', 2, 3, 'aftersale:refund:create', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE id = 504)
  AND NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'aftersale:refund:create');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 505, 502, '退款审核', '', '', '', 3, 3, 'aftersale:refund:approve', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE id = 505)
  AND NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'aftersale:refund:approve');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 506, 502, '执行退款', '', '', '', 4, 3, 'aftersale:refund:execute', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE id = 506)
  AND NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'aftersale:refund:execute');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 507, 503, '创建退货', '', '', '', 2, 3, 'aftersale:return:create', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE id = 507)
  AND NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'aftersale:return:create');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 508, 503, '确认收货', '', '', '', 3, 3, 'aftersale:return:receive', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE id = 508)
  AND NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'aftersale:return:receive');


-- ---------- ② 角色授权（最小权限） ----------
-- 运营专员(role_id=2)：目录 + 两个菜单节点 + 创建
INSERT INTO sys_role_menu (role_id, menu_id, create_time)
SELECT 2, m.id, NOW()
FROM sys_menu m
WHERE (m.id IN (501, 502, 503)
   OR m.permission IN ('aftersale:refund:create', 'aftersale:return:create'))
  AND NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = 2 AND rm.menu_id = m.id);

-- 仓库管理员(role_id=3)：目录 + 退货菜单 + 创建/收货
INSERT INTO sys_role_menu (role_id, menu_id, create_time)
SELECT 3, m.id, NOW()
FROM sys_menu m
WHERE (m.id IN (501, 503)
   OR m.permission IN ('aftersale:return:create', 'aftersale:return:receive'))
  AND NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = 3 AND rm.menu_id = m.id);

-- 财务(role_id=4)：目录 + 两个菜单节点 + 审核 + 执行打款（仅财务可打款）
INSERT INTO sys_role_menu (role_id, menu_id, create_time)
SELECT 4, m.id, NOW()
FROM sys_menu m
WHERE (m.id IN (501, 502, 503)
   OR m.permission IN ('aftersale:refund:approve', 'aftersale:refund:execute'))
  AND NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = 4 AND rm.menu_id = m.id);


-- ---------- ③ 执行结果自检 ----------
SELECT m.id, m.parent_id, m.menu_name, m.menu_type, m.permission
FROM sys_menu m
WHERE m.id = 501 OR m.parent_id IN (501, 502, 503)
ORDER BY m.id;

SELECT rm.role_id, r.role_name, m.id AS menu_id, m.permission
FROM sys_role_menu rm
JOIN sys_role r ON r.id = rm.role_id
JOIN sys_menu m ON m.id = rm.menu_id
WHERE m.permission LIKE 'aftersale:%' OR m.id IN (501, 502, 503)
ORDER BY rm.role_id, m.id;
