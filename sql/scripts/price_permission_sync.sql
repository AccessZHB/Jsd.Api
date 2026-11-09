-- ============================================================
-- 价格与客户模块「按钮级权限」同步脚本（幂等）
-- 数据库：MySQL 8.0（库名 jingsuding_order）
--
-- 背景：
--   PriceStrategyController / MemberLevelController / PriceController /
--   OrderPriceSnapshotController 已全部加 [Permission]，但 sys_menu
--   只有 602~605 四个列表菜单（permission 挂在菜单节点上），缺失按钮级
--   权限，且 sys_role_menu 中没有任何价格模块授权 → 非超管账号调用会被 403。
--
-- 本脚本：
--   ① 补 11 个按钮级菜单（menu_type=3，挂在 602/603/605 下）；
--   ② 按最小权限授权：
--        运营专员(2)：全部列表（只读）
--        仓库管理员(3)：全部列表（只读）
--        财务(4)     ：价格管理全权限（含改价、改折扣、改等级、写快照）
--   ③ 全脚本幂等（NOT EXISTS 去重），可重复执行。
-- ============================================================


-- ---------- ① 按钮级权限菜单 ----------
-- 客户等级
INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 606, 602, '新增等级', '', '', '', 2, 3, 'price:level:add', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'price:level:add');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 607, 602, '编辑等级', '', '', '', 3, 3, 'price:level:edit', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'price:level:edit');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 608, 602, '启用/停用等级', '', '', '', 4, 3, 'price:level:status', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'price:level:status');

-- 价格策略
INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 609, 603, '新增策略', '', '', '', 2, 3, 'price:strategy:add', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'price:strategy:add');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 610, 603, '编辑策略', '', '', '', 3, 3, 'price:strategy:edit', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'price:strategy:edit');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 611, 603, '提交启用', '', '', '', 4, 3, 'price:strategy:submit', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'price:strategy:submit');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 612, 603, '停用策略', '', '', '', 5, 3, 'price:strategy:disable', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'price:strategy:disable');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 613, 603, '删除策略', '', '', '', 6, 3, 'price:strategy:delete', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'price:strategy:delete');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 614, 603, '批量维护规则', '', '', '', 7, 3, 'price:strategy:rule:save', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'price:strategy:rule:save');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 615, 603, '代客询价', '', '', '', 8, 3, 'price:quotation', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'price:quotation');

-- 订单价格快照（写权限，仅财务）
INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 616, 605, '写入快照', '', '', '', 2, 3, 'price:snapshot:write', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'price:snapshot:write');


-- ---------- ② 角色授权（最小权限） ----------
-- 运营专员(role_id=2)：仅列表（只读）
INSERT INTO sys_role_menu (role_id, menu_id, create_time)
SELECT 2, m.id, NOW()
FROM sys_menu m
WHERE (m.id IN (601, 602, 603, 604, 605))
  AND NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = 2 AND rm.menu_id = m.id);

-- 仓库管理员(role_id=3)：仅列表（只读）
INSERT INTO sys_role_menu (role_id, menu_id, create_time)
SELECT 3, m.id, NOW()
FROM sys_menu m
WHERE (m.id IN (601, 602, 603, 604, 605))
  AND NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = 3 AND rm.menu_id = m.id);

-- 财务(role_id=4)：价格管理全权限（改价/改折扣/改等级/写快照）
INSERT INTO sys_role_menu (role_id, menu_id, create_time)
SELECT 4, m.id, NOW()
FROM sys_menu m
WHERE (m.id IN (601, 602, 603, 604, 605) OR m.parent_id IN (602, 603, 605))
  AND NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = 4 AND rm.menu_id = m.id);


-- ---------- ③ 执行结果自检 ----------
SELECT m.id, m.parent_id, m.menu_name, m.menu_type, m.permission
FROM sys_menu m
WHERE m.id = 601 OR m.parent_id IN (601, 602, 603, 605)
ORDER BY m.id;

SELECT rm.role_id, r.role_name, m.id AS menu_id, m.permission
FROM sys_role_menu rm
JOIN sys_role r ON r.id = rm.role_id
JOIN sys_menu m ON m.id = rm.menu_id
WHERE m.id = 601 OR m.parent_id IN (601, 602, 603, 605)
ORDER BY rm.role_id, m.id;
