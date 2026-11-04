-- ============================================================================
-- 阶段八 · 权限补全：给「运营专员（role_id=2）」补订单模块权限
--
-- 背景：阶段七给 OrderController 的 page/detail/remark/ship/export/cancel
--       加上了 [Permission(OrderPermissions.X)] 之后，role_id=2 原本只有
--       「物流查询 / 刷新物流」两个菜单，没有 order:list —— 客服登进后台点
--       「订单管理」会直接 403 空白。本脚本把订单模块授权补上。
--
-- 设计说明：
--   * 只授予「日常作业」所需权限：列表 / 详情 / 备注 / 发货 / 导出；
--   * 刻意【不授予】order:cancel（取消订单会触发库存回滚与资金处理，
--     属高风险动作，保留给超级管理员或财务角色）。需要时放开最后一段即可。
--   * 脚本幂等：重复执行不会产生重复行，也不会报主键冲突。
--
-- 执行：mysql -uroot -p jingsuding_order < grant_order_permissions_role2.sql
-- ============================================================================

-- ① 订单管理（父菜单，id=3）：没有父菜单，前端菜单树渲染不出「订单管理」节点
INSERT INTO sys_role_menu (role_id, menu_id, create_time)
SELECT 2, m.id, NOW()
FROM sys_menu m
WHERE m.id = 3
  AND NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = 2 AND rm.menu_id = m.id);

-- ② 订单模块的作业级权限（按权限码匹配，避免依赖硬编码 id）
INSERT INTO sys_role_menu (role_id, menu_id, create_time)
SELECT 2, m.id, NOW()
FROM sys_menu m
WHERE m.permission IN (
        'order:list',      -- 订单列表（本次核心诉求）
        'order:query',     -- 订单查询
        'order:detail',    -- 订单详情
        'order:remark',    -- 商家备注
        'order:ship',      -- 发货
        'order:export'     -- 导出
      )
  AND NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = 2 AND rm.menu_id = m.id);

-- ③ 【默认不执行】如需允许运营专员取消订单，去掉下面注释：
-- INSERT INTO sys_role_menu (role_id, menu_id, create_time)
-- SELECT 2, m.id, NOW()
-- FROM sys_menu m
-- WHERE m.permission = 'order:cancel'
--   AND NOT EXISTS (SELECT 1 FROM sys_role_menu rm WHERE rm.role_id = 2 AND rm.menu_id = m.id);

-- ④ 执行结果自检
SELECT rm.role_id, m.id, m.menu_name, m.permission
FROM sys_role_menu rm
JOIN sys_menu m ON m.id = rm.menu_id
WHERE rm.role_id = 2
ORDER BY m.id;
