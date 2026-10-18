-- ============================================================
-- 修复售后管理二级菜单（sys_menu parent_id = 501）
--
-- 问题：component 写成了 after-sale/... ，但前端真实文件在 src/views/aftersale/... 下，
--       点击菜单会报"页面不存在"；且"售后日志"根本没有独立页面（日志在详情抽屉内展示）。
--
-- 执行后必须 F5 刷新或重新登录，动态路由才会按新 component 重新注册。
-- ============================================================

USE `jingsuding_order`;

-- 1) 修正 component（必须与 src/views 真实文件一一对应，区分大小写/驼峰）
UPDATE `sys_menu` SET `component` = 'aftersale/refundApply/index'  WHERE `id` = 502;
UPDATE `sys_menu` SET `component` = 'aftersale/returnManage/index' WHERE `id` = 503;

-- 2) "售后日志"没有独立页面：日志时间轴嵌在 退款详情/退货详情 抽屉里
--    （AfterSaleLogTimeline 组件，接口 /after-sale/log/by-refund|by-return）。
--    该菜单无论 component 填什么都会 404，直接删除：
DELETE FROM `sys_menu` WHERE `id` = 504;
--    若不想删，也可改为隐藏（侧边栏不显示，但路由仍会注册，无副作用）：
-- UPDATE `sys_menu` SET `visible` = 0 WHERE `id` = 504;

-- 3) 权限标识对齐后端 AfterSalePermissions 常量（aftersale，无中划线）
--    后端常量：aftersale:refund:list / aftersale:return:list
--    若库里写成了 after-sale:refund:list（带中划线），按钮权限 v-permission 会匹配失败
UPDATE `sys_menu` SET `permission` = 'aftersale:refund:list'  WHERE `id` = 502 AND `permission` <> 'aftersale:refund:list';
UPDATE `sys_menu` SET `permission` = 'aftersale:return:list' WHERE `id` = 503 AND `permission` <> 'aftersale:return:list';

-- 4) 自检一：父目录 501 必须是"目录"，且 path=/after-sale、component=Layout
--    期望：menu_type=1，path='/after-sale'，component='Layout'
SELECT id, parent_id, menu_name, path, component, menu_type, visible, status
FROM `sys_menu` WHERE `id` = 501;

-- 5) 自检二：两个子菜单 component 与真实文件对应关系
--    502 -> aftersale/refundApply/index   => src/views/aftersale/refundApply/index.vue
--    503 -> aftersale/returnManage/index  => src/views/aftersale/returnManage/index.vue
SELECT id, menu_name, path, component, permission
FROM `sys_menu` WHERE `id` IN (502, 503);

-- 6) 自检三：如果你还建了 menu_type=3 的按钮权限行，确认标识与后端常量一致
--    退款：aftersale:refund:list / create / approve / execute
--    退货：aftersale:return:list / create / receive
SELECT id, parent_id, menu_name, permission, menu_type
FROM `sys_menu`
WHERE `permission` LIKE 'after-sale:%' OR `permission` LIKE 'aftersale:%'
ORDER BY `id`;
