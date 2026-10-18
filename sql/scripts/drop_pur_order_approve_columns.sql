-- =============================================================
-- 删除 pur_order 的审批字段（本系统采购【无审核环节】）
--   目标列：approver_id（审批人ID）、approve_time（审批时间）
--
-- 配套的代码改动（已同步完成）：
--   后端：PurOrder 实体移除两属性；PurchaseOrderDetailDto 移除两字段；
--         审批接口 POST /api/purchase/order/{id}/approve
--         → 改为「确认订单」POST /api/purchase/order/{id}/confirm（草稿 0 → 待入库 2）；
--         权限 purchase:order:approve → purchase:order:confirm。
--   前端：按钮"提交审批"→"确认订单"；状态 2 文案"审批通过"→"待入库"；详情不再展示审批时间。
-- =============================================================

USE `jingsuding_order`;

-- ---------- 0. 先看两列是否存在 ----------
SELECT `COLUMN_NAME`, `COLUMN_TYPE`
FROM `information_schema`.`COLUMNS`
WHERE `TABLE_SCHEMA` = DATABASE()
  AND `TABLE_NAME` = 'pur_order'
  AND `COLUMN_NAME` IN ('approver_id', 'approve_time');

-- ---------- 1. 删除两列（核心）----------
-- 说明：若上一步查不到这两列（说明本来就没有），本语句会报
--       ERROR 1091 (42000): Can't DROP 'xxx'; check that column/key exists
--       这属于"本来就不存在、无需删除"，可安全忽略，不会影响数据。
ALTER TABLE `pur_order`
  DROP COLUMN `approve_time`,
  DROP COLUMN `approver_id`;

-- ---------- 2. 验证：应返回 0 行 ----------
SELECT `COLUMN_NAME`
FROM `information_schema`.`COLUMNS`
WHERE `TABLE_SCHEMA` = DATABASE()
  AND `TABLE_NAME` = 'pur_order'
  AND `COLUMN_NAME` IN ('approver_id', 'approve_time');

-- ---------- 3. 最终确认 pur_order 结构 ----------
SELECT `COLUMN_NAME`, `COLUMN_TYPE`, `IS_NULLABLE`, `COLUMN_COMMENT`
FROM `information_schema`.`COLUMNS`
WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'pur_order'
ORDER BY `ORDINAL_POSITION`;

-- =============================================================
-- 4. 菜单权限同步（若你库里的采购菜单已按旧权限录入，需要一并改）
--    旧：'purchase:order:approve'（菜单名"提交审批"）
--    新：'purchase:order:confirm'（菜单名"确认订单"）
-- =============================================================
UPDATE `sys_menu`
SET `permission` = 'purchase:order:confirm', `menu_name` = '确认订单'
WHERE `permission` = 'purchase:order:approve';

-- 核对采购订单菜单下的按钮权限
SELECT `id`, `menu_name`, `permission`
FROM `sys_menu`
WHERE `parent_id` = (SELECT `id` FROM (SELECT `id` FROM `sys_menu` WHERE `menu_name` = '采购订单' AND `menu_type` = 2 LIMIT 1) t)
ORDER BY `sort_order`;

-- ⚠️ 执行顺序建议：先跑本脚本，再重启/刷新后台与前端。
--    前端菜单权限来自后端 sys_menu，改完需 F5 刷新或重新登录才会生效。
