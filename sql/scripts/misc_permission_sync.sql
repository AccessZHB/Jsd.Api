-- ============================================================
-- 最后一批（内容/字典/上传/日志）权限同步 —— 幂等
--
-- 现状：
--   banner:*（1507~1510）、announcement:*（1502~1505）、dict:*（902~910）
--   这些行早已存在，本次只在控制器挂载，无需重复插入。
--   真正需要新增的只有 3 个动作：
--     banner:status        轮播启用/禁用（parent 1506）
--     announcement:status  公告发布状态/置顶（parent 1501）
--     file:upload          文件上传（parent 1500 内容管理）
-- ============================================================

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60220, 1506, '轮播启用禁用', 3, 'banner:status', '', '', '', 5, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60221, 1501, '公告发布状态', 3, 'announcement:status', '', '', '', 5, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60222, 1500, '文件上传', 3, 'file:upload', '', '', '', 9, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

-- ---------- 授权 ----------
-- ① banner:status / announcement:status：镜像同模块 list 权限的角色
INSERT INTO sys_role_menu (role_id, menu_id)
SELECT DISTINCT rm.role_id, m.id
FROM sys_role_menu rm
JOIN sys_menu m ON m.permission = 'banner:status'
WHERE rm.menu_id IN (SELECT id FROM sys_menu WHERE permission = 'banner:list')
  AND NOT EXISTS (SELECT 1 FROM sys_role_menu x WHERE x.role_id = rm.role_id AND x.menu_id = m.id);

INSERT INTO sys_role_menu (role_id, menu_id)
SELECT DISTINCT rm.role_id, m.id
FROM sys_role_menu rm
JOIN sys_menu m ON m.permission = 'announcement:status'
WHERE rm.menu_id IN (SELECT id FROM sys_menu WHERE permission = 'announcement:list')
  AND NOT EXISTS (SELECT 1 FROM sys_role_menu x WHERE x.role_id = rm.role_id AND x.menu_id = m.id);

-- ② file:upload：授予所有已有菜单授权的后台角色
--    （上传本身只做图片白名单+大小校验，不含业务语义；
--      按"所有后台角色"授予可保持现有行为，同时保留按角色收回的能力）
INSERT INTO sys_role_menu (role_id, menu_id)
SELECT DISTINCT rm.role_id, (SELECT id FROM sys_menu WHERE permission = 'file:upload' LIMIT 1)
FROM sys_role_menu rm
WHERE NOT EXISTS (
    SELECT 1 FROM sys_role_menu x
    WHERE x.role_id = rm.role_id
      AND x.menu_id = (SELECT id FROM sys_menu WHERE permission = 'file:upload' LIMIT 1));

-- ---------- ④ 商品分类：复用已有按钮行（permission 原为 NULL）----------
UPDATE sys_menu SET permission='prod:category:list',   update_time=NOW() WHERE id=10205;
UPDATE sys_menu SET permission='prod:category:list',   update_time=NOW() WHERE id=10206;
UPDATE sys_menu SET permission='prod:category:add',    update_time=NOW() WHERE id=10207;
UPDATE sys_menu SET permission='prod:category:edit',   update_time=NOW() WHERE id=10208;
UPDATE sys_menu SET permission='prod:category:remove', update_time=NOW() WHERE id=10209;

-- ---------- 自检 ----------
SELECT id, parent_id, menu_name, permission FROM sys_menu
WHERE permission IN ('banner:status', 'announcement:status', 'file:upload')
ORDER BY permission;

SELECT id, parent_id, menu_name, permission FROM sys_menu
WHERE permission LIKE 'prod:category:%'
ORDER BY id;
