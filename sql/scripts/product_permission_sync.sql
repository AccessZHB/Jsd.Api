-- ============================================================
-- 商品 / SKU / 规格 / 供应商 权限同步（幂等，可重复执行）
--
-- 现状：sys_menu 里 10211~10214、10217~10220 这些按钮行早就存在，
--       但 permission 字段是 NULL —— 前端 v-permission 拿不到值，后端也无法授权。
--       本次直接把已有行的 permission 补上（UPDATE），不新建重复菜单。
--
-- ID 规划：
--   已有行（仅 UPDATE）：
--     10210 商品列表   → prod:info:list
--     10211 商品查询   → prod:info:query
--     10212 商品新增   → prod:info:add
--     10213 商品修改   → prod:info:edit
--     10214 商品删除   → prod:info:remove
--     10216 供应商管理 → sup:supplier:list
--     10217 供应商查询 → sup:supplier:query
--     10218 供应商新增 → sup:supplier:add
--     10219 供应商修改 → sup:supplier:edit
--     10220 供应商删除 → sup:supplier:remove
--   新增行（INSERT ... ON DUPLICATE KEY UPDATE）：
--     60201 商品上下架、60202~60206 SKU、60207~60210 规格
-- ============================================================

-- ---------- ① 复用已有按钮行，补齐 permission ----------
UPDATE sys_menu SET permission='prod:info:list',   update_time=NOW() WHERE id=10210;
UPDATE sys_menu SET permission='prod:info:query',  update_time=NOW() WHERE id=10211;
UPDATE sys_menu SET permission='prod:info:add',    update_time=NOW() WHERE id=10212;
UPDATE sys_menu SET permission='prod:info:edit',   update_time=NOW() WHERE id=10213;
UPDATE sys_menu SET permission='prod:info:remove', update_time=NOW() WHERE id=10214;

UPDATE sys_menu SET permission='sup:supplier:list',   update_time=NOW() WHERE id=10216;
UPDATE sys_menu SET permission='sup:supplier:query',  update_time=NOW() WHERE id=10217;
UPDATE sys_menu SET permission='sup:supplier:add',    update_time=NOW() WHERE id=10218;
UPDATE sys_menu SET permission='sup:supplier:edit',   update_time=NOW() WHERE id=10219;
UPDATE sys_menu SET permission='sup:supplier:remove', update_time=NOW() WHERE id=10220;

-- ---------- ② 商品上下架 ----------
INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60201, 10210, '商品上下架', 3, 'prod:info:status', '', '', '', 5, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

-- ---------- ③ SKU 五个动作（父：商品列表 10210）----------
INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60202, 10210, 'SKU列表', 3, 'prod:sku:list', '', '', '', 6, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60203, 10210, 'SKU查询', 3, 'prod:sku:query', '', '', '', 7, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60204, 10210, 'SKU新增', 3, 'prod:sku:add', '', '', '', 8, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60205, 10210, 'SKU修改', 3, 'prod:sku:edit', '', '', '', 9, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60206, 10210, 'SKU删除', 3, 'prod:sku:remove', '', '', '', 10, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

-- ---------- ④ 规格四个动作（父子项共用，父：商品列表 10210）----------
INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60207, 10210, '规格列表', 3, 'prod:spec:list', '', '', '', 11, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60208, 10210, '规格新增', 3, 'prod:spec:add', '', '', '', 12, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60209, 10210, '规格修改', 3, 'prod:spec:edit', '', '', '', 13, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60210, 10210, '规格删除', 3, 'prod:spec:remove', '', '', '', 14, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE parent_id=VALUES(parent_id), menu_name=VALUES(menu_name),
    permission=VALUES(permission), update_time=NOW();

-- ---------- ⑤ 授权：镜像「商品管理」父菜单(2) 的现有角色 ----------
INSERT INTO sys_role_menu (role_id, menu_id)
SELECT DISTINCT rm.role_id, m.id
FROM sys_role_menu rm
JOIN sys_menu m ON m.permission IN (
        'prod:info:list','prod:info:query','prod:info:add','prod:info:edit',
        'prod:info:status','prod:info:remove',
        'prod:sku:list','prod:sku:query','prod:sku:add','prod:sku:edit','prod:sku:remove',
        'prod:spec:list','prod:spec:add','prod:spec:edit','prod:spec:remove')
WHERE rm.menu_id = 2
  AND NOT EXISTS (SELECT 1 FROM sys_role_menu x WHERE x.role_id = rm.role_id AND x.menu_id = m.id);

-- ---------- ⑥ 自检：应为 20 行，distinct 也为 20 ----------
SELECT COUNT(*) AS total_rows, COUNT(DISTINCT permission) AS distinct_permissions
FROM sys_menu
WHERE permission LIKE 'prod:info:%' OR permission LIKE 'prod:sku:%'
   OR permission LIKE 'prod:spec:%' OR permission LIKE 'sup:supplier:%';
