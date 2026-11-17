-- ============================================================
-- 修复：清理历史权限脚本误授予的角色菜单（幂等，可重复执行）
--
-- 背景：
--   finance_permission_sync.sql / balance_recharge_permission_sync.sql
--   早期为了让业务可用，自动给 运营专员(role_id=2)、仓库管理员(role_id=3)
--   授予了财务结算（321/322/325/331）与余额流水（703）的列表权限。
--   这违反了「菜单可见性只由管理员在分配菜单中决定」的原则，
--   表现为：角色未勾选财务结算，登录后导航栏却出现该一级菜单及二级菜单。
--
-- 处理：
--   删除运营/仓库角色上被脚本注入的财务列表与会员余额菜单。
--   如需恢复授权，请在后台【角色管理 → 分配菜单】中勾选后保存，
--   不要再用 SQL 直接写 sys_role_menu。
-- ============================================================

-- 运营专员(2)：移除 财务结算二级菜单 + 余额流水（保留充值管理 702）
DELETE FROM sys_role_menu WHERE role_id = 2 AND menu_id IN (321, 322, 325, 331, 703);

-- 仓库管理员(3)：移除 财务结算二级菜单 + 充值管理 + 余额流水
DELETE FROM sys_role_menu WHERE role_id = 3 AND menu_id IN (321, 322, 325, 331, 702, 703);

-- ---------- 自检 ----------
-- 1) 不应再查到运营/仓库角色的财务权限
SELECT rm.role_id, r.role_name, m.id, m.menu_name, m.permission
FROM sys_role_menu rm
JOIN sys_role r ON r.id = rm.role_id
JOIN sys_menu m ON m.id = rm.menu_id
WHERE rm.role_id IN (2, 3)
  AND (m.permission LIKE 'finance:%' OR m.permission = 'member:balance-log:list')
ORDER BY rm.role_id, m.id;

-- 2) 运营专员当前的菜单（应只剩 订单/售后/价格/会员余额-充值管理/按钮级文件上传）
SELECT m.id, m.parent_id, m.menu_name, m.menu_type, m.permission
FROM sys_role_menu rm
JOIN sys_menu m ON m.id = rm.menu_id
WHERE rm.role_id = 2
ORDER BY m.parent_id, m.sort_order;
