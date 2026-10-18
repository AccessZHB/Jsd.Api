-- =============================================================
-- 镜速订产品订货系统 —— 采购管理模块 建表脚本
-- 数据库: MySQL 8.0 | 引擎: InnoDB | 字符集: utf8mb4 / utf8mb4_unicode_ci
-- 版本: V1.0  |  说明: 采购订单 / 以采定入 / 应付账款 五张表
-- 对应实体: Jsd.Api/Entities/PurOrder.cs, PurOrderItem.cs, PurInbound.cs,
--           PurInboundItem.cs, PurPayable.cs
-- 说明:   【与库存管理模块一致】本模块不建物理外键，外键约束由 Service 层事务保证；
--         删除保护（如供应商被删时保留历史单据）也在 Service 层实现。
-- =============================================================

USE `jingsuding_order`;

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- -------------------------------------------------------------
-- 1. pur_order 采购订单主表
--    状态 status: 0-草稿 1-审批中(历史兼容) 2-待入库 3-部分入库 4-已结清 9-作废
--    【无审核环节】0-草稿 --确认订单--> 2-待入库；不设审批人/审批时间字段
-- -------------------------------------------------------------
DROP TABLE IF EXISTS `pur_order`;
CREATE TABLE `pur_order` (
  `id`                    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `order_no`              VARCHAR(32)  NOT NULL                   COMMENT '采购单号(唯一, PO-yyyyMMdd-001)',
  `supplier_id`           BIGINT UNSIGNED NOT NULL               COMMENT '供应商ID, 关联 sup_supplier.id',
  `order_date`            DATE         NOT NULL                  COMMENT '下单日期',
  `expected_delivery_date` DATE         DEFAULT NULL             COMMENT '预计到货日期',
  `status`                TINYINT      NOT NULL DEFAULT 0         COMMENT '状态:0-草稿 1-审批中(历史兼容) 2-待入库 3-部分入库 4-已结清 9-作废',
  `total_amount`          DECIMAL(12,2) NOT NULL DEFAULT 0.00    COMMENT '订单总金额(=Σ明细小计)',
  `remark`                VARCHAR(500)         DEFAULT ''         COMMENT '备注',
  -- 本模块【无审核环节】：不设 approver_id / approve_time
  `create_time`           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_pur_order_no` (`order_no`),
  KEY `idx_pur_order_supplier` (`supplier_id`),
  KEY `idx_pur_order_status` (`status`),
  KEY `idx_pur_order_date` (`order_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='采购订单主表';

-- -------------------------------------------------------------
-- 2. pur_order_item 采购订单明细表
--    material_id 实际关联 prod_sku.id（本系统库存以 SKU 为最小单位）
-- -------------------------------------------------------------
DROP TABLE IF EXISTS `pur_order_item`;
CREATE TABLE `pur_order_item` (
  `id`          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `order_id`    BIGINT UNSIGNED NOT NULL               COMMENT '采购订单ID, 关联 pur_order.id',
  `material_id` BIGINT UNSIGNED NOT NULL               COMMENT '物料/SKU ID, 关联 prod_sku.id',
  `quantity`    DECIMAL(10,2) NOT NULL DEFAULT 0.00    COMMENT '采购数量',
  `unit_price`  DECIMAL(10,2) NOT NULL DEFAULT 0.00    COMMENT '采购单价(元)',
  `subtotal`    DECIMAL(12,2) NOT NULL DEFAULT 0.00    COMMENT '行小计(数量×单价, 服务端重算)',
  `remark`      VARCHAR(255)         DEFAULT ''         COMMENT '备注',
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_pur_order_item_order` (`order_id`),
  KEY `idx_pur_order_item_material` (`material_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='采购订单明细表';

-- -------------------------------------------------------------
-- 3. pur_inbound 采购入库单主表（以采定入）
--    status: 0-待确认 1-已入库
--    warehouse_id 当前无仓库主数据表，仅作引用存储
-- -------------------------------------------------------------
DROP TABLE IF EXISTS `pur_inbound`;
CREATE TABLE `pur_inbound` (
  `id`           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `inbound_no`   VARCHAR(32)  NOT NULL                   COMMENT '入库单号(唯一, RK-yyyyMMdd-001)',
  `order_id`     BIGINT UNSIGNED NOT NULL               COMMENT '采购订单ID, 关联 pur_order.id',
  `warehouse_id` BIGINT UNSIGNED NOT NULL               COMMENT '仓库ID(引用, 当前无仓库主数据表)',
  `inbound_date` DATE         NOT NULL                  COMMENT '入库日期',
  `operator_id`  BIGINT UNSIGNED DEFAULT NULL           COMMENT '入库操作员ID',
  `status`       TINYINT      NOT NULL DEFAULT 0         COMMENT '状态:0-待确认 1-已入库',
  `remark`       VARCHAR(500)         DEFAULT ''         COMMENT '备注',
  `create_time`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_pur_inbound_no` (`inbound_no`),
  KEY `idx_pur_inbound_order` (`order_id`),
  KEY `idx_pur_inbound_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='采购入库单主表';

-- -------------------------------------------------------------
-- 4. pur_inbound_item 采购入库明细表
--    order_item_id 精确关联采购订单明细行，实现分批入库追踪
-- -------------------------------------------------------------
DROP TABLE IF EXISTS `pur_inbound_item`;
CREATE TABLE `pur_inbound_item` (
  `id`           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `inbound_id`   BIGINT UNSIGNED NOT NULL               COMMENT '入库单ID, 关联 pur_inbound.id',
  `order_item_id` BIGINT UNSIGNED NOT NULL              COMMENT '采购订单明细ID, 关联 pur_order_item.id',
  `material_id`  BIGINT UNSIGNED NOT NULL               COMMENT '物料/SKU ID(冗余, 关联 prod_sku.id)',
  `actual_quantity` DECIMAL(10,2) NOT NULL DEFAULT 0.00 COMMENT '实收数量',
  `batch_no`     VARCHAR(32)           DEFAULT NULL     COMMENT '入库批次号',
  `remark`       VARCHAR(255)          DEFAULT ''        COMMENT '备注',
  `create_time`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_pur_inbound_item_inbound` (`inbound_id`),
  KEY `idx_pur_inbound_item_order_item` (`order_item_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='采购入库明细表';

-- -------------------------------------------------------------
-- 5. pur_payable 应付账款表（入库确认自动生成）
--    status: 0-未付 1-部分付款 2-已结清
-- -------------------------------------------------------------
DROP TABLE IF EXISTS `pur_payable`;
CREATE TABLE `pur_payable` (
  `id`            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `payable_no`    VARCHAR(32)  NOT NULL                   COMMENT '应付单号(唯一, AP-yyyyMMdd-001)',
  `supplier_id`   BIGINT UNSIGNED NOT NULL               COMMENT '供应商ID, 关联 sup_supplier.id',
  `related_order_id` BIGINT UNSIGNED DEFAULT NULL        COMMENT '关联采购订单ID, 关联 pur_order.id',
  `total_amount`  DECIMAL(12,2) NOT NULL DEFAULT 0.00    COMMENT '应付总额(实收数量×采购单价之和)',
  `paid_amount`   DECIMAL(12,2) NOT NULL DEFAULT 0.00    COMMENT '已付金额(付款核销时累加)',
  `unpaid_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00    COMMENT '未付金额(total_amount - paid_amount)',
  `due_date`      DATE         DEFAULT NULL              COMMENT '到期付款日',
  `status`        TINYINT      NOT NULL DEFAULT 0         COMMENT '状态:0-未付 1-部分付款 2-已结清',
  `remark`        VARCHAR(500)         DEFAULT ''         COMMENT '备注',
  `create_time`   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_pur_payable_no` (`payable_no`),
  KEY `idx_pur_payable_supplier` (`supplier_id`),
  KEY `idx_pur_payable_order` (`related_order_id`),
  KEY `idx_pur_payable_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='应付账款表';

-- =============================================================
-- 6. 采购管理模块 菜单种子
--    ID 段使用 130~145（已避开现有 1~6 / 103~128 / 200 / 300 / 600 段）
--    ⚠️ 前端路由关键：component 必须与 src/views 下的真实文件一一对应，
--       否则点击菜单会提示"页面不存在"（本项目用 import.meta.glob('@/views/**/*.vue') 解析）。
-- =============================================================

-- 6.1 顶级目录：采购管理
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (130, 0, '采购管理', '/procurement', 'Layout', 'shopping-cart', 60, 1, NULL, 1, NOW(), NOW());

-- 6.2 采购订单（菜单）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (131, 130, '采购订单', '/procurement/order', 'procurement/order/index', 'document', 1, 2, NULL, 1, NOW(), NOW());

-- 采购订单 - 按钮权限（与 PurchasePermissions 常量一致）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES
(132, 131, '订单列表',   '', '', '', 1, 3, 'purchase:order:list',        1, NOW(), NOW()),
(133, 131, '新增订单',   '', '', '', 2, 3, 'purchase:order:create',      1, NOW(), NOW()),
(134, 131, '编辑订单',   '', '', '', 3, 3, 'purchase:order:update',      1, NOW(), NOW()),
(135, 131, '确认订单',   '', '', '', 4, 3, 'purchase:order:confirm',     1, NOW(), NOW()),
(136, 131, '作废订单',   '', '', '', 5, 3, 'purchase:order:cancel',      1, NOW(), NOW()),
(137, 131, '预警生成',   '', '', '', 6, 3, 'purchase:order:auto-create', 1, NOW(), NOW());

-- 6.3 采购入库（菜单）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (138, 130, '采购入库', '/procurement/inbound', 'procurement/inbound/index', 'box', 2, 2, NULL, 1, NOW(), NOW());

-- 采购入库 - 按钮权限
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES
(139, 138, '入库单列表', '', '', '', 1, 3, 'purchase:inbound:list',    1, NOW(), NOW()),
(140, 138, '新增入库',   '', '', '', 2, 3, 'purchase:inbound:create',  1, NOW(), NOW()),
(141, 138, '确认入库',   '', '', '', 3, 3, 'purchase:inbound:confirm', 1, NOW(), NOW());

-- 6.4 应付账款（菜单）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (142, 130, '应付账款', '/procurement/payable', 'procurement/payable/index', 'money', 3, 2, NULL, 1, NOW(), NOW());

-- 应付账款 - 按钮权限
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES
(143, 142, '应付列表', '', '', '', 1, 3, 'purchase:payable:list', 1, NOW(), NOW()),
(144, 142, '付款核销', '', '', '', 2, 3, 'purchase:payable:pay',  1, NOW(), NOW());

SET FOREIGN_KEY_CHECKS = 1;
