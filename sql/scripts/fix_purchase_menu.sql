-- =============================================================
-- 修复：点击"采购管理"二级菜单提示"页面不存在"
--
-- 原因：
--   sys_menu.component 存的是 purchase/order/index（等），
--   前端页面文件实际在 src/views/procurement/order/index.vue（等）。
--   前端动态路由按 src/views/{component}.vue 查找文件，找不到就不注册路由，
--   于是点击菜单落入 404"页面不存在"。
--
-- 修复方式：把 component 改成与前端真实文件一致（procurement/...）。
--   （id 请按你库里的实际值，截图为 402/403/404）
-- =============================================================

USE `jingsuding_order`;

-- 1. 必改：component 与前端文件对齐（关键修复）
UPDATE `sys_menu` SET `component` = 'procurement/order/index'   WHERE `id` = 402;
UPDATE `sys_menu` SET `component` = 'procurement/inbound/index' WHERE `id` = 403;
UPDATE `sys_menu` SET `component` = 'procurement/payable/index' WHERE `id` = 404;

-- 2. 可选：path 规范化为 order/inbound/payable，
--    避免出现 /purchase/purchase-order 这种重复前缀的地址（不影响功能）
UPDATE `sys_menu` SET `path` = 'order'   WHERE `id` = 402 AND `path` = 'purchase-order';
UPDATE `sys_menu` SET `path` = 'inbound' WHERE `id` = 403 AND `path` = 'purchase-inbound';
UPDATE `sys_menu` SET `path` = 'payable' WHERE `id` = 404 AND `path` = 'purchase-payable';

-- 3. 自检：确认修改结果（component 必须与 src/views 下的文件一一对应）
--    procurement/order/index   -> src/views/procurement/order/index.vue
--    procurement/inbound/index -> src/views/procurement/inbound/index.vue
--    procurement/payable/index -> src/views/procurement/payable/index.vue
SELECT `id`, `menu_name`, `path`, `component`
FROM `sys_menu`
WHERE `parent_id` = 401
ORDER BY `sort_order`;

-- ⚠️ 执行完 SQL 后，浏览器必须刷新（F5）或重新登录：
--    菜单与动态路由只在"登录后 / 页面刷新时"拉取注册（router/index.ts 的 addDynamicRoutes），
--    不刷新的话旧路由表不会更新。
