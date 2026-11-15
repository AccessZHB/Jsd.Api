-- ============================================================
-- 全量系统权限同步脚本（强幂等版）
--
-- 幂等策略：固定主键 + ON DUPLICATE KEY UPDATE
--   ● 首次执行：正常插入
--   ● 重复执行：命中主键冲突 → 只刷新名称/权限码/父级/更新时间，绝不产生重复行
--   ● 主键被占用但权限码不同：会被纠正为脚本中的正确值（自愈）
--
-- 为什么不用 INSERT ... SELECT ... WHERE NOT EXISTS：
--   那种写法只能防「权限码重复」，防不住「主键被别的权限占用」，
--   后一种情况会抛 1062 Duplicate entry，脚本中断。
--
-- 本次覆盖 10 个权限码（5 个新增 + 5 个幂等重申）：
--   stock:detail / order:create / order:pay / order:complete /
--   purchase:order:auto-create /
--   system:security:update / system:operation:remove /
--   system:loginlog:remove / dict:cache:refresh / customer:reset
-- ============================================================

-- ---------- ① 库存明细（父：库存查询 126）----------
INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60110, 126, '库存明细', 3, 'stock:detail', '', '', '', 1, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE
    parent_id  = VALUES(parent_id),
    menu_name  = VALUES(menu_name),
    permission = VALUES(permission),
    update_time = NOW();

-- ---------- ② 订单写操作（父：订单管理 3）----------
INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60111, 3, '创建订单', 3, 'order:create', '', '', '', 1, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE
    parent_id  = VALUES(parent_id),
    menu_name  = VALUES(menu_name),
    permission = VALUES(permission),
    update_time = NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60112, 3, '订单支付', 3, 'order:pay', '', '', '', 2, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE
    parent_id  = VALUES(parent_id),
    menu_name  = VALUES(menu_name),
    permission = VALUES(permission),
    update_time = NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60113, 3, '订单完成', 3, 'order:complete', '', '', '', 3, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE
    parent_id  = VALUES(parent_id),
    menu_name  = VALUES(menu_name),
    permission = VALUES(permission),
    update_time = NOW();

-- ---------- ③ 采购单自动创建（父：采购订单 402）----------
INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60114, 402, '自动创建采购单', 3, 'purchase:order:auto-create', '', '', '', 9, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE
    parent_id  = VALUES(parent_id),
    menu_name  = VALUES(menu_name),
    permission = VALUES(permission),
    update_time = NOW();

-- ---------- ④ 系统安全 / 日志 / 字典缓存 / 客户重置 ----------
INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60101, 804, '修改安全策略', 3, 'system:security:update', '', '', '', 1, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE
    parent_id  = VALUES(parent_id),
    menu_name  = VALUES(menu_name),
    permission = VALUES(permission),
    update_time = NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60102, 802, '删除日志', 3, 'system:operation:remove', '', '', '', 2, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE
    parent_id  = VALUES(parent_id),
    menu_name  = VALUES(menu_name),
    permission = VALUES(permission),
    update_time = NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60103, 803, '删除日志', 3, 'system:loginlog:remove', '', '', '', 2, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE
    parent_id  = VALUES(parent_id),
    menu_name  = VALUES(menu_name),
    permission = VALUES(permission),
    update_time = NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60104, 903, '刷新缓存', 3, 'dict:cache:refresh', '', '', '', 2, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE
    parent_id  = VALUES(parent_id),
    menu_name  = VALUES(menu_name),
    permission = VALUES(permission),
    update_time = NOW();

INSERT INTO sys_menu (id, parent_id, menu_name, menu_type, permission, path, component, icon, sort_order, visible, status, create_time, update_time)
VALUES (60004, 600, '重置密码', 3, 'customer:reset', '', '', '', 5, 1, 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE
    parent_id  = VALUES(parent_id),
    menu_name  = VALUES(menu_name),
    permission = VALUES(permission),
    update_time = NOW();

-- ---------- ⑤ 自检：以下必须恰好 10 行，且无重复 permission ----------
SELECT COUNT(*) AS total_rows, COUNT(DISTINCT permission) AS distinct_permissions
FROM sys_menu
WHERE permission IN (
    'stock:detail',
    'order:create', 'order:pay', 'order:complete',
    'purchase:order:auto-create',
    'system:security:update', 'system:operation:remove',
    'system:loginlog:remove', 'dict:cache:refresh', 'customer:reset');
