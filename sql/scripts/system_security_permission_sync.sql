-- ============================================================
-- 系统核心安全架构加固：新增权限种子（幂等，可重复执行）
--
-- 背景：本次为以下高危接口补了 [Permission]，其中 4 个权限码在 sys_menu 中尚不存在，
--       需补齐后才能在前端"角色-菜单"里勾选授权（超管由 PermissionFilter.IsSuper 直接放行，不依赖本表）。
--
-- 已存在、无需新增（仅挂载到接口）：
--   system:security:list   菜单 804  登录安全策略-读取
--   system:operation:list  菜单 802  操作日志-查询
--   system:operation:export菜单 806  操作日志-导出
--   system:operation:clean 菜单 807  操作日志-清理
--   system:loginlog:list   菜单 803  登录日志-查询
--   system:loginlog:export 菜单 808  登录日志-导出
--   system:loginlog:clean  菜单 809  登录日志-清理
--   system:lock:list       菜单 805  已锁定账号-查询
--   system:lock:unlock     菜单 810  账号解锁
--   dict:cache:clear       菜单 911  字典缓存-清空
--   order:logistics:query  菜单 208  物流查询（运营/仓库已授权）
-- ============================================================

-- ① 登录安全策略 - 修改（仅超管，高危：可关闭验证码/放宽锁定阈值）
INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
SELECT 60101, 804, '修改安全策略', 3, 'system:security:update', '', '', '', 1, 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'system:security:update');

-- ② 操作日志 - 删除（仅超管，防止低权限账号"先导出再删除"反取证）
INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
SELECT 60102, 802, '删除日志', 3, 'system:operation:remove', '', '', '', 2, 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'system:operation:remove');

-- ③ 登录日志 - 删除（仅超管）
INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
SELECT 60103, 803, '删除日志', 3, 'system:loginlog:remove', '', '', '', 2, 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'system:loginlog:remove');

-- ④ 字典缓存 - 刷新（仅超管/运维）
INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
SELECT 60104, 903, '刷新缓存', 3, 'dict:cache:refresh', '', '', '', 2, 1, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE permission = 'dict:cache:refresh');

-- 自检
SELECT id, parent_id, menu_name, menu_type, permission
FROM sys_menu
WHERE permission IN ('system:security:update', 'system:operation:remove',
                     'system:loginlog:remove', 'dict:cache:refresh')
ORDER BY id;
