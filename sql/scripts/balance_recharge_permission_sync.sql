-- ============================================================
-- 会员余额模块权限补全：新增充值按钮权限 + 角色授权（幂等）
-- 现有菜单：701 会员余额 / 702 充值管理(member:recharge:list) / 703 余额流水(member:balance-log:list)
-- ============================================================

-- ① 充值管理下新增按钮：创建充值单、充值详情
INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 704, 702, '创建充值单', '', '', '', 1, 3, 'member:recharge:create', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'member:recharge:create');

INSERT INTO sys_menu (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status, create_time, update_time)
SELECT 705, 702, '充值详情', '', '', '', 2, 3, 'member:recharge:detail', 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'member:recharge:detail');

-- ② 财务(role_id=4)：充值全权限 + 余额流水查询
INSERT INTO sys_role_menu (role_id, menu_id, create_time)
SELECT 4, m.id, NOW()
FROM sys_menu m
WHERE m.permission IN (
    'member:recharge:list', 'member:recharge:create', 'member:recharge:detail',
    'member:balance-log:list'
)
AND NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = 4 AND rm.menu_id = m.id);

-- ③ 【已移除】原先自动给运营(2)/仓库(3)授予「充值管理 / 余额流水」列表权限。
--    原因：菜单可见性必须完全由管理员在「分配菜单」中决定，
--    脚本擅自授权会出现「角色未分配却能看到菜单」的权限越界。
--    如需授权，请在后台【角色管理 → 分配菜单】中勾选后保存。

-- ④ 自检
SELECT m.id, m.permission FROM sys_menu m WHERE m.permission LIKE 'member:recharge:%' OR m.permission = 'member:balance-log:list' ORDER BY m.id;
SELECT rm.role_id, r.role_name, m.id, m.permission
FROM sys_role_menu rm JOIN sys_role r ON r.id=rm.role_id JOIN sys_menu m ON m.id=rm.menu_id
WHERE m.permission LIKE 'member:recharge:%' OR m.permission = 'member:balance-log:list'
ORDER BY rm.role_id, m.id;
