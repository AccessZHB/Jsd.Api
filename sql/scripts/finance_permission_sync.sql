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

-- ③ 【已移除】原先自动给运营(2)/仓库(3)授予财务列表只读权限。
--    原因：角色能看到哪些菜单，必须完全由管理员在「分配菜单」里决定，
--    脚本擅自授权会导致「未分配的菜单也出现在导航栏」，与权限配置不一致。
--    如需让某角色查看财务列表，请在后台【角色管理 → 分配菜单】中勾选后保存。

-- ④ 自检
SELECT m.id, m.permission FROM sys_menu m WHERE m.permission = 'finance:receivable:sync';
SELECT rm.role_id, r.role_name, m.id, m.permission
FROM sys_role_menu rm JOIN sys_role r ON r.id=rm.role_id JOIN sys_menu m ON m.id=rm.menu_id
WHERE m.permission LIKE 'finance:%'
ORDER BY rm.role_id, m.id;
