-- =============================================================
-- 价格策略与会员余额模块 菜单种子（幂等版）
--   目标库：jingsuding_order
--
-- 说明：
--   若 sys_menu 中已存在价格管理 / 会员余额菜单（ID 170~186），本脚本会【自动跳过】，不会重复插入。
--   若菜单缺失（例如只在前端建了页面、菜单还没建），执行本脚本即可补齐，
--   执行后需 F5 刷新或重新登录，让前端重新拉取动态路由。
--
-- ⚠️ 前端路由关键：component 必须与 src/views 下的真实文件一一对应，
--    否则点击菜单提示"页面不存在"（前端用 import.meta.glob('@/views/**/*.vue') 解析）：
--      price/member-level/index      → src/views/price/member-level/index.vue
--      price/strategy/index          → src/views/price/strategy/index.vue
--      price/change-log/index        → src/views/price/change-log/index.vue
--      price/order-snapshot/index    → src/views/price/order-snapshot/index.vue
--      member/recharge/index         → src/views/member/recharge/index.vue
--      member/balance-log/index      → src/views/member/balance-log/index.vue
--
-- 权限标识与后端 Jsd.Api/Models/Price/PriceEnums.cs 的 PricePermissions 常量完全一致，
-- 前端按钮用 v-permission 绑定同一字符串（types/price.ts 的 PRICE_PERMISSIONS）。
--
-- ID 段：价格管理 170~176，会员余额 180~186
--   （已避开 1~6 / 103~128 / 130~144 采购 / 150~159 售后 / 200 / 300 / 600）
-- =============================================================

USE `jingsuding_order`;
SET FOREIGN_KEY_CHECKS = 0;

-- -------------------------------------------------------------
-- 一、一级目录：价格管理（path=/price，menu_type=1 目录，无组件）
-- -------------------------------------------------------------
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 170, 0, '价格管理', '/price', 'Layout', 'price', 80, 1, NULL, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 170));

-- ① 客户等级
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 171, 170, '客户等级', '/price/member-level', 'price/member-level/index', 'user', 1, 2, NULL, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 171));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  172, 171, '等级列表',   '', '', '', 1, 3, 'price:level:list',   1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 172));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  173, 171, '新增等级',   '', '', '', 2, 3, 'price:level:add',    1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 173));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  174, 171, '编辑等级',   '', '', '', 3, 3, 'price:level:edit',   1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 174));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  175, 171, '启用/停用',  '', '', '', 4, 3, 'price:level:status', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 175));

-- ② 价格策略（规则与阶梯明细在页面内维护，不单设菜单）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 176, 170, '价格策略', '/price/strategy', 'price/strategy/index', 'setting', 2, 2, NULL, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 176));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2001, 176, '策略列表',   '', '', '', 1, 3, 'price:strategy:list',    1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2001));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2002, 176, '新增策略',   '', '', '', 2, 3, 'price:strategy:add',     1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2002));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2003, 176, '编辑策略',   '', '', '', 3, 3, 'price:strategy:edit',    1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2003));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2004, 176, '提交启用',   '', '', '', 4, 3, 'price:strategy:submit',  1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2004));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2005, 176, '停用策略',   '', '', '', 5, 3, 'price:strategy:disable', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2005));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2006, 176, '删除策略',   '', '', '', 6, 3, 'price:strategy:delete',  1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2006));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2007, 176, '维护规则',   '', '', '', 7, 3, 'price:strategy:rule:save', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2007));

-- ③ 价格变更日志
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 177, 170, '价格变更日志', '/price/change-log', 'price/change-log/index', 'log', 3, 2, NULL, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 177));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2008, 177, '日志列表', '', '', '', 1, 3, 'price:change-log:list', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2008));

-- ④ 订单价格快照
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 178, 170, '订单价格快照', '/price/order-snapshot', 'price/order-snapshot/index', 'document', 4, 2, NULL, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 178));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2009, 178, '快照列表', '', '', '', 1, 3, 'price:snapshot:list', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2009));

-- -------------------------------------------------------------
-- 二、一级目录：会员余额（path=/member-balance，menu_type=1 目录）
-- -------------------------------------------------------------
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 180, 0, '会员余额', '/member-balance', 'Layout', 'wallet', 85, 1, NULL, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 180));

-- ⑤ 充值管理
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 181, 180, '充值管理', '/member-balance/recharge', 'member/recharge/index', 'money', 1, 2, NULL, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 181));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2010, 181, '充值列表',     '', '', '', 1, 3, 'member:recharge:list',   1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2010));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2011, 181, '发起充值',     '', '', '', 2, 3, 'member:recharge:create',  1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2011));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2012, 181, '后台代客充值', '', '', '', 3, 3, 'member:recharge:audit',   1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2012));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2013, 181, '充值退款',     '', '', '', 4, 3, 'member:recharge:refund',  1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2013));

-- ⑥ 余额流水
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT 182, 180, '余额流水', '/member-balance/balance-log', 'member/balance-log/index', 'list', 2, 2, NULL, 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 182));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2014, 182, '流水列表',     '', '', '', 1, 3, 'member:balance-log:list',   1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2014));

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
SELECT
  2015, 182, '后台调整余额', '', '', '', 2, 3, 'member:balance-log:adjust', 1, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `sys_menu` WHERE `id` = 2015));

SET FOREIGN_KEY_CHECKS = 1;

-- =============================================================
-- 校验（可选）
-- =============================================================
-- SELECT id, parent_id, menu_name, path, component, menu_type, permission
--   FROM sys_menu WHERE id IN (170,171,172,173,174,175,176,177,178,180,181,182,
--                              2001,2002,2003,2004,2005,2006,2007,2008,2009,
--                              2010,2011,2012,2013,2014,2015)
--   ORDER BY id;
