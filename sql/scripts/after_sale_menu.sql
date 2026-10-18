-- =============================================================
-- 售后管理模块 菜单种子
--    ID 段使用 150~164（已避开采购管理 130~144、现有 1~6 / 103~128 / 200 / 300 / 600 段）
--    ⚠️ 前端路由关键：component 必须与 src/views 下的真实文件一一对应，
--       否则点击菜单会提示"页面不存在"（本项目用 import.meta.glob('@/views/**/*.vue') 解析）。
--       前端目录命名是 aftersale（与后端 API 前缀 /after-sale 区分开，避免重蹈 purchase/procurement 覆辙）。
--    权限标识与后端 AfterSalePermissions 常量完全一致。
-- =============================================================

SET FOREIGN_KEY_CHECKS = 0;

-- 1. 顶级目录：售后管理
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (150, 0, '售后管理', '/after-sale', 'Layout', 'service', 70, 1, NULL, 1, NOW(), NOW());

-- 2. 退款申请（菜单）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (151, 150, '退款申请', '/after-sale/refund-apply', 'aftersale/refundApply/index', 'tickets', 1, 2, NULL, 1, NOW(), NOW());

-- 退款申请 - 按钮权限（与 AfterSalePermissions 常量一致）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES
(153, 151, '退款列表', '', '', '', 1, 3, 'aftersale:refund:list',    1, NOW(), NOW()),
(154, 151, '新增退款', '', '', '', 2, 3, 'aftersale:refund:create',  1, NOW(), NOW()),
(155, 151, '退款审核', '', '', '', 3, 3, 'aftersale:refund:approve', 1, NOW(), NOW()),
(156, 151, '执行退款', '', '', '', 4, 3, 'aftersale:refund:execute', 1, NOW(), NOW());

-- 3. 退货管理（菜单）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (152, 150, '退货管理', '/after-sale/return-manage', 'aftersale/returnManage/index', 'goods', 2, 2, NULL, 1, NOW(), NOW());

-- 退货管理 - 按钮权限
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES
(157, 152, '退货列表', '', '', '', 1, 3, 'aftersale:return:list',    1, NOW(), NOW()),
(158, 152, '创建退货', '', '', '', 2, 3, 'aftersale:return:create',  1, NOW(), NOW()),
(159, 152, '确认收货', '', '', '', 3, 3, 'aftersale:return:receive', 1, NOW(), NOW());

SET FOREIGN_KEY_CHECKS = 1;

-- =============================================================
-- 自检：确认菜单 component 与前端真实文件一一对应
-- 期望结果：
--   151 -> aftersale/refundApply/index  对应 src/views/aftersale/refundApply/index.vue
--   152 -> aftersale/returnManage/index 对应 src/views/aftersale/returnManage/index.vue
-- =============================================================
-- SELECT id, menu_name, component FROM sys_menu WHERE id IN (151, 152);
