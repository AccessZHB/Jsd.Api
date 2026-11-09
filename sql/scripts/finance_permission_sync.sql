-- ============================================================
-- 财务模块权限补全：新增 finance:receivable:sync + 角色授权（幂等）
-- ============================================================

-- ① 新增「应收账款-同步生成」按钮（挂在 321 应收账款下）
INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 335, 321, '同步应收', '', '', '', 2, 3, 'finance:receivable:sync', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'finance:receivable:sync');

-- ② 财务(role_id=4)：全权限（含同步）
INSERT INTO sys_role_menu (role_id, menu_id, create_time)
SELECT 4, 335, NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = 4 AND rm.menu_id = 335);

-- ③ 运营(2) / 仓库(3)：仅列表只读权限（收款/应收/发票/对账）
INSERT INTO sys_role_menu (role_id, menu_id, create_time)
SELECT r.id, m.id, NOW()
FROM (SELECT 2 AS id UNION ALL SELECT 3) r
CROSS JOIN (
    SELECT id FROM sys_menu WHERE permission IN (
        'finance:receivable:list', 'finance:payment:list',
        'finance:invoice:list', 'finance:reconciliation:list'
    )
) m
WHERE NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = r.id AND rm.menu_id = m.id);

-- ④ 自检
SELECT m.id, m.permission FROM sys_menu m WHERE m.permission = 'finance:receivable:sync';
SELECT rm.role_id, r.role_name, m.id, m.permission
FROM sys_role_menu rm JOIN sys_role r ON r.id=rm.role_id JOIN sys_menu m ON m.id=rm.menu_id
WHERE m.permission LIKE 'finance:%'
ORDER BY rm.role_id, m.id;
