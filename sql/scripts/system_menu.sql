-- ============================================================
-- 系统管理模块 · 按钮权限种子（幂等，可重复执行）
-- 说明：这些权限只用于前端 v-permission 按钮级控制，不生成路由（menu_type=3）。
--       执行前请确保 sys_menu 中已有目录/菜单 802~805（见 802~805 已在库中的记录）。
--       执行后需重新登录（或 F5 刷新）让 /sys/menu 重新下发权限。
--
-- ⚠️ 执行时必须显式指定字符集，否则中文菜单名会变乱码：
--     "/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" --default-character-set=utf8mb4 -uroot -p123456 -D jingsuding_order < system_menu.sql
-- ============================================================

INSERT IGNORE INTO sys_menu
  (id, parent_id, menu_name, path, component, icon, sort_order, menu_type, permission, visible, status)
VALUES
  (806, 802, '导出', '', NULL, 'Download', 1, 3, 'system:operation:export', 1, 1),
  (807, 802, '清理', '', NULL, 'Delete', 2, 3, 'system:operation:clean', 1, 1),
  (808, 803, '导出', '', NULL, 'Download', 1, 3, 'system:loginlog:export', 1, 1),
  (809, 803, '清理', '', NULL, 'Delete', 2, 3, 'system:loginlog:clean', 1, 1),
  (810, 805, '解锁', '', NULL, 'Unlock', 1, 3, 'system:lock:unlock', 1, 1);

-- 校验输出
SELECT id, parent_id, menu_name, menu_type, permission
FROM sys_menu
WHERE id BETWEEN 802 AND 810
ORDER BY id;
