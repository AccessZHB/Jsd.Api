-- =============================================================
-- 发票与税务管理 + 微信支付 T+1 对账 菜单种子（幂等版）
--   目标库：jingsuding_order
--
-- 说明：
--   挂在财务结算父菜单（id=320）下，新增：
--     · 发票管理（325，component=finance/invoice/index）
--     · 对账管理（331，component=finance/reconciliation/index）
--   并补齐页面用到的全部按钮权限，否则前端 v-permission 会因菜单树里
--   没有该权限标识而把按钮隐藏（超级管理员除外）。
--
-- ⚠️ 前端路由关键：component 必须与 src/views 下的真实文件一一对应，否则点击菜单白屏：
--      finance/invoice/index         → src/views/finance/invoice/index.vue
--      finance/reconciliation/index  → src/views/finance/reconciliation/index.vue
--
-- ⚠️ 执行后需 F5 刷新或重新登录，让前端重新拉取动态路由。
--    权限标识与后端 InvoicePermissions / ReconciliationPermissions 常量完全一致。
--
-- 权限标识清单：
--   发票：finance:invoice:list / apply / issue / cancel / info:list / info:save
--   对账：finance:reconciliation:list / download / run / handle
-- =============================================================

USE `jingsuding_order`;
SET FOREIGN_KEY_CHECKS = 0;

-- -------------------------------------------------------------
-- 一、发票管理（菜单，父 320，path=invoice）
-- -------------------------------------------------------------
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 325, 320, '发票管理', 'invoice', 'finance/invoice/index', 'document', 3, 2, 'finance:invoice:list', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 325);

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 326, 325, '提交开票申请', '', '', '', 1, 3, 'finance:invoice:apply', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 326);

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 327, 325, '财务开票', '', '', '', 2, 3, 'finance:invoice:issue', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 327);

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 328, 325, '作废/红冲', '', '', '', 3, 3, 'finance:invoice:cancel', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 328);

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 329, 325, '客户抬头', '', '', '', 4, 3, 'finance:invoice:info:list', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 329);

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 330, 325, '维护抬头', '', '', '', 5, 3, 'finance:invoice:info:save', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 330);

-- -------------------------------------------------------------
-- 二、对账管理（菜单，父 320，path=reconciliation）
-- -------------------------------------------------------------
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 331, 320, '对账管理', 'reconciliation', 'finance/reconciliation/index', 'money', 4, 2, 'finance:reconciliation:list', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 331);

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 332, 331, '下载账单', '', '', '', 1, 3, 'finance:reconciliation:download', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 332);

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 333, 331, '立即对账', '', '', '', 2, 3, 'finance:reconciliation:run', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 333);

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 334, 331, '差异处理', '', '', '', 3, 3, 'finance:reconciliation:handle', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 334);

SET FOREIGN_KEY_CHECKS = 1;

-- =============================================================
-- 校验（可选）
-- =============================================================
-- SELECT id, parent_id, menu_name, path, component, menu_type, permission
--   FROM sys_menu WHERE id IN (325,326,327,328,329,330,331,332,333,334)
--   ORDER BY id;
