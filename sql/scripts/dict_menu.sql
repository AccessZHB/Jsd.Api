-- ============================================================
-- 字典管理模块 · 按钮权限种子（幂等，可重复执行）
--
-- 【执行方式】务必带字符集参数，否则中文菜单名会变乱码：
--   "C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" --default-character-set=utf8mb4 -uroot -p123456 -D jingsuding_order < F:/WorkSpace/Jsd/dict_menu.sql
--
-- 【前提】目录/菜单 901 字典管理、902 字典类型、903 字典数据 已存在（本次未新建，
--        只补按钮级权限）。若不存在请先补这三条主记录。
--
-- 【说明】按钮(menu_type=3)不生成路由，仅用于前端 v-permission 按钮级控制；
--        权限标识与后端 DictPermissions 常量、前端 v-permission 三处须保持一致。
--        执行后需重新登录（或 F5 刷新）让 /sys/menu 重新下发权限。
-- ============================================================

-- ---------- 字典类型(902) 按钮 ----------
INSERT INTO sys_menu (id, menu_name, parent_id, menu_type, path, component, permission, visible, sort_order, create_time, update_time)
VALUES
  (905, '新增', 902, 3, '', '', 'dict:type:create', 0, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE menu_name = VALUES(menu_name), permission = VALUES(permission), update_time = NOW();

INSERT INTO sys_menu (id, menu_name, parent_id, menu_type, path, component, permission, visible, sort_order, create_time, update_time)
VALUES
  (906, '修改', 902, 3, '', '', 'dict:type:update', 0, 2, NOW(), NOW())
ON DUPLICATE KEY UPDATE menu_name = VALUES(menu_name), permission = VALUES(permission), update_time = NOW();

INSERT INTO sys_menu (id, menu_name, parent_id, menu_type, path, component, permission, visible, sort_order, create_time, update_time)
VALUES
  (907, '删除', 902, 3, '', '', 'dict:type:delete', 0, 3, NOW(), NOW())
ON DUPLICATE KEY UPDATE menu_name = VALUES(menu_name), permission = VALUES(permission), update_time = NOW();

-- ---------- 字典数据(903) 按钮 ----------
INSERT INTO sys_menu (id, menu_name, parent_id, menu_type, path, component, permission, visible, sort_order, create_time, update_time)
VALUES
  (908, '新增', 903, 3, '', '', 'dict:data:create', 0, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE menu_name = VALUES(menu_name), permission = VALUES(permission), update_time = NOW();

INSERT INTO sys_menu (id, menu_name, parent_id, menu_type, path, component, permission, visible, sort_order, create_time, update_time)
VALUES
  (909, '修改', 903, 3, '', '', 'dict:data:update', 0, 2, NOW(), NOW())
ON DUPLICATE KEY UPDATE menu_name = VALUES(menu_name), permission = VALUES(permission), update_time = NOW();

INSERT INTO sys_menu (id, menu_name, parent_id, menu_type, path, component, permission, visible, sort_order, create_time, update_time)
VALUES
  (910, '删除', 903, 3, '', '', 'dict:data:delete', 0, 3, NOW(), NOW())
ON DUPLICATE KEY UPDATE menu_name = VALUES(menu_name), permission = VALUES(permission), update_time = NOW();

-- ---------- 缓存维护(903) 按钮 ----------
INSERT INTO sys_menu (id, menu_name, parent_id, menu_type, path, component, permission, visible, sort_order, create_time, update_time)
VALUES
  (911, '刷新缓存', 903, 3, '', '', 'dict:cache:clear', 0, 4, NOW(), NOW())
ON DUPLICATE KEY UPDATE menu_name = VALUES(menu_name), permission = VALUES(permission), update_time = NOW();

SELECT 'dict_menu.sql 执行完成' AS done;
