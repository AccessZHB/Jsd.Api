-- =============================================================
-- 镜速订产品订货系统 —— 系统管理模块(后台用户权限) 建库建表脚本
-- 数据库: MySQL 8.0 | 引擎: InnoDB | 字符集: utf8mb4 / utf8mb4_unicode_ci
-- 版本: V1.3  |  说明: 后台管理系统 RBAC 权限四张表 + 初始化数据
-- 对应文档: 镜速订订货系统数据库设计文档V1.3.docx (3.6 运营管理模块)
-- =============================================================

-- 1. 创建数据库
CREATE DATABASE IF NOT EXISTS `jingsuding_order`
    DEFAULT CHARACTER SET utf8mb4
    DEFAULT COLLATE utf8mb4_unicode_ci;

USE `jingsuding_order`;

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- -------------------------------------------------------------
-- 2. sys_role 管理员角色表  (先建, 供 sys_user / sys_role_menu 引用)
-- -------------------------------------------------------------
DROP TABLE IF EXISTS `sys_role`;
CREATE TABLE `sys_role` (
  `id`          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `role_name`   VARCHAR(50)  NOT NULL                   COMMENT '角色名称(如超级管理员/运营/客服/仓库)',
  `role_code`   VARCHAR(30)  NOT NULL                   COMMENT '角色编码(唯一, 如 admin/operation/warehouse)',
  `data_scope`  TINYINT      NOT NULL DEFAULT 1         COMMENT '数据权限:1-全部 2-本部门 3-仅本人',
  `status`      TINYINT      NOT NULL DEFAULT 1         COMMENT '状态:1-启用 0-停用',
  `remark`      VARCHAR(255)          DEFAULT ''         COMMENT '备注',
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_role_code` (`role_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='后台角色表';

-- -------------------------------------------------------------
-- 3. sys_menu 后台菜单表  (树形: 目录/菜单/按钮)
-- -------------------------------------------------------------
DROP TABLE IF EXISTS `sys_menu`;
CREATE TABLE `sys_menu` (
  `id`          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `parent_id`   BIGINT UNSIGNED NOT NULL DEFAULT 0      COMMENT '父级菜单ID(0=顶级)',
  `menu_name`   VARCHAR(50)  NOT NULL                   COMMENT '菜单名称(如:订单管理/商品列表)',
  `path`        VARCHAR(100)          DEFAULT NULL      COMMENT '前端路由路径(如:/order/list)',
  `component`   VARCHAR(100)          DEFAULT NULL      COMMENT '前端组件路径',
  `icon`        VARCHAR(50)           DEFAULT NULL      COMMENT '菜单图标',
  `sort_order`  INT                   DEFAULT 0         COMMENT '排序号(越小越靠前)',
  `menu_type`   TINYINT      NOT NULL DEFAULT 1         COMMENT '类型:1-目录 2-菜单 3-按钮',
  `permission`  VARCHAR(100)          DEFAULT NULL      COMMENT '权限标识(如 order:view/order:ship)',
  `visible`     TINYINT      NOT NULL DEFAULT 1         COMMENT '是否显示:1-显示 0-隐藏',
  `status`      TINYINT      NOT NULL DEFAULT 1         COMMENT '状态:1-正常 0-停用',
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_parent_id` (`parent_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='后台菜单表';

-- -------------------------------------------------------------
-- 4. sys_user 后台管理员用户表  (与小程序 member 表物理隔离, 严禁混用)
-- -------------------------------------------------------------
DROP TABLE IF EXISTS `sys_user`;
CREATE TABLE `sys_user` (
  `id`              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `username`        VARCHAR(50)  NOT NULL                   COMMENT '登录账号(唯一)',
  `password`        VARCHAR(128) NOT NULL                   COMMENT '登录密码(BCrypt加密存储)',
  `real_name`       VARCHAR(50)           DEFAULT ''         COMMENT '真实姓名',
  `phone`           VARCHAR(20)           DEFAULT NULL      COMMENT '手机号',
  `avatar`          VARCHAR(255)          DEFAULT ''         COMMENT '头像URL',
  `role_id`         BIGINT UNSIGNED       DEFAULT NULL      COMMENT '角色ID, 关联 sys_role.id',
  `is_super`        TINYINT      NOT NULL DEFAULT 0         COMMENT '是否超级管理员:1-是(绕过权限校验) 0-否',
  `status`          TINYINT      NOT NULL DEFAULT 1         COMMENT '状态:1-正常 0-禁用',
  `last_login_time` DATETIME              DEFAULT NULL      COMMENT '最后登录时间',
  `last_login_ip`   VARCHAR(50)           DEFAULT NULL      COMMENT '最后登录IP',
  `create_time`     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_username` (`username`),
  KEY `idx_role_id` (`role_id`),
  CONSTRAINT `fk_user_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role`(`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='后台管理员用户表';

-- -------------------------------------------------------------
-- 5. sys_role_menu 角色-菜单关联表  (RBAC 多对多授权)
-- -------------------------------------------------------------
DROP TABLE IF EXISTS `sys_role_menu`;
CREATE TABLE `sys_role_menu` (
  `id`          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `role_id`     BIGINT UNSIGNED NOT NULL               COMMENT '角色ID, 关联 sys_role.id',
  `menu_id`     BIGINT UNSIGNED NOT NULL               COMMENT '菜单ID, 关联 sys_menu.id',
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_role_menu` (`role_id`,`menu_id`),
  KEY `idx_menu_id` (`menu_id`),
  CONSTRAINT `fk_rm_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role`(`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_rm_menu` FOREIGN KEY (`menu_id`) REFERENCES `sys_menu`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='角色菜单关联表';

SET FOREIGN_KEY_CHECKS = 1;

-- =============================================================
-- 6. 初始化数据 (可按需修改)
-- =============================================================

-- 6.1 角色
INSERT INTO `sys_role` (`id`,`role_name`,`role_code`,`data_scope`,`remark`) VALUES
 (1,'超级管理员','admin',1,'拥有全部菜单与按钮权限'),
 (2,'运营专员','operation',1,'负责商品与订单管理'),
 (3,'仓库管理员','warehouse',1,'仅负责订单发货'),
 (4,'财务','finance',1,'负责支付对账与退款审核');

-- 6.2 管理员账号 (密码为 BCrypt 占位密文, 对应明文 admin123; 正式环境请务必用代码重新生成并修改!)
--     若你的框架 BCrypt 结果不同, 请用登录接口注册逻辑重置密码后再使用.
INSERT INTO `sys_user` (`id`,`username`,`password`,`real_name`,`role_id`,`is_super`,`status`) VALUES
 (1,'admin','$2a$10$7JB720yubVSZvUI0rEqK/.VqGOZTH.ulu33dHOiBE8ByOhJIrdAu2','超级管理员',1,1,1);

-- 6.3 菜单树 (顶级目录 1~6; 二级菜单 100~; 三级按钮 10000~)
-- 一级目录
INSERT INTO `sys_menu` (`id`,`parent_id`,`menu_name`,`path`,`icon`,`sort_order`,`menu_type`,`permission`) VALUES
 (1,0,'系统管理','/system','setting',1,1,NULL),
 (2,0,'商品管理','/product','goods',2,1,NULL),
 (3,0,'订单管理','/inventory/order','Tickets',3,1,NULL),
 (4,0,'营销管理','/marketing','gift',4,1,NULL),
 (5,0,'财务管理','/finance','money',5,1,NULL);

-- 系统管理 下的菜单
INSERT INTO `sys_menu` (`id`,`parent_id`,`menu_name`,`path`,`component`,`sort_order`,`menu_type`,`permission`) VALUES
 (100,1,'管理员管理','user','system/user/index',1,2,'system:user:list'),
 (101,1,'角色管理','role','system/role/index',2,2,'system:role:list'),
 (102,1,'菜单管理','menu','system/menu/index',3,2,'system:menu:list');

-- 管理员管理 按钮
INSERT INTO `sys_menu` (`id`,`parent_id`,`menu_name`,`sort_order`,`menu_type`,`permission`) VALUES
 (10000,100,'查询',1,3,'system:user:query'),
 (10001,100,'新增',2,3,'system:user:add'),
 (10002,100,'修改',3,3,'system:user:edit'),
 (10003,100,'删除',4,3,'system:user:remove');
-- 角色管理 按钮
INSERT INTO `sys_menu` (`id`,`parent_id`,`menu_name`,`sort_order`,`menu_type`,`permission`) VALUES
 (10100,101,'查询',1,3,'system:role:query'),
 (10101,101,'新增',2,3,'system:role:add'),
 (10102,101,'修改',3,3,'system:role:edit'),
 (10103,101,'删除',4,3,'system:role:remove'),
 (10104,101,'分配权限',5,3,'system:role:grant');
-- 菜单管理 按钮
INSERT INTO `sys_menu` (`id`,`parent_id`,`menu_name`,`sort_order`,`menu_type`,`permission`) VALUES
 (10200,102,'查询',1,3,'system:menu:query'),
 (10201,102,'新增',2,3,'system:menu:add'),
 (10202,102,'修改',3,3,'system:menu:edit'),
 (10203,102,'删除',4,3,'system:menu:remove');

-- 商品/订单 常用菜单示例(供运营/仓库角色授权参考)
INSERT INTO `sys_menu` (`id`,`parent_id`,`menu_name`,`path`,`component`,`sort_order`,`menu_type`,`permission`) VALUES
 (200,2,'商品列表','list','product/list',1,2,'product:list'),
 (300,3,'订单列表','','inventory/order/index',1,2,'order:list'),
 (30000,300,'发货','','',1,3,'order:ship'),
 (30001,300,'关闭订单','','',2,3,'order:cancel'),
 (30002,300,'订单详情','','',3,3,'order:detail'),
 (30003,300,'修改备注','','',4,3,'order:remark'),
 (30004,300,'确认收货','','',5,3,'order:complete'),
 (30005,300,'导出订单','','',6,3,'order:export');

-- 6.4 超级管理员(role_id=1) 拥有全部菜单权限
INSERT INTO `sys_role_menu` (`role_id`,`menu_id`)
SELECT 1, `id` FROM `sys_menu`;

-- 6.5 仓库管理员(role_id=3) 仅授予: 订单管理目录 + 订单列表 + 发货
INSERT INTO `sys_role_menu` (`role_id`,`menu_id`) VALUES
 (3,3),(3,300),(3,30000),(3,30002);
 
 
 
 
 
 
 
 
 
 
 
 
 -- ============================================
-- 镜速订产品订货系统 - 商品模块建表SQL
-- 数据库: db_jingsuding_order
-- 版本: V1.5
-- 引擎: InnoDB | 字符集: utf8mb4 | 排序规则: utf8mb4_unicode_ci
-- ============================================

USE `jingsuding_order`;

-- --------------------------------------------
-- 1. 商品分类表 (prod_category)
-- 说明: 维护订货系统商品分类，支持多级分类（父分类）
-- --------------------------------------------
DROP TABLE IF EXISTS `prod_category`;
CREATE TABLE `prod_category` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `parent_id` BIGINT UNSIGNED NOT NULL DEFAULT 0 COMMENT '父分类ID（0=顶级分类）',
  `category_name` VARCHAR(50) NOT NULL COMMENT '分类名称',
  `category_type` TINYINT DEFAULT NULL COMMENT '分类类型：1-镜片 2-镜架 3-营养品 4-补光设备 5-视觉训练设备 6-远像设备',
  `icon` VARCHAR(255) DEFAULT '' COMMENT '分类图标URL',
  `sort` INT DEFAULT 0 COMMENT '排序号',
  `status` TINYINT NOT NULL DEFAULT 1 COMMENT '状态：1-启用 0-停用',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_parent_id` (`parent_id`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='商品分类表';


-- --------------------------------------------
-- 2. 供应商基础信息表 (sup_supplier)
-- 说明: 记录供应商基础信息，商品通过supplier_id关联本表
-- --------------------------------------------
DROP TABLE IF EXISTS `sup_supplier`;
CREATE TABLE `sup_supplier` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `supplier_name` VARCHAR(100) NOT NULL COMMENT '供应商名称（如：依视路光学）',
  `contact_person` VARCHAR(50) DEFAULT NULL COMMENT '联系人',
  `contact_phone` VARCHAR(20) DEFAULT NULL COMMENT '联系电话',
  `status` TINYINT NOT NULL DEFAULT 1 COMMENT '合作状态：1-启用 0-禁用',
  `remark` VARCHAR(255) DEFAULT NULL COMMENT '备注',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='供应商基础信息表';

-- --------------------------------------------
-- 3. 商品表（SPU）(prod_info)
-- 说明: 商品主表，记录商品基础信息
-- --------------------------------------------

DROP TABLE IF EXISTS `prod_info`;
CREATE TABLE `prod_info` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `prod_category_id` BIGINT UNSIGNED NOT NULL COMMENT '分类ID，关联prod_category表',
  `prod_info_name` VARCHAR(100) NOT NULL COMMENT '商品名称',
  `subtitle` VARCHAR(200) DEFAULT '' COMMENT '商品副标题/卖点描述',
  `brand` VARCHAR(50) DEFAULT '' COMMENT '品牌',
  `prod_info_code` VARCHAR(50) DEFAULT '' COMMENT '商品编码',
  `main_image` VARCHAR(255) DEFAULT '' COMMENT '商品主图URL',
  `cost_price` DECIMAL(12,2) DEFAULT 0.00 COMMENT '商品成本价',
  `unit` VARCHAR(10) DEFAULT '' COMMENT '计量单位（片/副/台/盒等）',
  `min_order_qty` INT DEFAULT 1 COMMENT '最小起订量',
  `is_prescription` TINYINT NOT NULL DEFAULT 0 COMMENT '是否需要配镜参数：1-是 0-否',
  `is_process` TINYINT NOT NULL DEFAULT 0 COMMENT '是否支持代加工：1-是 0-否',
  `is_single_piece` TINYINT NOT NULL DEFAULT 0 COMMENT '是否支持单片下单：1-是 0-否',
  `supplier_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '供应商ID，关联sup_supplier表',
  `sales_count` INT DEFAULT 0 COMMENT '销量',
  `detail` TEXT COMMENT '图文详情（富文本/HTML）',
  `status` TINYINT NOT NULL DEFAULT 1 COMMENT '状态：1-上架 0-下架',
  `sort` INT DEFAULT 0 COMMENT '排序号',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='商品表（SPU）';




-- --------------------------------------------
-- 4. 商品规格项表 (prod_spec_item)
-- 说明: 定义商品的可选规格维度（如折射率、膜层、颜色等）
-- --------------------------------------------
DROP TABLE IF EXISTS `prod_spec_item`;
CREATE TABLE `prod_spec_item` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `prod_info_id` BIGINT UNSIGNED NOT NULL COMMENT '商品ID，关联prod_info表',
  `spec_name` VARCHAR(50) NOT NULL COMMENT '规格项名称（如折射率、膜层、颜色）',
  `spec_type` TINYINT NOT NULL DEFAULT 1 COMMENT '规格类型：1-单选 2-输入',
  `is_required` TINYINT NOT NULL DEFAULT 0 COMMENT '是否必填：1-是 0-否',
  `sort` INT DEFAULT 0 COMMENT '排序号',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='商品规格项表';

-- --------------------------------------------
-- 5. 商品规格值表 (prod_spec_value)
-- 说明: 记录规格项的具体可选值（如折射率可选1.56/1.60/1.67）
-- --------------------------------------------
DROP TABLE IF EXISTS `prod_spec_value`;
CREATE TABLE `prod_spec_value` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `spec_id` BIGINT UNSIGNED NOT NULL COMMENT '规格项ID，关联prod_spec_item表',
  `prod_info_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '商品ID（冗余，便于查询）',
  `spec_value` VARCHAR(50) NOT NULL COMMENT '规格值（如1.60、防蓝光膜、C1）',
  `sort` INT DEFAULT 0 COMMENT '排序号',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='商品规格值表';

-- --------------------------------------------
-- 6. 商品SKU明细表 (prod_sku)
-- 说明: 商品规格库存明细，一个商品可含多个SKU
-- --------------------------------------------
DROP TABLE IF EXISTS `prod_sku`;
CREATE TABLE `prod_sku` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `prod_info_id` BIGINT UNSIGNED NOT NULL COMMENT '商品ID，关联prod_info表',
  `sku_code` VARCHAR(50) NOT NULL COMMENT 'SKU编码（唯一）',
  `sku_name` VARCHAR(100) DEFAULT '' COMMENT 'SKU名称',
  `spec_values` VARCHAR(255) DEFAULT '' COMMENT '规格值组合（如：1.60折射率/防蓝光膜）',
  `retail_price` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT 'SKU零售价',
  `sale_price` DECIMAL(12,2) DEFAULT 0.00 COMMENT 'SKU销售价',
  `stock` INT NOT NULL DEFAULT 0 COMMENT 'SKU库存量',
  `sales_count` INT DEFAULT 0 COMMENT 'SKU销量',
  `image` VARCHAR(255) DEFAULT '' COMMENT 'SKU图片URL',
  `status` TINYINT NOT NULL DEFAULT 1 COMMENT '状态：1-启用 0-停用',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='商品SKU明细表';


-- prod_image 图片资源表（原名 pro_image，已重命名；老库升级执行：RENAME TABLE `pro_image` TO `prod_image`;）
CREATE TABLE `prod_image` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `prod_info_id` bigint DEFAULT NULL COMMENT '关联业务ID，比如产品id/用户id',
  `image_type` varchar(32) DEFAULT NULL COMMENT '图片类型：1-主图 2-轮播图 3-详情图',
  `image_url` varchar(512) NOT NULL COMMENT '原图完整访问url',
  `sort` int DEFAULT 0 COMMENT '排序号，多张图片展示顺序',
  `create_time` datetime DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='产品图片表';




DROP TABLE IF EXISTS `log_stock_log`;
CREATE TABLE `log_stock_log` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `prod_info_id` BIGINT UNSIGNED NOT NULL COMMENT '商品ID，关联prod_info表',
  `sku_id` BIGINT UNSIGNED DEFAULT NULL COMMENT 'SKU ID（0=不区分SKU，作用于整个商品）',
  `change_type` TINYINT NOT NULL COMMENT '变动类型：1-入库 2-出库 3-盘点调整 4-订单扣减 5-其他',
  `change_qty` INT NOT NULL COMMENT '变动数量（正为增加，负为减少）',
  `before_stock` INT NOT NULL DEFAULT 0 COMMENT '变动前库存',
  `after_stock` INT NOT NULL DEFAULT 0 COMMENT '变动后库存',
  `ref_type` VARCHAR(20) DEFAULT NULL COMMENT '关联类型：stock_in/stock_out/check/order',
  `ref_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '关联单据ID',
  `remark` VARCHAR(255) DEFAULT '' COMMENT '备注',
  `operator` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '操作人',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_prod_info_id` (`prod_info_id`),
  KEY `idx_sku_id` (`sku_id`),
  KEY `idx_change_type` (`change_type`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='库存变动日志表（审计追溯）';


DROP TABLE IF EXISTS `log_stock_in`;
CREATE TABLE `log_stock_in` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `stock_in_no` VARCHAR(32) NOT NULL COMMENT '入库单号(如 RK202609100001)',
  `supplier_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '供应商ID，关联sup_supplier表',
  `total_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '入库总成本金额',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-待入库 1-已完成 2-已取消',
  `remark` VARCHAR(255) DEFAULT '' COMMENT '备注',
  `operator` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '操作人',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_stock_in_no` (`stock_in_no`),
  KEY `idx_supplier_id` (`supplier_id`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='商品入库单主表';


DROP TABLE IF EXISTS `log_stock_in_item`;
CREATE TABLE `log_stock_in_item` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `stock_in_id` BIGINT UNSIGNED NOT NULL COMMENT '入库单ID，关联log_stock_in表',
  `prod_info_id` BIGINT UNSIGNED NOT NULL COMMENT '商品ID',
  `sku_id` BIGINT UNSIGNED DEFAULT NULL COMMENT 'SKU ID',
  `quantity` INT NOT NULL DEFAULT 0 COMMENT '入库数量',
  `cost_price` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '入库成本价(用于计算利润)',
  `subtotal` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '小计金额(数量 × 成本价)',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_stock_in_id` (`stock_in_id`),
  KEY `idx_prod_info_id` (`prod_info_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='商品入库单明细表';


DROP TABLE IF EXISTS `log_stock_out`;
CREATE TABLE `log_stock_out` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `stock_out_no` VARCHAR(32) NOT NULL COMMENT '出库单号(如 CK202609100001)',
  `out_type` TINYINT NOT NULL DEFAULT 1 COMMENT '类型：1-样品领用 2-报损 3-其他',
  `total_qty` INT NOT NULL DEFAULT 0 COMMENT '出库总数量',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-待出库 1-已完成',
  `remark` VARCHAR(255) DEFAULT '' COMMENT '备注',
  `operator` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '操作人',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_stock_out_no` (`stock_out_no`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='商品出库单主表';


DROP TABLE IF EXISTS `log_stock_out_item`;
CREATE TABLE `log_stock_out_item` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `stock_out_id` BIGINT UNSIGNED NOT NULL COMMENT '出库单ID，关联log_stock_out表',
  `prod_info_id` BIGINT UNSIGNED NOT NULL COMMENT '商品ID',
  `sku_id` BIGINT UNSIGNED DEFAULT NULL COMMENT 'SKU ID',
  `quantity` INT NOT NULL DEFAULT 0 COMMENT '出库数量',
  `remark` VARCHAR(255) DEFAULT '' COMMENT '备注',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_stock_out_id` (`stock_out_id`),
  KEY `idx_prod_info_id` (`prod_info_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='商品出库单明细表';



DROP TABLE IF EXISTS `log_stock_check`;
CREATE TABLE `log_stock_check` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `check_no` VARCHAR(32) NOT NULL COMMENT '盘点单号(如 PD202609100001)',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-盘点中 1-已完成',
  `remark` VARCHAR(255) DEFAULT '' COMMENT '备注',
  `operator` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '操作人',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_check_no` (`check_no`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='库存盘点单主表';



DROP TABLE IF EXISTS `log_stock_check_item`;
CREATE TABLE `log_stock_check_item` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `check_id` BIGINT UNSIGNED NOT NULL COMMENT '盘点单ID，关联log_stock_check表',
  `prod_info_id` BIGINT UNSIGNED NOT NULL COMMENT '商品ID',
  `sku_id` BIGINT UNSIGNED DEFAULT NULL COMMENT 'SKU ID',
  `system_stock` INT NOT NULL DEFAULT 0 COMMENT '系统库存(盘点前)',
  `actual_stock` INT NOT NULL DEFAULT 0 COMMENT '实际库存(清点后)',
  `diff_qty` INT NOT NULL DEFAULT 0 COMMENT '盈亏数量(正为盈，负为亏)',
  `remark` VARCHAR(255) DEFAULT '' COMMENT '差异原因',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_check_id` (`check_id`),
  KEY `idx_prod_info_id` (`prod_info_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='库存盘点单明细表';



DROP TABLE IF EXISTS `log_stock_warning`;
CREATE TABLE `log_stock_warning` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `prod_info_id` BIGINT UNSIGNED NOT NULL COMMENT '商品ID',
  `sku_id` BIGINT UNSIGNED DEFAULT NULL COMMENT 'SKU ID',
  `warning_stock` INT NOT NULL DEFAULT 10 COMMENT '预警阈值(库存<=此值时报警)',
  `status` TINYINT NOT NULL DEFAULT 1 COMMENT '状态：1-启用 0-停用',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_prod_info_id` (`prod_info_id`),
  KEY `idx_sku_id` (`sku_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='库存预警配置表';





-- ============================================
-- 镜速订订货系统 - 订单管理模块建表语句
-- 数据库版本: MySQL 5.7+ / 8.0+
-- 表名: trx_order (订单主表) + trx_order_item (订单明细表)
-- 说明: 基于EF Core约定命名，外键使用 prod_info_id / sku_id
-- ============================================

-- ----------------------------
-- 表1: trx_order (订单主表)
-- ----------------------------
DROP TABLE IF EXISTS `trx_order`;
CREATE TABLE `trx_order` (
    `id`                    BIGINT          NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `order_no`              VARCHAR(64)     NOT NULL COMMENT '订单号(唯一，格式: ORD+yyyyMMddHHmmss+4位随机数)',
    `buyer_id`              BIGINT          NOT NULL DEFAULT 0 COMMENT '买家ID(用户ID)',
    `receiver_name`         VARCHAR(100)    NOT NULL DEFAULT '' COMMENT '收货人姓名',
    `receiver_phone`        VARCHAR(20)     NOT NULL DEFAULT '' COMMENT '收货人联系电话',
    `receiver_province`     VARCHAR(50)     NOT NULL DEFAULT '' COMMENT '收货省份',
    `receiver_city`         VARCHAR(50)     NOT NULL DEFAULT '' COMMENT '收货城市',
    `receiver_district`     VARCHAR(50)     NOT NULL DEFAULT '' COMMENT '收货区县',
    `receiver_address`      VARCHAR(500)    NOT NULL DEFAULT '' COMMENT '收货详细地址',
    `total_amount`          DECIMAL(12,2)   NOT NULL DEFAULT 0.00 COMMENT '订单总金额(所有明细小计之和)',
    `discount_amount`       DECIMAL(12,2)   NOT NULL DEFAULT 0.00 COMMENT '优惠金额',
    `freight_amount`        DECIMAL(12,2)   NOT NULL DEFAULT 0.00 COMMENT '运费金额',
    `pay_amount`            DECIMAL(12,2)   NOT NULL DEFAULT 0.00 COMMENT '实付金额(总金额-优惠+运费)',
    `order_status`          TINYINT         NOT NULL DEFAULT 0 COMMENT '订单状态: 0-待付款, 1-已支付(待发货), 2-已发货(待收货), 3-已完成, 4-已关闭',
    `pay_method`            TINYINT         NOT NULL DEFAULT 0 COMMENT '支付方式: 0-未支付, 1-在线支付, 2-银行转账, 3-货到付款',
    `pay_time`              DATETIME        DEFAULT NULL COMMENT '支付时间',
    `ship_company`          VARCHAR(100)    NOT NULL DEFAULT '' COMMENT '物流公司(发货时填写)',
    `ship_no`               VARCHAR(100)    NOT NULL DEFAULT '' COMMENT '物流单号(发货时填写)',
    `buyer_remark`          VARCHAR(500)    NOT NULL DEFAULT '' COMMENT '买家备注',
    `merchant_remark`       VARCHAR(500)    NOT NULL DEFAULT '' COMMENT '商家备注(后台内部可见)',
    `close_reason`          VARCHAR(200)    NOT NULL DEFAULT '' COMMENT '关闭原因(超时未付/用户取消等)',
    `close_time`            DATETIME        DEFAULT NULL COMMENT '关闭时间',
    `complete_time`         DATETIME        DEFAULT NULL COMMENT '完成时间',
    `create_time`           DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_time`           DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    `is_deleted`            TINYINT         NOT NULL DEFAULT 0 COMMENT '逻辑删除: 0-正常, 1-已删除',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_order_no` (`order_no`),
    KEY `idx_buyer_id` (`buyer_id`),
    KEY `idx_order_status` (`order_status`),
    KEY `idx_create_time` (`create_time`),
    KEY `idx_pay_time` (`pay_time`),
    KEY `idx_receiver_phone` (`receiver_phone`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='订单主表';


-- ----------------------------
-- 表2: trx_order_item (订单明细表)
-- ----------------------------
DROP TABLE IF EXISTS `trx_order_item`;
CREATE TABLE `trx_order_item` (
    `id`                    BIGINT          NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `order_id`              BIGINT          NOT NULL COMMENT '订单ID(关联trx_order.id)',
    `prod_info_id`          BIGINT          NOT NULL DEFAULT 0 COMMENT '商品ID(关联prod_info.id)',
    `sku_id`                BIGINT          NOT NULL DEFAULT 0 COMMENT 'SKU ID(关联prod_sku.id)',
    `prod_name`             VARCHAR(200)    NOT NULL DEFAULT '' COMMENT '商品名称(快照，商品改名不影响订单)',
    `sku_name`              VARCHAR(200)    NOT NULL DEFAULT '' COMMENT 'SKU名称(快照)',
    `spec_values`           VARCHAR(500)    NOT NULL DEFAULT '' COMMENT '规格值(快照，如: 红色/L码)',
    `product_image`         VARCHAR(500)    NOT NULL DEFAULT '' COMMENT '商品图片(快照)',
    `unit_price`            DECIMAL(10,2)   NOT NULL DEFAULT 0.00 COMMENT '单价(下单时的价格，快照)',
    `quantity`              INT             NOT NULL DEFAULT 1 COMMENT '购买数量',
    `subtotal_amount`       DECIMAL(12,2)   NOT NULL DEFAULT 0.00 COMMENT '小计金额(单价*数量)',
    `create_time`           DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_time`           DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    PRIMARY KEY (`id`),
    KEY `idx_order_id` (`order_id`),
    KEY `idx_prod_info_id` (`prod_info_id`),
    KEY `idx_sku_id` (`sku_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='订单明细表';





START TRANSACTION;

-- =============================================
-- 库存管理模块菜单数据 INSERT 脚本
-- 模块：库存管理 (Inventory Management)
-- 说明：在 sys_menu 表中插入库存管理相关菜单和按钮权限
-- =============================================


-- 1. 顶级目录：库存管理
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (103, 0, '库存管理', '/inventory', 'Layout', 'inventory', 50, 1, NULL, 1, NOW(), NOW());

-- 2. 入库管理（菜单）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (104, 103, '入库管理', '/inventory/stock-in', 'inventory/stockIn/index', 'document', 10, 2, NULL, 1, NOW(), NOW());

-- 入库管理 - 按钮权限
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES
(105, 104, '入库单列表', '', '', '', 1, 3, 'stock_in:list', 1, NOW(), NOW()),
(106, 104, '新增入库', '', '', '', 2, 3, 'stock_in:add', 1, NOW(), NOW()),
(107, 104, '编辑入库', '', '', '', 3, 3, 'stock_in:edit', 1, NOW(), NOW()),
(108, 104, '审核入库', '', '', '', 4, 3, 'stock_in:audit', 1, NOW(), NOW()),
(109, 104, '删除入库', '', '', '', 5, 3, 'stock_in:delete', 1, NOW(), NOW());

-- 3. 出库管理（菜单）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (110, 103, '出库管理', '/inventory/stock-out', 'inventory/stockOut/index', 'document', 20, 2, NULL, 1, NOW(), NOW());

-- 出库管理 - 按钮权限
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES
(111, 110, '出库单列表', '', '', '', 1, 3, 'stock_out:list', 1, NOW(), NOW()),
(112, 110, '新增出库', '', '', '', 2, 3, 'stock_out:add', 1, NOW(), NOW()),
(113, 110, '编辑出库', '', '', '', 3, 3, 'stock_out:edit', 1, NOW(), NOW()),
(114, 110, '审核出库', '', '', '', 4, 3, 'stock_out:audit', 1, NOW(), NOW()),
(115, 110, '删除出库', '', '', '', 5, 3, 'stock_out:delete', 1, NOW(), NOW());

-- 4. 库存盘点（菜单）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (116, 103, '库存盘点', '/inventory/stock-check', 'inventory/stockCheck/index', 'checked', 30, 2, NULL, 1, NOW(), NOW());

-- 库存盘点 - 按钮权限
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES
(117, 116, '盘点单列表', '', '', '', 1, 3, 'stock_check:list', 1, NOW(), NOW()),
(118, 116, '新增盘点', '', '', '', 2, 3, 'stock_check:add', 1, NOW(), NOW()),
(119, 116, '编辑盘点', '', '', '', 3, 3, 'stock_check:edit', 1, NOW(), NOW()),
(120, 116, '盘点详情', '', '', '', 4, 3, 'stock_check:detail', 1, NOW(), NOW()),
(121, 116, '删除盘点', '', '', '', 5, 3, 'stock_check:delete', 1, NOW(), NOW());

-- 5. 库存预警（菜单）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (122, 103, '库存预警', '/inventory/stock-warning', 'inventory/stockWarning/index', 'warning', 40, 2, NULL, 1, NOW(), NOW());

-- 库存预警 - 按钮权限
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES
(123, 122, '预警配置列表', '', '', '', 1, 3, 'stock_warning:list', 1, NOW(), NOW()),
(124, 122, '编辑预警配置', '', '', '', 2, 3, 'stock_warning:edit', 1, NOW(), NOW()),
(125, 122, '启用/停用', '', '', '', 3, 3, 'stock_warning:enable', 1, NOW(), NOW());

-- 6. 库存查询（菜单）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES (126, 103, '库存查询', '/inventory/stock-query', 'inventory/stockQuery/index', 'search', 50, 2, NULL, 1, NOW(), NOW());

-- 库存查询 - 按钮权限
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `status`, `create_time`, `update_time`)
VALUES
(127, 126, '库存总览', '', '', '', 1, 3, 'stock_query:overview', 1, NOW(), NOW()),
(128, 126, '库存明细', '', '', '', 2, 3, 'stock_query:detail', 1, NOW(), NOW()),
(129, 126, '变动日志', '', '', '', 3, 3, 'stock_query:log', 1, NOW(), NOW());

-- =============================================
-- 验证查询：查看库存管理模块所有菜单数据
-- =============================================
SELECT 
    id, 
    parent_id, 
    menu_name, 
    path, 
    sort_order, 
    menu_type, 
    permission, 
    status
FROM sys_menu
WHERE parent_id = 101 OR id = 101
ORDER BY sort_order, id;

COMMIT;


-- ============================================
-- 外键约束（可选，根据项目需要决定是否启用）
-- ============================================
-- ALTER TABLE `prod_info` ADD CONSTRAINT `fk_prod_info_category` FOREIGN KEY (`category_id`) REFERENCES `prod_category` (`id`) ON DELETE SET NULL;
-- ALTER TABLE `prod_info` ADD CONSTRAINT `fk_prod_info_supplier` FOREIGN KEY (`supplier_id`) REFERENCES `sup_supplier` (`id`) ON DELETE SET NULL;
-- ALTER TABLE `prod_spec_item` ADD CONSTRAINT `fk_spec_item_prod` FOREIGN KEY (`prod_info_id`) REFERENCES `prod_info` (`id`) ON DELETE CASCADE;
-- ALTER TABLE `prod_spec_value` ADD CONSTRAINT `fk_spec_value_item` FOREIGN KEY (`spec_id`) REFERENCES `prod_spec_item` (`id`) ON DELETE CASCADE;
-- ALTER TABLE `prod_sku` ADD CONSTRAINT `fk_prod_sku_prod` FOREIGN KEY (`prod_info_id`) REFERENCES `prod_info` (`id`) ON DELETE CASCADE;
-- ALTER TABLE `prod_category` ADD COLUMN `sort_order` INT NOT NULL DEFAULT 0 COMMENT '排序' AFTER `parent_id`;
-- ALTER TABLE `prod_info` ADD COLUMN `description` TEXT NULL COMMENT '商品描述' AFTER `prod_info_name`;

ALTER TABLE jingsuding_order.prod_info   MODIFY COLUMN prod_info_type TINYINT NULL   COMMENT '商品类型：1-镜片 2-镜架 3-营养品 4-补光设备 5-视觉训练设备 6-远像设备';
ALTER TABLE `prod_spec_item` ADD COLUMN `sort_order` INT NOT NULL DEFAULT 0 COMMENT '排序号',ADD COLUMN `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间';

ALTER TABLE `prod_sku` CHANGE COLUMN `price` `retail_price` DECIMAL(12,2) NOT NULL DEFAULT 0.00  COMMENT '零售价/划线价（元）'  AFTER `spec_values`;
ALTER TABLE `prod_sku` CHANGE COLUMN `original_price` `sale_price` DECIMAL(12,2) DEFAULT 0.00 COMMENT '销售价/成交价（元）'  ;

ALTER TABLE `prod_sku`  DROP COLUMN `cost_price`;
-- =======================================================
-- 1. 基础数据模块 (Base Data)
-- =======================================================
-- 插入一级目录：基础数据管理
INSERT INTO `sys_menu` (`parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `status`) 
VALUES 
(0, '基础数据管理', '/base', NULL, 'Setting', 20, 1, 1);

-- 获取刚插入的目录ID（假设这里自增为 100，请根据实际插入后的ID修改下面的 parent_id）
-- 如果你使用的是 Navicat 等工具，可以直接查看自增ID。这里为了演示，假设基础数据目录ID为 100
SET @base_data_menu_id = LAST_INSERT_ID();

-- 插入二级菜单：供应商管理
INSERT INTO `sys_menu` (`parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `status`) 
VALUES 
(@base_data_menu_id, '供应商管理', '/base/supplier', 'base/supplier/index', 'Shop', 1, 2, 1);

-- 获取供应商管理菜单ID，用于挂载按钮
SET @supplier_menu_id = LAST_INSERT_ID();

-- 插入供应商管理页面的按钮权限
INSERT INTO `sys_menu` (`parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `status`) 
VALUES 
(@supplier_menu_id, '供应商查询', '', NULL, NULL, 1, 3, 1),
(@supplier_menu_id, '供应商新增', '', NULL, NULL, 2, 3, 1),
(@supplier_menu_id, '供应商修改', '', NULL, NULL, 3, 3, 1),
(@supplier_menu_id, '供应商删除', '', NULL, NULL, 4, 3, 1);



-- =======================================================
-- 2. 商品管理模块 (Product Management)
-- =======================================================
-- 插入一级目录：商品管理
INSERT INTO `sys_menu` (`parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `status`) 
VALUES 
(0, '商品管理', '/product', NULL, 'Goods', 30, 1, 1);

SET @product_menu_id = LAST_INSERT_ID();

-- 插入二级菜单：商品分类管理
INSERT INTO `sys_menu` (`parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `status`) 
VALUES 
(@product_menu_id, '商品分类', '/product/category', 'product/category/index', 'Menu', 1, 2, 1);

SET @category_menu_id = LAST_INSERT_ID();

-- 插入商品分类页面的按钮权限
INSERT INTO `sys_menu` (`parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `status`) 
VALUES 
(@category_menu_id, '分类查询', '', NULL, NULL, 1, 3, 1),
(@category_menu_id, '分类新增', '', NULL, NULL, 2, 3, 1),
(@category_menu_id, '分类修改', '', NULL, NULL, 3, 3, 1),
(@category_menu_id, '分类删除', '', NULL, NULL, 4, 3, 1);

-- 插入二级菜单：商品列表管理
INSERT INTO `sys_menu` (`parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `status`) 
VALUES 
(@product_menu_id, '商品列表', '/product/list', 'product/list/index', 'List', 2, 2, 1);

SET @product_list_menu_id = LAST_INSERT_ID();

-- 插入商品列表页面的按钮权限
INSERT INTO `sys_menu` (`parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `status`) 
VALUES 
(@product_list_menu_id, '商品查询', '', NULL, NULL, 1, 3, 1),
(@product_list_menu_id, '商品新增', '', NULL, NULL, 2, 3, 1),
(@product_list_menu_id, '商品修改', '', NULL, NULL, 3, 3, 1),
(@product_list_menu_id, '商品删除', '', NULL, NULL, 4, 3, 1);





-- =============================================
-- 模块：订单管理 (Order Management)
-- 说明：包含订单列表及核心操作按钮权限
-- =============================================


-- 2. 二级菜单：订单列表（页面）
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (201, 3, '订单列表', '', 'inventory/order/index', NULL, 1, 2, 'order:list', 1, 1, NOW(), NOW());

-- 3. 按钮：订单查询
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (202, 3, '订单查询', '', '', NULL, 1, 3, 'order:query', 1, 1, NOW(), NOW());

-- 4. 按钮：订单详情
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (203, 3, '订单详情', '', '', NULL, 2, 3, 'order:detail', 1, 1, NOW(), NOW());

-- 5. 按钮：订单备注
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (204, 3, '订单备注', '', '', NULL, 3, 3, 'order:remark', 1, 1, NOW(), NOW());

-- 6. 按钮：订单发货
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (205, 3, '订单发货', '', '', NULL, 4, 3, 'order:ship', 1, 1, NOW(), NOW());

-- 7. 按钮：订单导出
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (206, 3, '订单导出', '', '', NULL, 5, 3, 'order:export', 1, 1, NOW(), NOW());

-- 8. 按钮：取消订单
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (207, 3, '取消订单', '', '', NULL, 6, 3, 'order:cancel', 1, 1, NOW(), NOW());
-- ============================================
-- 菜单层级结构总览
-- ============================================
-- 订单管理 (id=200, M=目录, parent_id=0)
-- └── 订单列表 (id=201, C=菜单, parent_id=200)
--     ├── 订单查询   (id=202, B=按钮, perms=order:query)
--     ├── 订单详情   (id=203, B=按钮, perms=order:detail)
--     ├── 订单备注   (id=204, B=按钮, perms=order:remark)
--     ├── 订单发货   (id=205, B=按钮, perms=order:ship)
--     ├── 订单导出   (id=206, B=按钮, perms=order:export)
--     └── 取消订单   (id=207, B=按钮, perms=order:cancel)








-- =============================================
-- 模块补充：客户管理、财务结算、数据看板
-- =============================================

-- 1. 【一级菜单】数据看板 (Dashboard)
-- 说明：通常作为系统首页，排序设为0或最小值
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (300, 0, '数据看板', '/dashboard', 'dashboard/index', 'DataLine', 0, 1, NULL, 1, 1, NOW(), NOW());

-- 2. 【一级菜单】客户管理 (Customer)
-- 说明：B2B核心模块，管理经销商/客户档案
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (310, 0, '客户管理', '/customer', NULL, 'User', 6, 1, NULL, 1, 1, NOW(), NOW());

    -- 2.1 【二级菜单】客户列表
    INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
    VALUES (311, 310, '客户列表', 'list', 'customer/list/index', NULL, 1, 2, 'customer:list', 1, 1, NOW(), NOW());

        -- 按钮权限
        INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`) VALUES (312, 311, '客户查询', '', '', NULL, 1, 3, 'customer:query', 1, 1, NOW(), NOW());
        INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`) VALUES (313, 311, '新增客户', '', '', NULL, 2, 3, 'customer:add', 1, 1, NOW(), NOW());
        INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`) VALUES (314, 311, '编辑客户', '', '', NULL, 3, 3, 'customer:edit', 1, 1, NOW(), NOW());
        INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`) VALUES (315, 311, '删除客户', '', '', NULL, 4, 3, 'customer:del', 1, 1, NOW(), NOW());

-- 3. 【一级菜单】财务结算 (Finance)
-- 说明：处理应收应付及资金流水
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (320, 0, '财务结算', '/finance', NULL, 'Money', 7, 1, NULL, 1, 1, NOW(), NOW());

    -- 3.1 【二级菜单】应收账款
    INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
    VALUES (321, 320, '应收账款', 'receivable', 'finance/receivable/index', NULL, 1, 2, 'finance:receivable:list', 1, 1, NOW(), NOW());

    -- 3.2 【二级菜单】收款记录
    INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
    VALUES (322, 320, '收款记录', 'payment', 'finance/payment/index', NULL, 2, 2, 'finance:payment:list', 1, 1, NOW(), NOW());

        -- 按钮权限（收款）
        INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`) VALUES (323, 322, '录入收款', '', '', NULL, 1, 3, 'finance:payment:add', 1, 1, NOW(), NOW());
        INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`) VALUES (324, 322, '核销账单', '', '', NULL, 2, 3, 'finance:payment:verify', 1, 1, NOW(), NOW());
        
        
        
        
        
        
        
        
        
        -- =============================================
-- 模块：客户管理 (Customer / Member Management)
-- 表名：mem_member
-- 说明：小程序客户/会员主表，存储客户档案及登录账号信息
-- 关联：trx_order.buyer_id -> mem_member.id
-- =============================================

-- ============================================================
-- 客户管理模块 初始化脚本
-- 包含：mem_member 建表 + 菜单/按钮种子 + 角色授权
-- 说明：与后端 Entities/MemMember.cs、AppDbContext 索引、前端 inventory/customer 路由一致。
--       若表/菜单已存在，先执行末尾的清理脚本，或自行跳过重复段。
-- ============================================================

-- ------------------------------------------------------------
-- 1. 会员 / 客户主表 mem_member
--    订单表 trx_order.buyer_id 即指向本表 id（会员=买家，物理隔离于后台 sys_user）
-- ------------------------------------------------------------
DROP TABLE IF EXISTS `mem_member`;
CREATE TABLE `mem_member` (
  `id`                    BIGINT          NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `member_no`             VARCHAR(32)     NOT NULL COMMENT '会员编号(唯一，M+yyyyMMddHHmmss+4位随机数)',
  `name`                  VARCHAR(100)    NOT NULL DEFAULT '' COMMENT '客户名称/联系人姓名',
  `company_name`          VARCHAR(200)    DEFAULT NULL COMMENT '公司名称(企业客户)',
  `level`                 TINYINT         NOT NULL DEFAULT 0 COMMENT '会员等级: 0-普通 1-VIP 2-核心',
  `discount_rate`         DECIMAL(5,2)    NOT NULL DEFAULT 1.00 COMMENT '折扣率(0.00~1.00, 1.00不打折)',
  `credit_limit`          DECIMAL(12,2)   NOT NULL DEFAULT 0.00 COMMENT '信用额度(元, 0不支持赊账)',
  `account_period`        INT             NOT NULL DEFAULT 0 COMMENT '账期(天, 0款到发货)',
  `total_consumption`     DECIMAL(12,2)   NOT NULL DEFAULT 0.00 COMMENT '累计消费(已支付订单实付之和)',
  `order_count`           INT             NOT NULL DEFAULT 0 COMMENT '累计订单数(已支付)',
  `completed_order_count` INT             NOT NULL DEFAULT 0 COMMENT '已完成订单数',
  `last_order_no`         VARCHAR(64)     DEFAULT NULL COMMENT '最近订单号(支付时回写)',
  `last_order_time`       DATETIME        DEFAULT NULL COMMENT '最近下单时间(支付时回写)',
  `username`              VARCHAR(50)     NOT NULL DEFAULT '' COMMENT '登录账号(唯一, 客户前台登录)',
  `password`              VARCHAR(128)    NOT NULL DEFAULT '' COMMENT '登录密码(BCrypt密文)',
  `phone`                 VARCHAR(20)     DEFAULT NULL COMMENT '联系电话',
  `email`                 VARCHAR(100)    DEFAULT NULL COMMENT '电子邮箱',
  `contact_person`        VARCHAR(50)     DEFAULT NULL COMMENT '业务联系人',
  `address`               VARCHAR(500)    DEFAULT NULL COMMENT '联系地址',
  `remark`                VARCHAR(500)    DEFAULT NULL COMMENT '备注',
  `status`                TINYINT         NOT NULL DEFAULT 1 COMMENT '状态: 1-启用 0-禁用',
  `is_deleted`            TINYINT         NOT NULL DEFAULT 0 COMMENT '逻辑删除: 0-正常 1-已删除',
  `create_time`           DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`           DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_member_no` (`member_no`),
  UNIQUE KEY `uk_username` (`username`),
  KEY `idx_level` (`level`),
  KEY `idx_status` (`status`),
  KEY `idx_create_time` (`create_time`),
  KEY `idx_is_deleted` (`is_deleted`),
  KEY `idx_phone` (`phone`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='会员/客户主表';


-- ------------------------------------------------------------
-- 2. 菜单 / 按钮种子
--    目录"客户管理" + 列表菜单(动态路由 /inventory/customer) + 增删改查按钮
--    前端组件约定：列表=inventory/customer/index，详情页为隐藏路由(后端菜单不注册)
-- ------------------------------------------------------------
INSERT INTO `sys_menu` (`id`,`parent_id`,`menu_name`,`path`,`component`,`sort_order`,`menu_type`,`permission`) VALUES
 (6,    0, '客户管理', '/inventory/customer', 'user',       6, 1, NULL),
 (600,  6, '客户列表', '',                   'inventory/customer/index', 1, 2, 'customer:list'),
 (60000,600,'查询',     '',                   '',                        1, 3, 'customer:query'),
 (60001,600,'新增',     '',                   '',                        2, 3, 'customer:add'),
 (60002,600,'编辑',     '',                   '',                        3, 3, 'customer:edit'),
 (60003,600,'删除',     '',                   '',                        4, 3, 'customer:del');


-- ------------------------------------------------------------
-- 3. 角色授权（示例：授权给运营/仓库角色 id=3，可按需增删）
--    若你的角色 id 不同，把下方 3 替换为实际角色 id 即可
-- ------------------------------------------------------------
INSERT INTO `sys_role_menu` (`role_id`,`menu_id`) VALUES
 (3, 6),      -- 目录
 (3, 600),    -- 列表(可见)
 (3, 60000),  -- 查询
 (3, 60001),  -- 新增
 (3, 60002),  -- 编辑
 (3, 60003);  -- 删除


-- ------------------------------------------------------------
-- 4. （可选）清理脚本：表/菜单/授权已存在时，先执行这段再跑上面
-- ------------------------------------------------------------
-- DELETE FROM sys_role_menu WHERE menu_id IN (6,600,60000,60001,60002,60003);
-- DELETE FROM sys_menu WHERE id IN (6,600,60000,60001,60002,60003);
-- DROP TABLE IF EXISTS `mem_member`;
        
        
        
 -- =============================================
-- 镜速订订货系统 财务结算模块建表 SQL
-- 版本：V1.8
-- 数据库：MySQL 8.0
-- 说明：包含应收账款、支付日志、收款记录、收款核销关联共4张表
--       以及 trx_order 表新增 is_credit 字段的 ALTER 语句
-- =============================================

-- =============================================
-- 1. 应收账款表 (trx_receivable)
-- 说明：记录每个订单产生的应收款项，支撑B2B赊账/月结业务
--       当订单支付成功后，系统自动在表中插入应收记录，
--       并关联客户的账期天数计算到期日。支持多笔收款分次核销。
-- =============================================
CREATE TABLE `trx_receivable` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `order_id` BIGINT UNSIGNED NOT NULL COMMENT '关联订单ID (trx_order.id)',
  `member_id` BIGINT UNSIGNED NOT NULL COMMENT '客户ID (mem_member.id)',
  `total_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '应收总额 (订单实付金额)',
  `paid_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '已收款金额 (累计核销金额)',
  `unpaid_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '未收款金额 (total_amount - paid_amount)',
  `order_date` DATE NOT NULL COMMENT '订单日期 (用于账龄计算)',
  `due_date` DATE DEFAULT NULL COMMENT '到期日 (订单日期 + 客户账期天数)',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-未结清 1-已结清 2-已逾期',
  `remark` VARCHAR(500) DEFAULT NULL COMMENT '备注',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_member_id` (`member_id`),
  KEY `idx_order_id` (`order_id`),
  KEY `idx_status_due_date` (`status`, `due_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='应收账款表（B2B赊账/月结）';

-- =============================================
-- 2. 支付日志表 (trx_payment_log)
-- 说明：记录微信支付回调原始数据，用于支付对账、退款追踪、异常排查
--       这是技术对账的基石表，不可省略
-- =============================================
CREATE TABLE `trx_payment_log` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `order_id` BIGINT UNSIGNED NOT NULL COMMENT '关联订单ID (trx_order.id)',
  `order_no` VARCHAR(32) NOT NULL COMMENT '订单号 (冗余，便于查询)',
  `transaction_id` VARCHAR(64) DEFAULT NULL COMMENT '微信支付交易号 (transaction_id)',
  `out_trade_no` VARCHAR(64) DEFAULT NULL COMMENT '商户订单号 (out_trade_no)',
  `payment_type` TINYINT NOT NULL DEFAULT 1 COMMENT '支付方式：1-微信支付(JSAPI) 2-余额支付 3-其他',
  `amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '支付金额 (元)',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '支付状态：0-待支付 1-已支付 2-已取消 3-已退款 4-支付失败',
  `callback_data` TEXT DEFAULT NULL COMMENT '微信回调原始数据 (XML/JSON，用于对账)',
  `callback_time` DATETIME DEFAULT NULL COMMENT '微信回调时间',
  `error_code` VARCHAR(64) DEFAULT NULL COMMENT '错误码 (支付失败时记录)',
  `error_msg` VARCHAR(500) DEFAULT NULL COMMENT '错误描述',
  `operator_id` INT DEFAULT NULL COMMENT '操作人员ID (后台管理员)',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_order_id` (`order_id`),
  KEY `idx_order_no` (`order_no`),
  KEY `idx_transaction_id` (`transaction_id`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='支付日志表（微信支付回调对账）';

ALTER TABLE trx_payment_log ADD UNIQUE KEY uk_transaction_id (transaction_id);
-- =============================================
-- 3. 收款记录表 (mkt_payment)
-- 说明：记录客户实际收款（银行转账/支票/现金等线下收款），
--       与 trx_payment_log（微信支付回调日志）互补，
--       分别对应"线上支付"和"线下收款"两条资金流。
--       支持一笔收款拆分为多笔核销，关联不同的应收账款记录。
-- =============================================
CREATE TABLE `mkt_payment` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `payment_no` VARCHAR(32) NOT NULL COMMENT '收款单号 (唯一)，如: PAY202609150001',
  `member_id` BIGINT UNSIGNED NOT NULL COMMENT '客户ID (mem_member.id)',
  `amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '实际收款金额 (元)',
  `payment_method` TINYINT NOT NULL DEFAULT 1 COMMENT '收款方式：1-银行转账 2-支票 3-现金 4-其他',
  `payment_date` DATE NOT NULL COMMENT '打款/到账日期',
  `voucher` VARCHAR(255) DEFAULT NULL COMMENT '打款凭证/转账截图路径',
  `remark` VARCHAR(500) DEFAULT NULL COMMENT '备注 (如: 附言、用途)',
  `operator_id` INT NOT NULL COMMENT '后台操作人员ID (录入核销的管理员)',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-待核销 1-已核销',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '录入时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_payment_no` (`payment_no`),
  KEY `idx_member_id` (`member_id`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='收款记录表（线下收款）';

-- =============================================
-- 4. 收款核销关联表 (mkt_payment_item)
-- 说明：解决"一笔收款对应多张订单"的多对多核销场景。
--       管理员录入一笔收款后，可选择多个应收账款进行核销拆分，
--       实现灵活的财务对账。
-- =============================================
CREATE TABLE `mkt_payment_item` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `payment_id` BIGINT UNSIGNED NOT NULL COMMENT '关联收款记录ID (mkt_payment.id)',
  `receivable_id` BIGINT UNSIGNED NOT NULL COMMENT '关联应收账款ID (trx_receivable.id)',
  `offset_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '该笔收款用于此订单的核销金额 (元)',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_payment_id` (`payment_id`),
  KEY `idx_receivable_id` (`receivable_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='收款核销关联表';

-- =============================================
-- 5. ALTER：为 trx_order 表新增 is_credit 字段
-- 说明：标记信用订单（0-否/1-是），区分普通支付订单与赊账/月结订单
--       插入位置：在 seller_remark 字段之后
-- =============================================
ALTER TABLE `trx_order` ADD COLUMN `is_credit` TINYINT NOT NULL DEFAULT 0 COMMENT '是否信用订单：0-否(正常支付) 1-是(走应收流程)' AFTER `merchant_remark`;     

        
        
        
-- ============================================================
-- 镜速订订货系统数据库设计 V1.9
-- 采购管理模块 + 售后管理模块 建表SQL语句
-- 数据库: MySQL 8.0
-- 存储引擎: InnoDB | 字符集: utf8mb4 | 排序规则: utf8mb4_unicode_ci
-- ============================================================

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- --------------------------------------------------------
-- 3.8 采购管理模块
-- --------------------------------------------------------

-- ----------------------------
-- 3.8.1 pur_order 采购订单主表
-- 记录向供应商发起采购的核心订单信息。当库存系统检测到商品库存量低于安全库存阈值时，
-- 系统自动根据商品关联的默认供应商在此表中生成采购订单草稿，经管理员审批后正式生效。
-- ----------------------------
DROP TABLE IF EXISTS `pur_order`;
CREATE TABLE `pur_order` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `order_no` VARCHAR(32) NOT NULL COMMENT '采购单号（业务唯一，如PO-20260920-001）',
  `supplier_id` BIGINT UNSIGNED NOT NULL COMMENT '供应商ID，关联sup_supplier表',
  `order_date` DATE NOT NULL COMMENT '下单日期',
  `expected_delivery_date` DATE DEFAULT NULL COMMENT '预计到货日期',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-草稿 1-审批中 2-审批通过/待入库 3-部分入库 4-已结清 9-作废',
  `total_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '订单总金额（元）',
  `remark` VARCHAR(500) DEFAULT NULL COMMENT '备注',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_order_no` (`order_no`),
  KEY `idx_supplier_id` (`supplier_id`),
  KEY `idx_order_date` (`order_date`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='采购订单主表';

-- ----------------------------
-- 3.8.2 pur_order_item 采购订单明细表
-- 记录采购订单中的商品明细，包括采购数量、单价、小计等。与pur_order主表为一对多关系，
-- 支持一笔采购订单包含多种商品。
-- ----------------------------
DROP TABLE IF EXISTS `pur_order_item`;
CREATE TABLE `pur_order_item` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `order_id` BIGINT UNSIGNED NOT NULL COMMENT '采购订单ID，关联pur_order表',
  `material_id` BIGINT UNSIGNED NOT NULL COMMENT '物料/商品ID，关联prod_info表',
  `quantity` DECIMAL(10,2) NOT NULL DEFAULT 0.00 COMMENT '采购数量',
  `unit_price` DECIMAL(10,2) NOT NULL DEFAULT 0.00 COMMENT '采购单价（元）',
  `subtotal` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '行小计金额（数量x单价）',
  `remark` VARCHAR(255) DEFAULT NULL COMMENT '备注',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_order_id` (`order_id`),
  KEY `idx_material_id` (`material_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='采购订单明细表';

-- ----------------------------
-- 3.8.3 pur_inbound 采购入库单主表
-- 记录采购入库单信息，实现"以采定入"核心逻辑。仓库人员必须选择一张"审批通过"且
-- "未完全入库"的采购订单来创建入库单，系统自动带出订单明细，实收数量可小于等于订单数量
-- （支持分批到货）。入库完成后自动更新采购订单状态及库存。
-- ----------------------------
DROP TABLE IF EXISTS `pur_inbound`;
CREATE TABLE `pur_inbound` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `inbound_no` VARCHAR(32) NOT NULL COMMENT '入库单号（业务唯一，如RK-20260921-001）',
  `order_id` BIGINT UNSIGNED NOT NULL COMMENT '关联采购订单ID（pur_order.id），核心关联字段',
  `warehouse_id` BIGINT UNSIGNED NOT NULL COMMENT '仓库ID，关联仓库表',
  `inbound_date` DATE NOT NULL COMMENT '入库日期',
  `operator_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '入库操作员ID',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-待确认 1-已入库',
  `remark` VARCHAR(500) DEFAULT NULL COMMENT '备注',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_inbound_no` (`inbound_no`),
  KEY `idx_order_id` (`order_id`),
  KEY `idx_warehouse_id` (`warehouse_id`),
  KEY `idx_inbound_date` (`inbound_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='采购入库单主表';

-- ----------------------------
-- 3.8.4 pur_inbound_item 采购入库明细表
-- 记录采购入库单中的商品明细，精确关联采购订单明细行（order_item_id），
-- 实现分批入库的精确追踪。每次入库时校验"已入库总数+本次入库数"不超过订单采购总数，
-- 防止超量入库。
-- ----------------------------
DROP TABLE IF EXISTS `pur_inbound_item`;
CREATE TABLE `pur_inbound_item` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `inbound_id` BIGINT UNSIGNED NOT NULL COMMENT '入库单ID，关联pur_inbound表',
  `order_item_id` BIGINT UNSIGNED NOT NULL COMMENT '关联采购订单明细ID（pur_order_item.id），精确记录本次入库对应订单哪一行',
  `material_id` BIGINT UNSIGNED NOT NULL COMMENT '物料/商品ID（冗余，便于快速查询）',
  `actual_quantity` DECIMAL(10,2) NOT NULL DEFAULT 0.00 COMMENT '实收数量',
  `batch_no` VARCHAR(32) DEFAULT NULL COMMENT '入库批次号',
  `remark` VARCHAR(255) DEFAULT NULL COMMENT '备注',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_inbound_id` (`inbound_id`),
  KEY `idx_order_item_id` (`order_item_id`),
  KEY `idx_material_id` (`material_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='采购入库明细表';

-- ----------------------------
-- 3.8.5 pur_payable 应付账款表
-- 记录向供应商的应付货款，与营销与财务模块的mkt_payment（收款记录）形成对称——
-- mkt_payment管"客户欠我们的钱"（应收），pur_payable管"我们欠供应商的钱"（应付）。
-- 入库单确认后自动生成应付记录，后续可通过线下付款进行核销。
-- ----------------------------
DROP TABLE IF EXISTS `pur_payable`;
CREATE TABLE `pur_payable` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `payable_no` VARCHAR(32) NOT NULL COMMENT '应付单号（业务唯一）',
  `supplier_id` BIGINT UNSIGNED NOT NULL COMMENT '供应商ID，关联sup_supplier表',
  `related_order_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '关联采购订单ID（pur_order.id）',
  `total_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '应付总额（入库明细实收数量x采购单价之和）',
  `paid_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '已付金额（由付款核销时更新）',
  `unpaid_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '未付金额（total_amount - paid_amount）',
  `due_date` DATE DEFAULT NULL COMMENT '到期付款日',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-未付 1-部分付款 2-已结清',
  `remark` VARCHAR(500) DEFAULT NULL COMMENT '备注',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_payable_no` (`payable_no`),
  KEY `idx_supplier_id` (`supplier_id`),
  KEY `idx_related_order_id` (`related_order_id`),
  KEY `idx_status` (`status`),
  KEY `idx_due_date` (`due_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='应付账款表';

CREATE TABLE IF NOT EXISTS `trx_refund` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `refund_no`      VARCHAR(32)     NOT NULL                COMMENT '退款单号（业务唯一，TK+日期+序号）',
  `order_id`       BIGINT UNSIGNED NOT NULL                COMMENT '订单ID，关联 trx_order.id',
  `order_no`       VARCHAR(50)     DEFAULT NULL            COMMENT '订单号（冗余，便于列表检索）',
  `member_id`      BIGINT UNSIGNED NOT NULL                COMMENT '申请人ID，关联 mem_member.id',
  `refund_amount`  DECIMAL(12,2)   NOT NULL DEFAULT 0.00   COMMENT '退款金额（元）',
  `refund_type`    TINYINT         NOT NULL DEFAULT 1      COMMENT '退款类型：1-仅退款 2-退货退款',
  `refund_reason`  VARCHAR(255)    DEFAULT NULL            COMMENT '退款原因',
  `voucher_images` VARCHAR(500)    DEFAULT NULL            COMMENT '凭证图片（多个用逗号分隔的相对路径）',
  `status`         TINYINT         NOT NULL DEFAULT 0      COMMENT '状态：0-待审核 1-待退款(已通过) 2-已退款 3-已驳回',
  `admin_remark`   VARCHAR(255)    DEFAULT NULL            COMMENT '客服/财务处理备注（审核意见）',
  `operator_id`    BIGINT UNSIGNED DEFAULT NULL            COMMENT '处理人ID（审核/执行人）',
  `handle_time`    DATETIME        DEFAULT NULL            COMMENT '审核处理时间',
  `refund_time`    DATETIME        DEFAULT NULL            COMMENT '实际打款时间（执行退款写入）',
  `create_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_refund_no` (`refund_no`),
  KEY `idx_order_id`    (`order_id`),
  KEY `idx_member_id`   (`member_id`),
  KEY `idx_status`      (`status`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='退款申请表（售后资金流主表）';

-- ------------------------------------------------------------
-- 2) trx_return_item 补 received_quantity（实收数量）
--    仓库收货时要按"实收数量"回滚库存，原建表脚本缺此列。
--    纯动态 SQL，无需 DELIMITER；列已存在则跳过。
-- ------------------------------------------------------------
SET @db  = DATABASE();
SET @cnt = (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'trx_return_item' AND COLUMN_NAME = 'received_quantity'
);
SET @sql = IF(@cnt = 0,
  'ALTER TABLE `trx_return_item` ADD COLUMN `received_quantity` INT NOT NULL DEFAULT 0 COMMENT ''实收数量（仓库收货写入；0=拒收）''',
  'SELECT ''received_quantity 已存在，跳过'' AS skip_reason'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 3) 执行结果自检（应看到 trx_refund / trx_return / trx_return_item / trx_refund_log 四张表）
-- ------------------------------------------------------------
SELECT TABLE_NAME, TABLE_COMMENT
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('trx_refund', 'trx_return', 'trx_return_item', 'trx_refund_log')
ORDER BY TABLE_NAME;

-- SHOW COLUMNS FROM `trx_refund`;

-- --------------------------------------------------------
-- 3.9 售后管理模块
-- --------------------------------------------------------

select * from trx_refund_log
select * from trx_return
select * from trx_return_item

-- ----------------------------
-- 3.9.1 trx_refund_log 售后日志表
-- 记录售后单（trx_refund）的状态变更日志和操作记录，便于追溯售后处理全过程。
-- 每次售后状态流转（待审核->已审核->已发货->已收货->已退款/已拒绝）均在此表插入日志记录，
-- 支持客服和管理人员审计。
-- ----------------------------
DROP TABLE IF EXISTS `trx_refund_log`;
CREATE TABLE `trx_refund_log` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `refund_id` BIGINT UNSIGNED NOT NULL COMMENT '售后单ID，关联trx_refund表',
  `action` VARCHAR(50) NOT NULL COMMENT '操作动作（如created/approved/shipped/received/refunded/rejected）',
  `operator_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '操作人员ID（后台管理员/客服）',
  `operator_name` VARCHAR(50) DEFAULT NULL COMMENT '操作人名称（冗余，便于查询）',
  `remark` VARCHAR(500) DEFAULT NULL COMMENT '备注/处理说明',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_refund_id` (`refund_id`),
  KEY `idx_action` (`action`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='售后日志表';

-- ----------------------------
-- 3.9.2 trx_return 退货单表
-- 记录客户退货申请的完整信息，包括退货原因、退货商品、物流信息及收货确认。
-- 与trx_refund表配合使用：trx_refund记录退款申请（资金流向），
-- trx_return记录实物退货（物流流向），二者通过order_id关联同一笔订单。
-- 支持部分退货（一笔订单中只退部分商品）。
-- ----------------------------
DROP TABLE IF EXISTS `trx_return`;
CREATE TABLE `trx_return` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `return_no` VARCHAR(32) NOT NULL COMMENT '退货单号（业务唯一）',
  `order_id` BIGINT UNSIGNED NOT NULL COMMENT '订单ID，关联trx_order表',
  `refund_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '关联售后单ID（trx_refund.id），与退款申请联动',
  `member_id` BIGINT UNSIGNED NOT NULL COMMENT '申请退货的用户ID，关联mem_member表',
  `return_reason` VARCHAR(255) NOT NULL COMMENT '退货原因（如度数做错/质量问题/七天无理由等）',
  `return_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '退货涉及金额（元）',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-待审核 1-已审核/待退货 2-已发货/已寄回 3-仓库已收货 4-已退款 5-已拒绝',
  `logistics_company` VARCHAR(50) DEFAULT NULL COMMENT '退货物流公司',
  `logistics_no` VARCHAR(50) DEFAULT NULL COMMENT '退货物流单号',
  `receive_address` TEXT DEFAULT NULL COMMENT '退货收货地址（快照）',
  `admin_remark` VARCHAR(255) DEFAULT NULL COMMENT '客服/仓库备注',
  `handle_time` DATETIME DEFAULT NULL COMMENT '处理时间',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_return_no` (`return_no`),
  KEY `idx_order_id` (`order_id`),
  KEY `idx_refund_id` (`refund_id`),
  KEY `idx_member_id` (`member_id`),
  KEY `idx_status` (`status`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='退货单表';

-- ----------------------------
-- 3.9.3 trx_return_item 退货明细表
-- 记录退货单中的具体退货商品明细。一笔退货单可能包含多个商品（或同一商品的不同SKU），
-- 此表精确记录每个退货商品的名称、规格、数量、单价及小计，
-- 与trx_order_item订单明细表形成对应关系，支持精确的退货入库和退款金额计算。
-- ----------------------------
DROP TABLE IF EXISTS `trx_return_item`;
CREATE TABLE `trx_return_item` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `return_id` BIGINT UNSIGNED NOT NULL COMMENT '退货单ID，关联trx_return表',
  `order_item_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '关联订单明细ID（trx_order_item.id）',
  `prod_info_id` BIGINT UNSIGNED NOT NULL COMMENT '商品ID，关联prod_info表',
  `sku_id` BIGINT UNSIGNED DEFAULT NULL COMMENT 'SKU ID，关联prod_sku表',
  `prod_info_name` VARCHAR(100) NOT NULL COMMENT '商品名称（退货时快照）',
  `sku_name` VARCHAR(100) DEFAULT NULL COMMENT 'SKU名称（退货时快照）',
  `quantity` INT NOT NULL DEFAULT 1 COMMENT '退货数量',
  `unit_price` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '商品单价（元，退货时快照）',
  `subtotal` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '退货小计金额（数量x单价）',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '退货状态：0-待确认 1-已收货 2-已拒收',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_return_id` (`return_id`),
  KEY `idx_order_item_id` (`order_item_id`),
  KEY `idx_prod_info_id` (`prod_info_id`),
  KEY `idx_sku_id` (`sku_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='退货明细表';

-- --------------------------------------------------------
-- 恢复外键检查
-- --------------------------------------------------------
SET FOREIGN_KEY_CHECKS = 1;
        
-- ============================================
-- 采购管理模块菜单数据（一级 + 二级）
-- 说明：一级菜单为目录(M)，二级菜单为页面(C)
-- 假设当前最大菜单ID为400，请根据实际数据库自增值调整
-- ============================================

-- 一级菜单：采购管理（目录，parent_id=0 表示顶级菜单）
INSERT INTO `sys_menu` 
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES 
(401, 0, '采购管理', 'purchase', NULL, 'ShoppingCart', 4, '1', NULL, '1', '1', NOW(), NOW());

-- 二级菜单：采购订单（页面，挂在采购管理目录下）
INSERT INTO `sys_menu` 
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES 
(402, 401, '采购订单', 'purchase-order', 'procurement/order/index', 'Document', 1, '2', 'purchase:order:list', '1', '1', NOW(), NOW());

-- 二级菜单：采购入库（页面，挂在采购管理目录下）
INSERT INTO `sys_menu` 
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES 
(403, 401, '采购入库', 'purchase-inbound', 'procurement/inbound/index', 'Box', 2, '2', 'purchase:inbound:list', '1', '1', NOW(), NOW());

-- 二级菜单：应付账款（页面，挂在采购管理目录下）
INSERT INTO `sys_menu` 
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES 
(404, 401, '应付账款', 'purchase-payable', 'procurement/payable/index', 'Money', 3, '2', 'purchase:payable:list', '1', '1', NOW(), NOW());
        
SELECT `COLUMN_NAME`, `COLUMN_TYPE`, `IS_NULLABLE`, `COLUMN_COMMENT`
FROM `information_schema`.`COLUMNS`
WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'pur_order'
ORDER BY `ORDINAL_POSITION`;  


-- ============================================
-- 售后管理模块菜单（一级 + 二级）
-- 字段：id, parent_id, menu_name, path, component, icon, 
--        sort_order, menu_type, permission, visible, status, 
--        create_time, update_time
-- ============================================

select * from sys_menu  where parent_id = 501

-- 一级菜单：售后管理（目录）
INSERT INTO `sys_menu` 
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES 
(501, 0, '售后管理', 'after-sale', NULL, 'Service', 5, '1', NULL, '1', '1', NOW(), NOW());

-- 二级菜单：退款申请
INSERT INTO `sys_menu` 
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES 
(502, 501, '退款申请', 'refund-apply', 'after-sale/refund-apply/index', 'Refund', 1, '2', 'after-sale:refund:list', '1', '1', NOW(), NOW());

-- 二级菜单：退货管理
INSERT INTO `sys_menu` 
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES 
(503, 501, '退货管理', 'return-manage', 'after-sale/return-manage/index', 'Return', 2, '2', 'after-sale:return:list', '1', '1', NOW(), NOW());

-- 二级菜单：售后日志
INSERT INTO `sys_menu` 
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES 
(504, 501, '售后日志', 'refund-log', 'after-sale/refund-log/index', 'Document', 3, '2', 'after-sale:log:list', '1', '1', NOW(), NOW());




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












-- ============================================================
-- 镜速订产品订货系统  价格策略模块 + 会员余额与充值模块
-- 对应数据库设计文档 V2.0 第 3.10 / 3.11 节（共 8 张表）
-- 数据库：MySQL 8.0   引擎：InnoDB   字符集：utf8mb4 / utf8mb4_unicode_ci
-- 规范：BIGINT UNSIGNED 自增主键；金额 DECIMAL(12,2)；折扣 DECIMAL(5,4)；
--       时间 DATETIME DEFAULT CURRENT_TIMESTAMP；外键字段建索引，业务唯一字段建唯一索引
-- 执行顺序无循环依赖，按文件从上到下依次执行即可
-- ============================================================

SET NAMES utf8mb4;

-- ------------------------------------------------------------
-- 1. mem_member_level  客户(会员)等级表
-- ------------------------------------------------------------
CREATE TABLE `mem_member_level` (
  `id`               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `level_name`       VARCHAR(32)  NOT NULL COMMENT '等级名称（如普通客户、VIP、一级批发商）',
  `level_code`       VARCHAR(16)  NOT NULL COMMENT '等级编码（如L01、L02，唯一）',
  `sort_order`       INT          NOT NULL DEFAULT 0 COMMENT '排序权重（等级越高数值越大）',
  `default_discount` DECIMAL(5,4) NOT NULL DEFAULT 1.0000 COMMENT '默认折扣率（如0.9000表示9折，作用于全部商品）',
  `description`      VARCHAR(255) NULL DEFAULT NULL COMMENT '等级描述',
  `status`           TINYINT      NOT NULL DEFAULT 1 COMMENT '状态：1-启用 0-停用',
  `create_time`      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_level_code` (`level_code`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='客户(会员)等级表';

-- ------------------------------------------------------------
-- 2. price_strategy  价格策略主表
-- ------------------------------------------------------------
CREATE TABLE `price_strategy` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `strategy_no`    VARCHAR(32)  NOT NULL COMMENT '策略编号（业务唯一，如PS-20260922-001）',
  `strategy_name`  VARCHAR(64)  NOT NULL COMMENT '策略名称（如VIP客户等级价、2026国庆促销）',
  `strategy_type`  TINYINT      NOT NULL DEFAULT 1 COMMENT '策略类型：1-客户等级价 2-客户专属价 3-批量阶梯价 4-促销价',
  `priority`       INT          NOT NULL DEFAULT 0 COMMENT '优先级（数值越大越优先，多策略冲突时取价依据）',
  `effective_date` DATETIME     NULL DEFAULT NULL COMMENT '生效日期',
  `expire_date`    DATETIME     NULL DEFAULT NULL COMMENT '失效日期（NULL=永久有效）',
  `status`         TINYINT      NOT NULL DEFAULT 0 COMMENT '状态：0-草稿 1-启用 2-停用',
  `remark`         VARCHAR(255) NULL DEFAULT NULL COMMENT '策略描述/备注',
  `create_by`      BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '创建人ID',
  `create_time`    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_strategy_no` (`strategy_no`),
  KEY `idx_strategy_type` (`strategy_type`),
  KEY `idx_status` (`status`),
  KEY `idx_effective` (`effective_date`, `expire_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格策略主表';

-- ------------------------------------------------------------
-- 3. price_rule  价格规则表
-- ------------------------------------------------------------
CREATE TABLE `price_rule` (
  `id`                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `strategy_id`       BIGINT UNSIGNED NOT NULL COMMENT '关联price_strategy策略ID',
  `mem_member_id`     BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '客户ID（客户专属价时填写，关联mem_member表）',
  `customer_level_id` BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '客户等级ID（等级价时填写，关联mem_member_level表）',
  `apply_scope`       TINYINT      NOT NULL DEFAULT 1 COMMENT '适用范围：1-全部商品 2-指定分类 3-指定商品',
  `category_id`       BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '商品分类ID（apply_scope=2时填写，关联prod_category表）',
  `calc_type`         TINYINT      NOT NULL DEFAULT 1 COMMENT '计算方式：1-固定价格 2-折扣率 3-减免金额',
  `calc_value`        DECIMAL(12,2) NULL DEFAULT 0.00 COMMENT '计算值（折扣率如0.85表示85折；减免金额如10表示减10元）',
  `min_price`         DECIMAL(12,2) NULL DEFAULT NULL COMMENT '最低限价（底价保护，成交价低于此价需审批）',
  `status`            TINYINT      NOT NULL DEFAULT 1 COMMENT '状态：1-启用 0-禁用',
  `create_time`       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_strategy_id` (`strategy_id`),
  KEY `idx_mem_member_id` (`mem_member_id`),
  KEY `idx_customer_level_id` (`customer_level_id`),
  KEY `idx_category_id` (`category_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格规则表';

-- ------------------------------------------------------------
-- 4. price_rule_item  价格规则明细表
-- ------------------------------------------------------------
CREATE TABLE `price_rule_item` (
  `id`           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `rule_id`      BIGINT UNSIGNED NOT NULL COMMENT '关联price_rule规则ID',
  `prod_info_id` BIGINT UNSIGNED NOT NULL COMMENT '商品ID，关联prod_info表',
  `sku_id`       BIGINT UNSIGNED NULL DEFAULT NULL COMMENT 'SKU ID（精确到SKU定价时填写，关联prod_sku表）',
  `tier_no`      INT          NOT NULL DEFAULT 1 COMMENT '阶梯序号（阶梯价用1/2/3...，非阶梯统一为1）',
  `min_quantity` INT          NOT NULL DEFAULT 0 COMMENT '最小数量阈值（阶梯价：>=此数量生效）',
  `max_quantity` INT          NULL DEFAULT NULL COMMENT '最大数量阈值（NULL=无上限）',
  `price`        DECIMAL(12,2) NULL DEFAULT NULL COMMENT '固定价格（calc_type=1时使用）',
  `discount`     DECIMAL(5,4)  NULL DEFAULT NULL COMMENT '折扣率（calc_type=2时使用，如0.8500）',
  `reduce_amount` DECIMAL(12,2) NULL DEFAULT NULL COMMENT '减免金额（calc_type=3时使用）',
  `create_time`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_rule_id` (`rule_id`),
  KEY `idx_prod_info_id` (`prod_info_id`),
  KEY `idx_sku_id` (`sku_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格规则明细表';

-- ------------------------------------------------------------
-- 5. price_change_log  价格变更日志表
-- ------------------------------------------------------------
CREATE TABLE `price_change_log` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `strategy_id`    BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '关联price_strategy策略ID',
  `rule_id`        BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '关联price_rule规则ID（可选）',
  `operation_type` TINYINT      NOT NULL DEFAULT 1 COMMENT '操作类型：1-新增 2-修改 3-停用 4-删除',
  `field_name`     VARCHAR(64)  NULL DEFAULT NULL COMMENT '变更字段名',
  `old_value`      VARCHAR(255) NULL DEFAULT NULL COMMENT '变更前值',
  `new_value`      VARCHAR(255) NULL DEFAULT NULL COMMENT '变更后值',
  `operator_id`    BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '操作人ID',
  `operate_time`   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '操作时间',
  `reason`         VARCHAR(255) NULL DEFAULT NULL COMMENT '变更原因',
  PRIMARY KEY (`id`),
  KEY `idx_strategy_id` (`strategy_id`),
  KEY `idx_rule_id` (`rule_id`),
  KEY `idx_operate_time` (`operate_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格变更日志表';

-- ------------------------------------------------------------
-- 6. order_price_snapshot  订单价格快照表
-- ------------------------------------------------------------
CREATE TABLE `order_price_snapshot` (
  `id`                    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `trx_order_id`          BIGINT UNSIGNED NOT NULL COMMENT '关联trx_order订单ID',
  `trx_order_item_id`     BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '关联trx_order_item订单明细ID',
  `prod_info_id`          BIGINT UNSIGNED NOT NULL COMMENT '商品ID',
  `sku_id`                BIGINT UNSIGNED NULL DEFAULT NULL COMMENT 'SKU ID',
  `standard_price`        DECIMAL(12,2) NULL DEFAULT 0.00 COMMENT '商品标准售价（快照）',
  `matched_strategy_id`   BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '匹配到的价格策略ID',
  `matched_strategy_type` TINYINT      NULL DEFAULT NULL COMMENT '匹配策略类型（1-5对应五种优先级）',
  `matched_rule_id`       BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '匹配到的价格规则ID',
  `final_price`           DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '最终成交价',
  `discount_info`         VARCHAR(255) NULL DEFAULT NULL COMMENT '折扣信息描述（如"VIP等级价 85折"）',
  `create_time`           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_trx_order_id` (`trx_order_id`),
  KEY `idx_trx_order_item_id` (`trx_order_item_id`),
  KEY `idx_prod_info_id` (`prod_info_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='订单价格快照表';

-- ------------------------------------------------------------
-- 7. mem_recharge  充值订单表
-- ------------------------------------------------------------
CREATE TABLE `mem_recharge` (
  `id`               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `recharge_no`      VARCHAR(32)  NOT NULL COMMENT '充值单号（业务唯一，如CZ-20260922-001）',
  `mem_member_id`    BIGINT UNSIGNED NOT NULL COMMENT '客户ID，关联mem_member表',
  `recharge_amount`  DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '充值金额（元）',
  `gift_amount`      DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '赠送金额（元，如充1000送100）',
  `pay_type`         TINYINT      NOT NULL DEFAULT 1 COMMENT '支付方式：1-微信支付 2-线下转账',
  `pay_status`       TINYINT      NOT NULL DEFAULT 0 COMMENT '支付状态：0-待支付 1-已到账 2-已关闭 3-已退款',
  `transaction_id`   VARCHAR(64)  NULL DEFAULT NULL COMMENT '微信支付交易单号（回调写入，用于对账幂等）',
  `recharge_channel` TINYINT      NOT NULL DEFAULT 1 COMMENT '充值渠道：1-小程序自助 2-后台代充',
  `operator_id`      BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '后台代充操作人ID（关联sys_user表）',
  `pay_time`         DATETIME     NULL DEFAULT NULL COMMENT '到账时间',
  `remark`           VARCHAR(255) NULL DEFAULT NULL COMMENT '备注/凭证说明',
  `create_time`      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_recharge_no` (`recharge_no`),
  UNIQUE KEY `uk_transaction_id` (`transaction_id`),
  KEY `idx_mem_member_id` (`mem_member_id`),
  KEY `idx_pay_status` (`pay_status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='充值订单表';

-- ------------------------------------------------------------
-- 8. mkt_balance_log  余额流水表（余额唯一真值台账）
-- ------------------------------------------------------------
CREATE TABLE `mkt_balance_log` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `mem_member_id`  BIGINT UNSIGNED NOT NULL COMMENT '客户ID，关联mem_member表',
  `change_type`    TINYINT      NOT NULL DEFAULT 1 COMMENT '变动类型：1-充值 2-下单扣款 3-退款退回 4-提现 5-后台调整 6-赠送',
  `change_amount`  DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '变动金额（正=增加，负=减少）',
  `before_balance` DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '变动前余额',
  `after_balance`  DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT '变动后余额',
  `related_type`   TINYINT      NOT NULL DEFAULT 0 COMMENT '关联单据类型：0-无 1-订单 2-充值订单 3-退款单',
  `related_id`     BIGINT UNSIGNED NULL DEFAULT NULL COMMENT '关联单据ID（订单/充值/退款ID）',
  `remark`         VARCHAR(255) NULL DEFAULT NULL COMMENT '备注',
  `create_time`    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_mem_member_id` (`mem_member_id`),
  KEY `idx_related` (`related_type`, `related_id`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='余额流水表';


-- ============================================================
-- 价格策略与会员余额模块 —— 数据库脚本
-- 目标库：jingsuding_order
--
-- 内容：
--   1) 8 张新建表：mem_member_level / price_strategy / price_rule / price_rule_item
--                 price_change_log / order_price_snapshot / mem_recharge / mkt_balance_log
--   2) mem_member 扩展 4 列：customer_level_id / balance / frozen_balance / total_recharge
--
-- 幂等：CREATE TABLE IF NOT EXISTS + information_schema 判列，可重复执行，不破坏现有数据。
-- 用法：全选执行即可（Navicat / DBeaver / mysql CLI 均可）。
-- 字段与后端实体 Jsd.Api/Entities/*.cs 严格一一对应（含 decimal(10,2) / decimal(5,4) 精度）。
-- ============================================================

USE `jingsuding_order`;

-- ------------------------------------------------------------
-- 1) mem_member_level 客户（会员）等级表
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `mem_member_level` (
  `id`               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `level_name`       VARCHAR(50)     NOT NULL                COMMENT '等级名称',
  `level_code`       VARCHAR(20)     NOT NULL                COMMENT '等级编码（唯一）',
  `sort_order`       INT             NOT NULL DEFAULT 0      COMMENT '排序权重（数值越大越靠前）',
  `default_discount` DECIMAL(5,4)    NOT NULL DEFAULT 1.0000 COMMENT '默认折扣率（0.8500 表示 85 折）',
  `description`      VARCHAR(255)    DEFAULT NULL            COMMENT '描述',
  `status`           TINYINT         NOT NULL DEFAULT 1      COMMENT '状态：0-停用 1-启用',
  `create_time`      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_level_code` (`level_code`),
  KEY `idx_status`     (`status`),
  KEY `idx_sort_order` (`sort_order`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='客户（会员）等级表';

-- ------------------------------------------------------------
-- 2) price_strategy 价格策略主表
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `price_strategy` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `strategy_no`    VARCHAR(32)     NOT NULL                COMMENT '策略编号（唯一）',
  `strategy_name`  VARCHAR(100)    NOT NULL                COMMENT '策略名称',
  `strategy_type`  TINYINT         NOT NULL DEFAULT 1      COMMENT '1-客户等级价 2-客户专属价 3-批量阶梯价 4-促销价',
  `priority`       INT             NOT NULL DEFAULT 0      COMMENT '优先级（数值越大越优先）',
  `effective_date` DATETIME        NOT NULL                COMMENT '生效时间',
  `expire_date`    DATETIME        NOT NULL                COMMENT '失效时间',
  `status`         TINYINT         NOT NULL DEFAULT 0      COMMENT '状态：0-草稿 1-启用 2-停用',
  `description`    VARCHAR(255)    DEFAULT NULL            COMMENT '描述',
  `create_by`      BIGINT UNSIGNED NOT NULL DEFAULT 0      COMMENT '创建人ID（sys_user.id）',
  `create_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_strategy_no` (`strategy_no`),
  KEY `idx_strategy_type` (`strategy_type`),
  KEY `idx_status`        (`status`),
  KEY `idx_effective`     (`effective_date`),
  KEY `idx_expire`        (`expire_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格策略主表';

-- ------------------------------------------------------------
-- 3) price_rule 价格规则表
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `price_rule` (
  `id`                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `strategy_id`       BIGINT UNSIGNED NOT NULL                COMMENT '关联策略ID（price_strategy.id）',
  `mem_member_id`     BIGINT UNSIGNED DEFAULT NULL            COMMENT '会员ID（客户专属价时填）',
  `customer_level_id` BIGINT UNSIGNED DEFAULT NULL            COMMENT '等级ID（客户等级价时填，mem_member_level.id）',
  `apply_scope`       TINYINT         NOT NULL DEFAULT 1      COMMENT '1-全部商品 2-指定分类 3-指定商品',
  `category_id`       BIGINT UNSIGNED DEFAULT NULL            COMMENT '分类ID（apply_scope=2 时填，prod_category.id）',
  `calc_type`         TINYINT         NOT NULL DEFAULT 2      COMMENT '1-固定价 2-折扣率 3-减免金额',
  `calc_value`        DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '计算值（固定价=单价 / 折扣率=0.85 / 减免=金额）',
  `min_price`         DECIMAL(10,2)   DEFAULT NULL            COMMENT '底价保护（低于此值仅打标不拦截）',
  `status`            TINYINT         NOT NULL DEFAULT 1      COMMENT '状态：0-停用 1-启用',
  `create_time`       DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`       DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_strategy_id`       (`strategy_id`),
  KEY `idx_mem_member_id`     (`mem_member_id`),
  KEY `idx_customer_level_id` (`customer_level_id`),
  KEY `idx_status`            (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格规则表';

-- ------------------------------------------------------------
-- 4) price_rule_item 价格规则明细表（指定商品 / 阶梯价）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `price_rule_item` (
  `id`            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `rule_id`       BIGINT UNSIGNED NOT NULL                COMMENT '关联规则ID（price_rule.id）',
  `material_id`   BIGINT UNSIGNED NOT NULL                COMMENT '商品物料ID（prod_sku.id）',
  `tier_no`       INT             NOT NULL DEFAULT 1      COMMENT '阶梯序号（从 1 开始）',
  `min_quantity`  INT             NOT NULL DEFAULT 0      COMMENT '最小数量（含）',
  `max_quantity`  INT             DEFAULT NULL            COMMENT '最大数量（含；NULL/0 表示不封顶）',
  `price`         DECIMAL(10,2)   DEFAULT NULL            COMMENT '阶梯单价（固定价）',
  `discount`      DECIMAL(5,4)    DEFAULT NULL            COMMENT '阶梯折扣（折扣率）',
  `reduce_amount` DECIMAL(10,2)   DEFAULT NULL            COMMENT '阶梯减免金额',
  PRIMARY KEY (`id`),
  KEY `idx_rule_id`     (`rule_id`),
  KEY `idx_material_id` (`material_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格规则明细表（指定商品/阶梯价）';

-- ------------------------------------------------------------
-- 5) price_change_log 价格变更日志表（审计）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `price_change_log` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `strategy_id`    BIGINT UNSIGNED DEFAULT NULL            COMMENT '策略ID',
  `rule_id`        BIGINT UNSIGNED DEFAULT NULL            COMMENT '规则ID',
  `operation_type` TINYINT         NOT NULL DEFAULT 1      COMMENT '1-新增 2-修改 3-停用 4-删除',
  `field_name`     VARCHAR(50)     DEFAULT NULL            COMMENT '变更字段',
  `old_value`      VARCHAR(255)    DEFAULT NULL            COMMENT '旧值',
  `new_value`      VARCHAR(255)    DEFAULT NULL            COMMENT '新值',
  `operator_id`    BIGINT UNSIGNED DEFAULT NULL            COMMENT '操作人ID（sys_user.id）',
  `operate_time`   DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '操作时间',
  `reason`         VARCHAR(255)    DEFAULT NULL            COMMENT '变更原因',
  PRIMARY KEY (`id`),
  KEY `idx_strategy_id`    (`strategy_id`),
  KEY `idx_rule_id`        (`rule_id`),
  KEY `idx_operation_type` (`operation_type`),
  KEY `idx_operate_time`   (`operate_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='价格变更日志表';

-- ------------------------------------------------------------
-- 6) order_price_snapshot 订单价格快照表（历史价格追溯唯一依据）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `order_price_snapshot` (
  `id`                    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `order_id`              BIGINT UNSIGNED NOT NULL                COMMENT '订单ID（trx_order.id）',
  `order_item_id`         BIGINT UNSIGNED NOT NULL                COMMENT '订单明细ID（trx_order_item.id）',
  `material_id`           BIGINT UNSIGNED NOT NULL                COMMENT '商品物料ID（prod_sku.id）',
  `standard_price`        DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '标准售价',
  `matched_strategy_id`   BIGINT UNSIGNED DEFAULT NULL            COMMENT '命中策略ID',
  `matched_strategy_type` TINYINT         DEFAULT NULL            COMMENT '命中策略类型（1-4；0=未命中）',
  `matched_rule_id`       BIGINT UNSIGNED DEFAULT NULL            COMMENT '命中规则ID',
  `final_price`           DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '最终成交价',
  `discount_info`         JSON            DEFAULT NULL            COMMENT '折扣详情快照（JSON）',
  PRIMARY KEY (`id`),
  KEY `idx_order_id`      (`order_id`),
  KEY `idx_order_item_id` (`order_item_id`),
  KEY `idx_material_id`   (`material_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='订单价格快照表';

-- ------------------------------------------------------------
-- 7) mem_recharge 充值订单表（资金入口）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `mem_recharge` (
  `id`              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `recharge_no`     VARCHAR(32)     NOT NULL                COMMENT '充值单号（唯一）',
  `mem_member_id`   BIGINT UNSIGNED NOT NULL                COMMENT '会员ID（mem_member.id）',
  `recharge_amount` DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '充值本金',
  `gift_amount`     DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '赠送金额',
  `pay_channel`     VARCHAR(20)     DEFAULT NULL            COMMENT '支付渠道（WECHAT / ALIPAY / BANK / OFFLINE）',
  `transaction_id`  VARCHAR(64)     DEFAULT NULL            COMMENT '支付流水号（唯一，幂等键）',
  `status`          TINYINT         NOT NULL DEFAULT 0      COMMENT '0-待支付 1-已支付 2-已退款 3-失败',
  `pay_time`        DATETIME        DEFAULT NULL            COMMENT '支付时间',
  `create_time`     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time`     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_recharge_no`    (`recharge_no`),
  UNIQUE KEY `uk_transaction_id` (`transaction_id`),
  KEY `idx_mem_member_id` (`mem_member_id`),
  KEY `idx_status`        (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='充值订单表';

-- ------------------------------------------------------------
-- 8) mkt_balance_log 余额流水表（余额真值台账）
--    约定：change_amount 一律记正数，方向由 change_type 决定；
--          after_balance 恒等于「操作后 mem_member.balance」。
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `mkt_balance_log` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `mem_member_id`  BIGINT UNSIGNED NOT NULL                COMMENT '会员ID（mem_member.id）',
  `change_type`    TINYINT         NOT NULL DEFAULT 1      COMMENT '1-充值 2-下单扣减 3-退款退回 4-后台调整 5-冻结 6-解冻',
  `change_amount`  DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '变动金额（正数）',
  `before_balance` DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '变动前可用余额',
  `after_balance`  DECIMAL(10,2)   NOT NULL DEFAULT 0.00   COMMENT '变动后可用余额（对账真值）',
  `related_type`   VARCHAR(30)     DEFAULT NULL            COMMENT '关联业务类型（recharge/order/refund/adjust）',
  `related_id`     BIGINT UNSIGNED DEFAULT NULL            COMMENT '关联业务ID',
  `remark`         VARCHAR(255)    DEFAULT NULL            COMMENT '备注',
  `operator_id`    BIGINT UNSIGNED DEFAULT NULL            COMMENT '操作人ID',
  `create_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_mem_member_id` (`mem_member_id`),
  KEY `idx_change_type`   (`change_type`),
  KEY `idx_related`       (`related_type`, `related_id`),
  KEY `idx_create_time`   (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='余额流水表（余额真值台账）';

-- ============================================================
-- 9) mem_member 扩展 4 列（价格策略与会员余额模块）
--    纯动态 SQL，无需 DELIMITER；列已存在则自动跳过。
-- ============================================================
SET @db = DATABASE();

-- 9.1 customer_level_id
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='customer_level_id');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `customer_level_id` BIGINT UNSIGNED DEFAULT NULL COMMENT ''关联等级ID（mem_member_level.id）''',
  'SELECT ''customer_level_id 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 9.2 balance（可用余额）
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='balance');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `balance` DECIMAL(10,2) NOT NULL DEFAULT 0.00 COMMENT ''可用余额（先充值后下单）''',
  'SELECT ''balance 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 9.3 frozen_balance（冻结余额）
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='frozen_balance');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `frozen_balance` DECIMAL(10,2) NOT NULL DEFAULT 0.00 COMMENT ''冻结余额（下单占用）''',
  'SELECT ''frozen_balance 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 9.4 total_recharge（累计充值总额，只统计本金）
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='mem_member' AND COLUMN_NAME='total_recharge');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `mem_member` ADD COLUMN `total_recharge` DECIMAL(10,2) NOT NULL DEFAULT 0.00 COMMENT ''累计充值总额（仅本金）''',
  'SELECT ''total_recharge 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ============================================================
-- 10) 初始化示例数据（可选，重复执行会跳过已存在的等级编码）
-- ============================================================
INSERT INTO `mem_member_level` (`level_name`, `level_code`, `sort_order`, `default_discount`, `description`, `status`)
SELECT '普通客户', 'NORMAL', 1, 1.0000, '默认等级，无折扣', 1
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `mem_member_level` WHERE `level_code`='NORMAL'));

INSERT INTO `mem_member_level` (`level_name`, `level_code`, `sort_order`, `default_discount`, `description`, `status`)
SELECT '银牌客户', 'SILVER', 2, 0.9500, '95 折', 1
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `mem_member_level` WHERE `level_code`='SILVER'));

INSERT INTO `mem_member_level` (`level_name`, `level_code`, `sort_order`, `default_discount`, `description`, `status`)
SELECT '金牌客户', 'GOLD', 3, 0.9000, '9 折', 1
WHERE NOT EXISTS (SELECT 1 FROM (SELECT 1) t WHERE EXISTS (SELECT 1 FROM `mem_member_level` WHERE `level_code`='GOLD'));

-- ============================================================
-- 执行后校验（可选）
-- ============================================================
-- SELECT TABLE_NAME FROM information_schema.TABLES
--  WHERE TABLE_SCHEMA=DATABASE()
--    AND TABLE_NAME IN ('mem_member_level','price_strategy','price_rule','price_rule_item',
--                       'price_change_log','order_price_snapshot','mem_recharge','mkt_balance_log');
-- SELECT COLUMN_NAME FROM information_schema.COLUMNS
--  WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mem_member'
--    AND COLUMN_NAME IN ('customer_level_id','balance','frozen_balance','total_recharge');

-- ============================================================
-- 【可选补充】mem_member 客户表 V2.0 新增 3 个字段
-- 若已建表，执行以下 ALTER；新建库可并入 mem_member 建表语句
-- ============================================================
 ALTER TABLE `mem_member`
   ADD COLUMN `customer_level_id` BIGINT UNSIGNED NOT NULL DEFAULT 0 COMMENT '【V2.0新增】客户等级ID，关联mem_member_level表' AFTER `discount_rate`,
   ADD COLUMN `frozen_balance`    DECIMAL(12,2)   NOT NULL DEFAULT 0.00 COMMENT '【V2.0新增】冻结余额（下单预扣）' AFTER `customer_level_id`,
   ADD COLUMN `total_recharge`    DECIMAL(12,2)   NOT NULL DEFAULT 0.00 COMMENT '【V2.0新增】累计充值金额（元）' AFTER `frozen_balance`,
   ADD KEY `idx_customer_level_id` (`customer_level_id`);




-- ============================================================
-- 价格策略 + 会员余额 模块 菜单数据 (sys_menu INSERT)
-- 覆盖 V2.0 新增 8 张表:
--   价格策略模块: mem_member_level / price_strategy / price_rule
--                 price_rule_item / price_change_log / order_price_snapshot
--   会员余额模块: mem_recharge / mkt_balance_log
-- 字段: id, parent_id, menu_name, path, component, icon,
--       sort_order, menu_type, permission, visible, status,
--       create_time, update_time
-- menu_type: M=目录, C=页面, F=按钮权限
-- visible: 0=显示 1=隐藏; status: 0=正常 1=停用
-- 说明: price_rule / price_rule_item 作为「价格策略」页面内的
--       明细维护功能, 不单设菜单页面, 因此 8 张表全部有归属。
-- ID 分段: 价格管理 601~605, 会员余额 701~703
-- 执行前请先 SELECT MAX(id) FROM sys_menu 确认无主键冲突
-- ============================================================


select * from sys_menu  where parent_id = 0

-- 一级菜单: 价格管理 (目录)
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(601, 0, '价格管理', 'price', NULL, 'Price', 6, '1', NULL, '1', '1', NOW(), NOW());

-- 二级菜单: 客户等级 (对应 mem_member_level)
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(602, 601, '客户等级', 'member-level', 'price/member-level/index', 'Medal', 1, '2', 'price:level:list', '1', '1', NOW(), NOW());

-- 二级菜单: 价格策略 (对应 price_strategy, 内含 price_rule / price_rule_item 明细维护)
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(603, 601, '价格策略', 'strategy', 'price/strategy/index', 'Discount', 2, '2', 'price:strategy:list', '1', '1', NOW(), NOW());

-- 二级菜单: 价格变更日志 (对应 price_change_log)
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(604, 601, '价格变更日志', 'change-log', 'price/change-log/index', 'Document', 3, '2', 'price:change-log:list', '1', '1', NOW(), NOW());

-- 二级菜单: 订单价格快照 (对应 order_price_snapshot)
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(605, 601, '订单价格快照', 'order-snapshot', 'price/order-snapshot/index', 'Tickets', 4, '2', 'price:snapshot:list', '1', '1', NOW(), NOW());

-- 一级菜单: 会员余额 (目录)
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(701, 0, '会员余额', 'member-balance', NULL, 'Wallet', 7, '1', NULL, '1', '1', NOW(), NOW());

-- 二级菜单: 充值管理 (对应 mem_recharge)
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(702, 701, '充值管理', 'recharge', 'member/recharge/index', 'CreditCard', 1, '2', 'member:recharge:list', '1', '1', NOW(), NOW());

-- 二级菜单: 余额流水 (对应 mkt_balance_log)
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(703, 701, '余额流水', 'balance-log', 'member/balance-log/index', 'Money', 2, '2', 'member:balance-log:list', '1', '1', NOW(), NOW());









-- ============================================
-- sys_operation_log 操作审计日志表
-- ============================================
CREATE TABLE `sys_operation_log` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `operator_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '操作人ID，关联sys_user表',
  `operator_name` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '操作人姓名',
  `module` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '操作模块',
  `action` VARCHAR(100) NOT NULL DEFAULT '' COMMENT '操作动作',
  `request_method` VARCHAR(10) NOT NULL DEFAULT '' COMMENT 'HTTP请求方法',
  `path` VARCHAR(255) NOT NULL DEFAULT '' COMMENT '请求接口路径',
  `request_params` TEXT DEFAULT NULL COMMENT '请求参数(JSON，敏感字段脱敏)',
  `response_result` TEXT DEFAULT NULL COMMENT '响应结果(JSON，脱敏)',
  `ip_address` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '操作IP地址',
  `user_agent` VARCHAR(500) DEFAULT NULL COMMENT '客户端User-Agent',
  `browser` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '浏览器类型',
  `os` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '操作系统',
  `execution_time` INT NOT NULL DEFAULT 0 COMMENT '接口执行耗时(毫秒)',
  `status` TINYINT NOT NULL DEFAULT 1 COMMENT '操作状态：1-成功 0-失败',
  `error_message` TEXT DEFAULT NULL COMMENT '异常堆栈信息',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '操作时间',
  PRIMARY KEY (`id`),
  KEY `idx_operator_id` (`operator_id`),
  KEY `idx_module` (`module`),
  KEY `idx_status` (`status`),
  KEY `idx_create_time` (`create_time`),
  KEY `idx_module_status_time` (`module`,`status`,`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='操作审计日志表';

-- ============================================
-- sys_login_log 登录日志表
-- ============================================
CREATE TABLE `sys_login_log` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `member_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '管理员ID，关联sys_user表',
  `member_name` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '管理员用户名',
  `login_type` TINYINT NOT NULL DEFAULT 1 COMMENT '登录方式：1-账号密码 2-微信授权 3-短信验证码',
  `ip_address` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '登录IP地址',
  `user_agent` VARCHAR(500) DEFAULT NULL COMMENT '客户端User-Agent',
  `browser` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '浏览器类型',
  `os` VARCHAR(50) NOT NULL DEFAULT '' COMMENT '操作系统',
  `login_location` VARCHAR(100) NOT NULL DEFAULT '' COMMENT '登录地点(省/市)',
  `status` TINYINT NOT NULL DEFAULT 1 COMMENT '登录状态：1-成功 0-失败',
  `fail_reason` VARCHAR(255) NOT NULL DEFAULT '' COMMENT '失败原因',
  `login_time` DATETIME DEFAULT NULL COMMENT '登录时间',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '记录创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_member_id` (`member_id`),
  KEY `idx_status` (`status`),
  KEY `idx_create_time` (`create_time`),
  KEY `idx_ip_status_time` (`ip_address`,`status`,`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='登录日志表';


















-- ============================================
-- sys_operation_log / sys_login_log 菜单表 INSERT 语句
-- 所属模块：系统管理（801 段）
-- 字段：id, parent_id, menu_name, path, component, icon,
--       sort_order, menu_type, permission, visible, status,
--       create_time, update_time
-- ============================================

-- 一级菜单：系统管理（目录）
-- INSERT INTO `sys_menu`
-- (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
-- VALUES
-- (801, 0, '运维管理', 'system', NULL, 'Setting', 8, 'M', NULL, '0', '0', NOW(), NOW());

-- 二级菜单：操作日志
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(802, 1, '操作日志', 'operation-log', 'system/operation-log/index', 'Document', 1, '2', 'system:operation:list', '1', '1', NOW(), NOW());

-- 二级菜单：登录日志
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(803, 1, '登录日志', 'login-log', 'system/login-log/index', 'Login', 2, '2', 'system:loginlog:list', '1', '1', NOW(), NOW());

-- 二级菜单：登录安全策略
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(804, 1, '登录安全策略', 'login-security', 'system/login-security/index', 'Lock', 3, '2', 'system:security:list', '1', '1', NOW(), NOW());

-- 二级菜单：登录失败锁定
INSERT INTO `sys_menu`
(`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
(805, 1, '登录失败锁定', 'login-lock', 'system/login-lock/index', 'Warning', 4, '2', 'system:lock:list', '1', '1', NOW(), NOW());









USE `jingsuding_order`;
SET @db = DATABASE();

-- ------------------------------------------------------------
-- 1) sys_user 补 fail_count（连续登录失败次数）
-- ------------------------------------------------------------
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='sys_user' AND COLUMN_NAME='fail_count');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `sys_user` ADD COLUMN `fail_count` INT NOT NULL DEFAULT 0 COMMENT ''连续登录失败次数（达到阈值锁定，成功登录后清零）'' AFTER `last_login_ip`',
  'SELECT ''sys_user.fail_count 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 2) sys_user 补 lock_until（锁定截止时间，NULL=未锁定）
-- ------------------------------------------------------------
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='sys_user' AND COLUMN_NAME='lock_until');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `sys_user` ADD COLUMN `lock_until` DATETIME NULL DEFAULT NULL COMMENT ''锁定截止时间（NULL-未锁定；达到失败阈值后 = 当前时间 + 锁定时长）'' AFTER `fail_count`',
  'SELECT ''sys_user.lock_until 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 3) sys_user 补 pwd_version（密码版本号：改密后 +1，使该用户所有已签发 JWT 失效）
-- ------------------------------------------------------------
SET @cnt = (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=@db AND TABLE_NAME='sys_user' AND COLUMN_NAME='pwd_version');
SET @sql = IF(@cnt=0,
  'ALTER TABLE `sys_user` ADD COLUMN `pwd_version` INT NOT NULL DEFAULT 0 COMMENT ''密码版本号（修改密码后+1，低于该版本的 JWT 一律拒绝，强制重新登录）'' AFTER `lock_until`',
  'SELECT ''sys_user.pwd_version 已存在，跳过'' AS skip_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 4) 新建 sys_config（系统配置表：登录安全策略等键值对配置）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `sys_config` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `config_key` VARCHAR(100) NOT NULL COMMENT '配置键（唯一）',
  `config_value` TEXT DEFAULT NULL COMMENT '配置值（JSON 或纯文本）',
  `remark` VARCHAR(255) NOT NULL DEFAULT '' COMMENT '备注说明',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_config_key` (`config_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='系统配置表';

-- 5) 登录安全策略默认配置（幂等：已存在则跳过）
--    maxLoginAttempts     连续失败锁定阈值（次）
--    lockDurationMinutes  锁定时长（分钟）
--    passwordMinLength    密码最小长度
--    passwordRequireUpper 必须含大写字母
--    passwordRequireLower 必须含小写字母
--    passwordRequireDigit 必须含数字
--    captchaEnabled       登录是否必须校验验证码
INSERT IGNORE INTO `sys_config` (`config_key`, `config_value`, `remark`)
VALUES ('security.login',
        '{"maxLoginAttempts":5,"lockDurationMinutes":30,"passwordMinLength":8,"passwordRequireUpper":true,"passwordRequireLower":true,"passwordRequireDigit":true,"captchaEnabled":true}',
        '登录安全策略（操作日志/登录日志/登录安全策略模块）');

SELECT 'system_align.sql 执行完成' AS result;










-- ============================================
-- sys_dict_type 字典类型表
-- ============================================
DROP TABLE IF EXISTS `sys_dict_type`;
CREATE TABLE `sys_dict_type` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '字典主键',
  `dict_name` VARCHAR(100) NOT NULL DEFAULT '' COMMENT '字典名称',
  `dict_type` VARCHAR(100) NOT NULL DEFAULT '' COMMENT '字典类型（唯一标识）',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-正常 1-停用',
  `create_by` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '创建者',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_by` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '更新者',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  `remark` VARCHAR(500) DEFAULT NULL COMMENT '备注',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_dict_type` (`dict_type`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='字典类型表';

-- ============================================
-- sys_dict_data 字典数据表
-- ============================================
DROP TABLE IF EXISTS `sys_dict_data`;
CREATE TABLE `sys_dict_data` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '字典编码',
  `dict_type_id` BIGINT UNSIGNED NOT NULL COMMENT '字典类型ID，关联sys_dict_type.id',
  `dict_label` VARCHAR(100) NOT NULL DEFAULT '' COMMENT '字典标签',
  `dict_value` VARCHAR(100) NOT NULL DEFAULT '' COMMENT '字典键值',
  `dict_sort` INT NOT NULL DEFAULT 0 COMMENT '字典排序',
  `css_class` VARCHAR(100) NOT NULL DEFAULT '' COMMENT '样式属性（其他样式扩展）',
  `is_default` CHAR(1) NOT NULL DEFAULT 'N' COMMENT '是否默认：Y-是 N-否',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-正常 1-停用',
  `create_by` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '创建者',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_by` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '更新者',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  `remark` VARCHAR(500) DEFAULT NULL COMMENT '备注',
  PRIMARY KEY (`id`),
  KEY `idx_dict_type_id_sort` (`dict_type_id`, `dict_sort`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='字典数据表';



-- ============================================
-- 初始化字典类型数据
-- ============================================
INSERT INTO `sys_dict_type` (`id`, `dict_name`, `dict_type`, `status`, `create_by`, `remark`)
VALUES
  (1, '系统状态', 'sys_common_status', 0, 'admin', '登录状态列表'),
  (2, '通知类型', 'sys_notice_type',   0, 'admin', '通知类型列表'),
  (3, '菜单类型', 'sys_menu_type',     0, 'admin', '菜单类型列表：M-目录 C-页面 F-按钮'),
  (4, '用户性别', 'sys_user_sex',      0, 'admin', '用户性别列表'),
  (5, '是否',     'sys_yes_no',        0, 'admin', '系统是否列表');

-- ============================================
-- 初始化字典数据
-- ============================================
INSERT INTO `sys_dict_data` (`dict_type_id`, `dict_label`, `dict_value`, `dict_sort`, `is_default`, `status`, `create_by`, `remark`)
VALUES
  -- 系统状态 (dict_type_id=1)
  (1, '正常', '0', 1, 'Y', 0, 'admin', '正常状态'),
  (1, '停用', '1', 2, 'N', 0, 'admin', '停用状态'),

  -- 通知类型 (dict_type_id=2)
  (2, '通知', '1', 1, 'Y', 0, 'admin', '普通通知'),
  (2, '公告', '2', 2, 'N', 0, 'admin', '系统公告'),

  -- 菜单类型 (dict_type_id=3)
  (3, '目录', 'M', 1, 'N', 0, 'admin', '一级/二级目录'),
  (3, '页面', 'C', 2, 'N', 0, 'admin', '功能页面'),
  (3, '按钮', 'F', 3, 'N', 0, 'admin', '操作按钮'),

  -- 用户性别 (dict_type_id=4)
  (4, '男',   '1', 1, 'N', 0, 'admin', NULL),
  (4, '女',   '2', 2, 'N', 0, 'admin', NULL),
  (4, '未知', '0', 3, 'Y', 0, 'admin', NULL),

  -- 是否 (dict_type_id=5)
  (5, '是',   'Y', 1, 'N', 0, 'admin', NULL),
  (5, '否',   'N', 2, 'Y', 0, 'admin', NULL);







-- ============================================
-- sys_menu 字典管理模块菜单
-- ============================================
-- 一级目录：字典管理
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (901, 0, '字典管理', 'dict', NULL, 'dict', 9, '1', NULL, 0, 0, NOW(), NOW());

-- 二级页面：字典类型
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (902, 901, '字典类型', 'type', 'dict/type/index', 'dict', 1, '2', 'dict:type:list', 1, 1, NOW(), NOW());

-- 二级页面：字典数据
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (903, 901, '字典数据', 'data', 'dict/data/index', 'dict', 2, '2', 'dict:data:list', 1, 1, NOW(), NOW());



-- ============================================
-- sys_config 系统配置表
-- ============================================
DROP TABLE IF EXISTS `sys_config`;
CREATE TABLE `sys_config` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '配置主键',
  `config_name` VARCHAR(100) NOT NULL DEFAULT '' COMMENT '配置名称',
  `config_key` VARCHAR(100) NOT NULL DEFAULT '' COMMENT '配置键名（唯一标识，代码中通过此键取值）',
  `config_value` VARCHAR(500) NOT NULL DEFAULT '' COMMENT '配置值',
  `config_type` CHAR(1) NOT NULL DEFAULT 'N' COMMENT '配置类型：Y-系统内置（不可删除） N-自定义',
  `sort_order` INT NOT NULL DEFAULT 0 COMMENT '排序',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0-正常 1-停用',
  `create_by` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '创建者',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_by` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '更新者',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  `remark` VARCHAR(500) DEFAULT NULL COMMENT '备注',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_config_key` (`config_key`),
  KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='系统配置表';





-- ============================================================
-- 系统管理 - 系统配置模块 初始化脚本
--
--   1) sys_config 种子配置（security.login，系统内置 Y）
--   2) sys_menu 菜单与按钮权限（811 页面 + 812~816 按钮）
--
-- 幂等：可重复执行，已存在自动跳过，不动任何已有数据。
-- 执行：mysql -uroot -p jingsuding_order --default-character-set=utf8mb4 < sys_config_init.sql
-- ============================================================

USE `jingsuding_order`;

-- ------------------------------------------------------------
-- 1) 种子配置：登录安全策略（security.login）
--    为什么标成系统内置(Y)：被 SecurityService 依赖，删掉会让登录
--    安全策略整体失效（验证码/失败锁定阈值全部回落默认值），必须保护起来。
--    值与 SecurityConfigDto 的默认值一致，播种后登录行为零变化。
-- ------------------------------------------------------------
INSERT IGNORE INTO `sys_config`
  (`config_name`, `config_key`, `config_value`, `config_type`, `sort_order`, `status`, `create_by`, `update_by`, `remark`)
VALUES
  ('登录安全策略', 'security.login',
   '{"maxLoginAttempts":5,"lockDurationMinutes":30,"passwordMinLength":8,"passwordRequireUpper":true,"passwordRequireLower":true,"passwordRequireDigit":true,"captchaEnabled":true,"captchaExpireMinutes":5,"captchaLength":4,"autoUnlock":true,"passwordRequireSpecial":false,"passwordValidDays":0,"tokenExpireHours":24,"allowConcurrentLogin":false,"maxConcurrentSessions":1}',
   'Y', 1, 0, 'system', 'system', '登录安全策略（失败锁定阈值/时长、密码强度、验证码开关），由 SecurityService 读取');

-- ------------------------------------------------------------
-- 2) 菜单：系统配置（挂在「系统管理」id=1 下，排序 5）
-- ------------------------------------------------------------
INSERT IGNORE INTO `sys_menu`
  (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
  (811, 1, '系统配置', 'config', 'system/config/index', 'tool', 5, '2', 'system:config:list', 1, 1, NOW(), NOW());

-- ------------------------------------------------------------
-- 3) 按钮权限（menu_type=3，不生成路由，只用于前端 v-permission）
-- ------------------------------------------------------------
INSERT IGNORE INTO `sys_menu`
  (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
  (812, 811, '新增',   '', NULL, NULL, 1, '3', 'system:config:create', 1, 1, NOW(), NOW()),
  (813, 811, '修改',   '', NULL, NULL, 2, '3', 'system:config:update', 1, 1, NOW(), NOW()),
  (814, 811, '删除',   '', NULL, NULL, 3, '3', 'system:config:delete', 1, 1, NOW(), NOW()),
  (815, 811, '刷新缓存', '', NULL, NULL, 4, '3', 'system:config:cache', 1, 1, NOW(), NOW()),
  (816, 811, '清空缓存', '', NULL, NULL, 5, '3', 'system:config:cache', 1, 1, NOW(), NOW());

SELECT 'sys_config_init.sql 执行完成' AS result;





-- ============================================
-- 定时任务中心 - 建表SQL和菜单SQL语句
-- 基于 Quartz.NET 调度框架 + 应用业务表
-- ============================================

-- ============================================
-- 第一部分：Quartz.NET 框架层建表（11张）
-- ============================================

-- 1. QRTZ_JOB_DETAILS - 任务详情
DROP TABLE IF EXISTS `QRTZ_JOB_DETAILS`;
CREATE TABLE `QRTZ_JOB_DETAILS` (
  `SCHED_NAME` VARCHAR(120) NOT NULL COMMENT '调度器名称',
  `JOB_NAME` VARCHAR(200) NOT NULL COMMENT '任务名称',
  `JOB_GROUP` VARCHAR(200) NOT NULL COMMENT '任务组名',
  `DESCRIPTION` VARCHAR(250) DEFAULT NULL COMMENT '任务描述',
  `JOB_CLASS_NAME` VARCHAR(250) NOT NULL COMMENT '任务类名',
  `IS_DURABLE` VARCHAR(1) NOT NULL DEFAULT '0' COMMENT '是否持久化：0-否 1-是',
  `IS_NONCONCURRENT` VARCHAR(1) NOT NULL DEFAULT '0' COMMENT '是否并发执行：0-否 1-是',
  `IS_UPDATE_DATA` VARCHAR(1) NOT NULL DEFAULT '0' COMMENT '是否更新数据',
  `REQUESTS_RECOVERY` VARCHAR(1) NOT NULL DEFAULT '0' COMMENT '是否请求恢复',
  `JOB_DATA` BLOB DEFAULT NULL COMMENT '任务数据',
  PRIMARY KEY (`SCHED_NAME`, `JOB_NAME`, `JOB_GROUP`),
  KEY `IDX_QRTZ_J_REQ_RECOVERY` (`SCHED_NAME`, `REQUESTS_RECOVERY`),
  KEY `IDX_QRTZ_J_GRP` (`SCHED_NAME`, `JOB_GROUP`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Quartz任务详情表';

-- 2. QRTZ_TRIGGERS - 触发器基本信息
DROP TABLE IF EXISTS `QRTZ_TRIGGERS`;
CREATE TABLE `QRTZ_TRIGGERS` (
  `SCHED_NAME` VARCHAR(120) NOT NULL COMMENT '调度器名称',
  `TRIGGER_NAME` VARCHAR(200) NOT NULL COMMENT '触发器名称',
  `TRIGGER_GROUP` VARCHAR(200) NOT NULL COMMENT '触发器组名',
  `JOB_NAME` VARCHAR(200) NOT NULL COMMENT '关联任务名称',
  `JOB_GROUP` VARCHAR(200) NOT NULL COMMENT '关联任务组名',
  `DESCRIPTION` VARCHAR(250) DEFAULT NULL COMMENT '触发器描述',
  `NEXT_FIRE_TIME` BIGINT(13) DEFAULT NULL COMMENT '下次触发时间',
  `PREV_FIRE_TIME` BIGINT(13) DEFAULT NULL COMMENT '上次触发时间',
  `PRIORITY` INT DEFAULT NULL COMMENT '优先级',
  `TRIGGER_STATE` VARCHAR(16) NOT NULL COMMENT '触发器状态：WAITING-等待 RUNNING-运行 WAITING_AFTER_ERROR-错误后等待',
  `TRIGGER_TYPE` VARCHAR(8) NOT NULL DEFAULT 'CRON' COMMENT '触发器类型：SIMPLE-简单 CRON-Cron表达式',
  `START_TIME` BIGINT(13) NOT NULL COMMENT '开始时间',
  `END_TIME` BIGINT(13) DEFAULT NULL COMMENT '结束时间',
  `INSTANCE_NAME` VARCHAR(200) DEFAULT NULL COMMENT '实例名称',
  `JOB_KEY` VARCHAR(300) DEFAULT NULL COMMENT '任务键',
  PRIMARY KEY (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`),
  KEY `IDX_QRTZ_T_J` (`SCHED_NAME`, `JOB_NAME`, `JOB_GROUP`),
  KEY `IDX_QRTZ_T_JG` (`SCHED_NAME`, `JOB_GROUP`),
  KEY `IDX_QRTZ_T_G` (`SCHED_NAME`, `TRIGGER_GROUP`),
  KEY `IDX_QRTZ_T_S` (`SCHED_NAME`, `TRIGGER_STATE`),
  KEY `IDX_QRTZ_T_S_STATE` (`SCHED_NAME`, `TRIGGER_STATE`, `INSTANCE_NAME`),
  KEY `IDX_QRTZ_T_NEXT_FT` (`SCHED_NAME`, `NEXT_FIRE_TIME`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Quartz触发器表';

-- 3. QRTZ_SIMPLE_TRIGGERS - 简单触发器
DROP TABLE IF EXISTS `QRTZ_SIMPLE_TRIGGERS`;
CREATE TABLE `QRTZ_SIMPLE_TRIGGERS` (
  `SCHED_NAME` VARCHAR(120) NOT NULL COMMENT '调度器名称',
  `TRIGGER_NAME` VARCHAR(200) NOT NULL COMMENT '触发器名称',
  `TRIGGER_GROUP` VARCHAR(200) NOT NULL COMMENT '触发器组名',
  `REPEAT_COUNT` INT(11) NOT NULL COMMENT '重复次数',
  `REPEAT_INTERVAL` BIGINT(13) NOT NULL COMMENT '重复间隔',
  `TIMES_TRIGGERED` INT(10) UNSIGNED NOT NULL COMMENT '已触发次数',
  PRIMARY KEY (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`),
  CONSTRAINT `QRTZ_SIMPLE_TRIGGERS_ibfk_1` FOREIGN KEY (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`)
    REFERENCES `QRTZ_TRIGGERS` (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Quartz简单触发器表';

-- 4. QRTZ_CRON_TRIGGERS - Cron触发器
DROP TABLE IF EXISTS `QRTZ_CRON_TRIGGERS`;
CREATE TABLE `QRTZ_CRON_TRIGGERS` (
  `SCHED_NAME` VARCHAR(120) NOT NULL COMMENT '调度器名称',
  `TRIGGER_NAME` VARCHAR(200) NOT NULL COMMENT '触发器名称',
  `TRIGGER_GROUP` VARCHAR(200) NOT NULL COMMENT '触发器组名',
  `CRON_EXPRESSION` VARCHAR(200) NOT NULL COMMENT 'Cron表达式',
  `TIME_ZONE_ID` VARCHAR(80) DEFAULT NULL COMMENT '时区ID',
  PRIMARY KEY (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`),
  CONSTRAINT `QRTZ_CRON_TRIGGERS_ibfk_1` FOREIGN KEY (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`)
    REFERENCES `QRTZ_TRIGGERS` (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Quartz Cron触发器表';

-- 5. QRTZ_SIMPROP_TRIGGERS - 高级触发器
DROP TABLE IF EXISTS `QRTZ_SIMPROP_TRIGGERS`;
CREATE TABLE `QRTZ_SIMPROP_TRIGGERS` (
  `SCHED_NAME` VARCHAR(120) NOT NULL COMMENT '调度器名称',
  `TRIGGER_NAME` VARCHAR(200) NOT NULL COMMENT '触发器名称',
  `TRIGGER_GROUP` VARCHAR(200) NOT NULL COMMENT '触发器组名',
  `STR_PROP_1` VARCHAR(512) DEFAULT NULL COMMENT '字符串属性1',
  `STR_PROP_2` VARCHAR(512) DEFAULT NULL COMMENT '字符串属性2',
  `STR_PROP_3` VARCHAR(512) DEFAULT NULL COMMENT '字符串属性3',
  `INT_PROP_1` INT DEFAULT NULL COMMENT '整数属性1',
  `INT_PROP_2` INT DEFAULT NULL COMMENT '整数属性2',
  `LONG_PROP_1` BIGINT DEFAULT NULL COMMENT '长整数属性1',
  `LONG_PROP_2` BIGINT DEFAULT NULL COMMENT '长整数属性2',
  `DEC_PROP_1` DECIMAL(13,4) DEFAULT NULL COMMENT '小数属性1',
  `DEC_PROP_2` DECIMAL(13,4) DEFAULT NULL COMMENT '小数属性2',
  `BOOL_PROP_1` VARCHAR(1) DEFAULT NULL COMMENT '布尔属性1',
  `BOOL_PROP_2` VARCHAR(1) DEFAULT NULL COMMENT '布尔属性2',
  PRIMARY KEY (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`),
  CONSTRAINT `QRTZ_SIMPROP_TRIGGERS_ibfk_1` FOREIGN KEY (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`)
    REFERENCES `QRTZ_TRIGGERS` (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Quartz高级触发器表';


-- 6. QRTZ_BLOB_TRIGGERS - 二进制触发器
DROP TABLE IF EXISTS `QRTZ_BLOB_TRIGGERS`;
CREATE TABLE `QRTZ_BLOB_TRIGGERS` (
  `SCHED_NAME` VARCHAR(120) NOT NULL COMMENT '调度器名称',
  `TRIGGER_NAME` VARCHAR(200) NOT NULL COMMENT '触发器名称',
  `TRIGGER_GROUP` VARCHAR(200) NOT NULL COMMENT '触发器组名',
  `BLOB_DATA` BLOB DEFAULT NULL COMMENT '二进制数据',
  PRIMARY KEY (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`),
  CONSTRAINT `QRTZ_BLOB_TRIGGERS_ibfk_1` FOREIGN KEY (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`)
    REFERENCES `QRTZ_TRIGGERS` (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Quartz二进制触发器表';



-- 7. QRTZ_FIRED_TRIGGERS - 正在执行的触发器
DROP TABLE IF EXISTS `QRTZ_FIRED_TRIGGERS`;
CREATE TABLE `QRTZ_FIRED_TRIGGERS` (
  `SCHED_NAME` VARCHAR(120) NOT NULL COMMENT '调度器名称',
  `ENTRY_ID` VARCHAR(95) NOT NULL COMMENT '执行入口ID',
  `TRIGGER_NAME` VARCHAR(200) NOT NULL COMMENT '触发器名称',
  `TRIGGER_GROUP` VARCHAR(200) NOT NULL COMMENT '触发器组名',
  `INSTANCE_NAME` VARCHAR(200) DEFAULT NULL COMMENT '实例名称',
  `FIRED_TIME` BIGINT(13) NOT NULL COMMENT '触发时间',
  `SCHED_TIME` BIGINT(13) NOT NULL COMMENT '调度时间',
  `PRIORITY` INT NOT NULL COMMENT '优先级',
  `STATE` VARCHAR(16) NOT NULL COMMENT '执行状态',
  `JOB_NAME` VARCHAR(200) DEFAULT NULL COMMENT '任务名称',
  `JOB_GROUP` VARCHAR(200) DEFAULT NULL COMMENT '任务组名',
  `IS_NONCONCURRENT` VARCHAR(1) DEFAULT NULL COMMENT '是否非并发',
  `REQUESTS_RECOVERY` VARCHAR(1) DEFAULT NULL COMMENT '是否请求恢复',
  PRIMARY KEY (`SCHED_NAME`, `ENTRY_ID`),
  KEY `IDX_QRTZ_FT_TRIG_INST_NAME` (`SCHED_NAME`, `INSTANCE_NAME`),
  KEY `IDX_QRTZ_FT_INST_JOB_REQ_RCVRY` (`SCHED_NAME`, `INSTANCE_NAME`, `REQUESTS_RECOVERY`),
  KEY `IDX_QRTZ_FT_J_G` (`SCHED_NAME`, `JOB_NAME`, `JOB_GROUP`),
  KEY `IDX_QRTZ_FT_JG` (`SCHED_NAME`, `JOB_GROUP`),
  KEY `IDX_QRTZ_FT_T_G` (`SCHED_NAME`, `TRIGGER_NAME`, `TRIGGER_GROUP`),
  KEY `IDX_QRTZ_FT_TG` (`SCHED_NAME`, `TRIGGER_GROUP`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Quartz正在执行的触发器表';

-- 8. QRTZ_PAUSED_TRIGGER_GRPS - 暂停的触发器组
DROP TABLE IF EXISTS `QRTZ_PAUSED_TRIGGER_GRPS`;
CREATE TABLE `QRTZ_PAUSED_TRIGGER_GRPS` (
  `SCHED_NAME` VARCHAR(120) NOT NULL COMMENT '调度器名称',
  `TRIGGER_GROUP` VARCHAR(200) NOT NULL COMMENT '触发器组名',
  PRIMARY KEY (`SCHED_NAME`, `TRIGGER_GROUP`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Quartz暂停的触发器组表';

-- 9. QRTZ_CALENDARS - 日历表
DROP TABLE IF EXISTS `QRTZ_CALENDARS`;
CREATE TABLE `QRTZ_CALENDARS` (
  `SCHED_NAME` VARCHAR(120) NOT NULL COMMENT '调度器名称',
  `CALENDAR_NAME` VARCHAR(200) NOT NULL COMMENT '日历名称',
  `CALENDAR` BLOB NOT NULL COMMENT '日历数据',
  PRIMARY KEY (`SCHED_NAME`, `CALENDAR_NAME`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Quartz日历表';

-- 10. QRTZ_SCHEDULER_STATE - 调度器状态
DROP TABLE IF EXISTS `QRTZ_SCHEDULER_STATE`;
CREATE TABLE `QRTZ_SCHEDULER_STATE` (
  `SCHED_NAME` VARCHAR(120) NOT NULL COMMENT '调度器名称',
  `INSTANCE_NAME` VARCHAR(200) NOT NULL COMMENT '实例名称',
  `LAST_CHECKIN_TIME` BIGINT(13) NOT NULL COMMENT '最后签到时间',
  `CHECKIN_INTERVAL` BIGINT(13) NOT NULL COMMENT '签到间隔',
  PRIMARY KEY (`SCHED_NAME`, `INSTANCE_NAME`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Quartz调度器状态表';

-- 11. QRTZ_LOCKS - 分布式锁
DROP TABLE IF EXISTS `QRTZ_LOCKS`;
CREATE TABLE `QRTZ_LOCKS` (
  `SCHED_NAME` VARCHAR(120) NOT NULL COMMENT '调度器名称',
  `LOCK_NAME` VARCHAR(40) NOT NULL COMMENT '锁名称',
  PRIMARY KEY (`SCHED_NAME`, `LOCK_NAME`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Quartz分布式锁表';

-- ============================================
-- 第二部分：应用业务层建表（2张）
-- ============================================

-- 12. sys_job - 定时任务定义表
DROP TABLE IF EXISTS `sys_job`;
CREATE TABLE `sys_job` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '任务主键',
  `job_name` VARCHAR(100) NOT NULL DEFAULT '' COMMENT '任务名称',
  `job_group` VARCHAR(100) NOT NULL DEFAULT 'DEFAULT' COMMENT '任务分组',
  `job_type` VARCHAR(200) NOT NULL DEFAULT '' COMMENT '任务类型（全限定类名，反射调用）',
  `cron_expression` VARCHAR(100) NOT NULL DEFAULT '' COMMENT 'Cron表达式',
  `job_params` VARCHAR(500) DEFAULT '' COMMENT '任务参数（JSON格式）',
  `status` TINYINT NOT NULL DEFAULT 0 COMMENT '任务状态：0-停止 1-运行中',
  `misfire_policy` TINYINT NOT NULL DEFAULT 1 COMMENT '丢失策略：0-立即执行 1-执行一次 2-忽略',
  `concurrent` TINYINT NOT NULL DEFAULT 0 COMMENT '是否允许并发：0-禁止 1-允许',
  `timeout` INT NOT NULL DEFAULT 0 COMMENT '任务超时时间（秒），0=不限制',
  `retry_count` TINYINT NOT NULL DEFAULT 0 COMMENT '失败重试次数',
  `retry_interval` INT NOT NULL DEFAULT 0 COMMENT '失败重试间隔（秒）',
  `create_by` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '创建者',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_by` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '更新者',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  `remark` VARCHAR(500) DEFAULT NULL COMMENT '备注',
  PRIMARY KEY (`id`),
  KEY `idx_job_group` (`job_group`),
  KEY `idx_status` (`status`),
  KEY `idx_job_name` (`job_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='定时任务定义表';

-- 13. sys_job_log - 任务执行日志表
DROP TABLE IF EXISTS `sys_job_log`;
CREATE TABLE `sys_job_log` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '日志主键',
  `job_id` BIGINT UNSIGNED DEFAULT NULL COMMENT '关联任务ID',
  `job_name` VARCHAR(100) NOT NULL DEFAULT '' COMMENT '任务名称',
  `job_group` VARCHAR(100) NOT NULL DEFAULT 'DEFAULT' COMMENT '任务分组',
  `trigger_name` VARCHAR(200) DEFAULT '' COMMENT '触发器名称',
  `trigger_group` VARCHAR(200) DEFAULT '' COMMENT '触发器组名',
  `fire_time` DATETIME NOT NULL COMMENT '触发时间',
  `start_time` DATETIME NOT NULL COMMENT '开始执行时间',
  `end_time` DATETIME DEFAULT NULL COMMENT '结束执行时间',
  `duration` BIGINT UNSIGNED NOT NULL DEFAULT 0 COMMENT '执行耗时（毫秒）',
  `status` TINYINT NOT NULL DEFAULT 1 COMMENT '执行状态：1-成功 0-失败',
  `execute_result` TEXT DEFAULT NULL COMMENT '执行结果',
  `error_message` TEXT DEFAULT NULL COMMENT '异常堆栈信息',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '记录创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_job_id` (`job_id`),
  KEY `idx_job_group` (`job_group`),
  KEY `idx_status` (`status`),
  KEY `idx_fire_time` (`fire_time`),
  KEY `idx_job_id_status_time` (`job_id`, `status`, `create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='任务执行日志表';

-- ============================================
-- 第三部分：菜单表 INSERT 语句（ID 1101 段）
-- ============================================

-- 一级目录：定时任务
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (1101, 0, '定时任务', 'task', NULL, 'job', 10, '1', NULL, 1, 1, NOW(), NOW());

-- 二级页面：任务管理
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (1102, 1101, '任务管理', 'job', 'task/job/index', 'job', 1, '2', 'task:job:list', 1, 1, NOW(), NOW());

-- 按钮权限
INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (1103, 1102, '新增任务', NULL, NULL, NULL, 1, '3', 'task:job:add', 1, 1, NOW(), NOW());

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (1104, 1102, '编辑任务', NULL, NULL, NULL, 2, '3', 'task:job:edit', 1, 1, NOW(), NOW());

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (1105, 1102, '删除任务', NULL, NULL, NULL, 3, '3', 'task:job:remove', 1, 1, NOW(), NOW());

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (1106, 1102, '启动任务', NULL, NULL, NULL, 4, '3', 'task:job:start', 1, 1, NOW(), NOW());

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (1107, 1102, '停止任务', NULL, NULL, NULL, 5, '3', 'task:job:stop', 1, 1, NOW(), NOW());

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (1108, 1102, '立即执行', NULL, NULL, NULL, 6, '3', 'task:job:run', 1, 1, NOW(), NOW());

INSERT INTO `sys_menu` (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES (1109, 1102, '执行日志', NULL, NULL, NULL, 7, '3', 'task:job:log', 1, 1, NOW(), NOW());

-- ============================================
-- 第四部分：初始化定时任务数据（可选执行）
-- ============================================
INSERT INTO `sys_job` (
  `id`, `job_name`, `job_group`, `job_type`, `cron_expression`, `job_params`, 
  `status`, `misfire_policy`, `concurrent`, `timeout`, `retry_count`, `retry_interval`, 
  `create_by`, `create_time`, `update_by`, `update_time`, `remark`
) VALUES
(1, '登录日志清理任务', 'DEFAULT', 'Quartz.Net.Jobs.LogCleanJob, QuartzNetDemo', '0 0 0 * * ?', '{"logType":"login","retainDays":90}', 0, 1, 0, 0, 0, 0, 'admin', NOW(), '', NOW(), '每日凌晨0点清理登录日志'),
(2, '操作日志清理任务', 'DEFAULT', 'Quartz.Net.Jobs.LogCleanJob, QuartzNetDemo', '0 0 0 * * ?', '{"logType":"operation","retainDays":90}', 0, 1, 0, 0, 0, 0, 'admin', NOW(), '', NOW(), '每日凌晨0点清理操作日志'),
(3, '缓存预热任务', 'DEFAULT', 'Quartz.Net.Jobs.CacheWarmupJob, QuartzNetDemo', '0 0 8 * * ?', '', 0, 1, 0, 0, 0, 0, 'admin', NOW(), '', NOW(), '每日早上8点预热缓存'),
(4, 'Token清理任务', 'DEFAULT', 'Quartz.Net.Jobs.TokenCleanJob, QuartzNetDemo', '0 0 * * * ?', '', 0, 1, 0, 0, 0, 0, 'admin', NOW(), '', NOW(), '每小时清理过期Token'),
(5, '系统健康检查任务', 'DEFAULT', 'Quartz.Net.Jobs.HealthCheckJob, QuartzNetDemo', '0 0/30 * * * ?', '', 0, 1, 30, 0, 0, 0, 'admin', NOW(), '', NOW(), '每30秒执行系统健康检查');




-- ============================================================
-- 自动任务模块 —— Quartz 表结构补齐脚本
--
-- ⚠️ 幂等性由执行器保证（会先查 information_schema 再决定要不要 ALTER）。
--    在 MySQL 客户端里手工执行时：若提示 Duplicate column name / Duplicate key name，
--    说明这一条已经加过了，跳过即可，脚本不会破坏已有数据。
--
-- 背景（以真实库为准，需求文档没提到这回事）：
-- 库里现有的 qrtz_* 11 张表是按 Quartz 1.x / 2.0 的老 schema 建的，缺两个列：
--   1) MISFIRE_INSTR  —— Quartz 2.2+ 起必填
--      缺它 → 调度器启动阶段恢复作业失败：
--      "Couldn't recover jobs: Unknown column 'MISFIRE_INSTR' in 'where clause'"
--      症状是「应用照常启动，但所有定时任务都不跑」，最难查的一类。
--   2) CALENDAR_NAME  —— Quartz.NET（.NET 移植版）特有，Java 版 qrtz_triggers 没有
--      Quartz 3.12 的 trigger 落库语句是
--        UPDATE QRTZ_TRIGGERS SET ... CALENDAR_NAME = @triggerCalendarName, MISFIRE_INSTR = ...
--      检索 / 恢复 trigger 时同样带上它。缺它 →
--      "Couldn't retrieve trigger: Unknown column 'CALENDAR_NAME' in 'field list'"
--      症状是「新增成功、列表正常，一点启动就 400」。
-- 本脚本只加列 / 加索引，不删任何东西。
-- ============================================================

SET NAMES utf8mb4;

-- 1) qrtz_triggers 补 MISFIRE_INSTR
ALTER TABLE `qrtz_triggers`
  ADD COLUMN `MISFIRE_INSTR` SMALLINT(6) NOT NULL DEFAULT 0
  AFTER `END_TIME`;

-- 2) qrtz_triggers 补 CALENDAR_NAME（Quartz.NET 特有列）
ALTER TABLE `qrtz_triggers`
  ADD COLUMN `CALENDAR_NAME` VARCHAR(200) NULL DEFAULT NULL
  AFTER `END_TIME`;

-- 3) qrtz_triggers 补 JOB_DATA
--    Quartz.NET 把 JobDataMap（任务参数）存在 trigger 上而不是 job 上，所以这一列必须有。
--    缺它 → "Couldn't retrieve trigger: Unknown column 'JOB_DATA' in 'field list'"。
--    用 MEDIUMTEXT：JSON 参数不会超，也不像 BLOB 那样有二进制边界问题。
ALTER TABLE `qrtz_triggers`
  ADD COLUMN `JOB_DATA` MEDIUMTEXT NULL DEFAULT NULL
  AFTER `PRIORITY`;

-- 4) ⚠️ 反悔：qrtz_simple_triggers 不能带 MISFIRE_INSTR
--    Quartz 3.12 读取 trigger 时会执行
--      SELECT ... MISFIRE_INSTR ... FROM QRTZ_TRIGGERS t
--        LEFT JOIN QRTZ_SIMPLE_TRIGGERS st ON ...
--        LEFT JOIN QRTZ_CRON_TRIGGERS  ct ON ...
--    两张表都有 MISFIRE_INSTR 就会报
--      "Column 'MISFIRE_INSTR' in field list is ambiguous"
--    而 Quartz.NET 写 simple trigger 的语句是
--      INSERT INTO QRTZ_SIMPLE_TRIGGERS (..., REPEAT_COUNT, REPEAT_INTERVAL, TIMES_TRIGGERED)
--    压根没有这一列（只有 Java 版才有），所以 simple_triggers 保持原样就够。
ALTER TABLE `qrtz_simple_triggers`
  DROP COLUMN `MISFIRE_INSTR`;

-- 5) qrtz_job_details / qrtz_fired_triggers 的 IS_* 标志列改成 TINYINT(1)
--    Quartz 是拿 DbType.Boolean 去读写 IS_DURABLE / IS_NONCONCURRENT / IS_UPDATE_DATA /
--    REQUESTS_RECOVERY 的，库里这四列却是 VARCHAR(1)（老 schema 的写法，存字符串 '1'），
--    MySqlConnector 用 GetBoolean 读字符串会直接抛
--      "Couldn't retrieve job: String '1' was not recognized as a valid Boolean."
--    症状是「新增成功、列表正常，一启动就 400」。改成 TINYINT(1) 才是 Quartz 想要的形态。
ALTER TABLE `qrtz_job_details`
  MODIFY COLUMN `IS_DURABLE` TINYINT(1) NOT NULL DEFAULT 0,
  MODIFY COLUMN `IS_NONCONCURRENT` TINYINT(1) NOT NULL DEFAULT 0,
  MODIFY COLUMN `IS_UPDATE_DATA` TINYINT(1) NOT NULL DEFAULT 0,
  MODIFY COLUMN `REQUESTS_RECOVERY` TINYINT(1) NOT NULL DEFAULT 0;

ALTER TABLE `qrtz_fired_triggers`
  MODIFY COLUMN `IS_NONCONCURRENT` TINYINT(1) NULL DEFAULT NULL,
  MODIFY COLUMN `REQUESTS_RECOVERY` TINYINT(1) NULL DEFAULT NULL;

-- 6) sys_job_log 补索引：日志页按 job_id + create_time 范围筛选，全表扫会随数据量劣化
ALTER TABLE `sys_job_log`
  ADD INDEX `idx_job_log_job_id` (`job_id`),
  ADD INDEX `idx_job_log_create_time` (`create_time`);





-- ============================================================
-- 微信支付 主动查单与补单 模块 增量脚本（已有库专用，纯增量、幂等）
-- 执行前请在 mysql 客户端加 --default-character-set=utf8mb4
--   "C:/Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -uroot -p123456 --default-character-set=utf8mb4 jingsuding_order < payment_align.sql
-- ============================================================

-- ------------------------------------------------------------
-- 1. 支付单表 payment_order（CREATE TABLE IF NOT EXISTS，可重复执行）
--    状态机：INIT(0) → PAYING(1) → SUCCESS(2) / FAIL(3) / CLOSED(4)
--    ⚠️ out_trade_no 唯一索引：拦截重复支付请求的最后一道防线
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `payment_order` (
  `id`             BIGINT          NOT NULL AUTO_INCREMENT                COMMENT '支付单ID（主键）',
  `biz_order_id`   BIGINT          NOT NULL DEFAULT 0                     COMMENT '关联业务订单ID（如 trx_order.id，支付成功后推进业务订单）',
  `out_trade_no`   VARCHAR(64)     NOT NULL                               COMMENT '商户订单号（业务系统生成，唯一，拦截重复请求）',
  `transaction_id` VARCHAR(64)     NULL                                   COMMENT '微信交易号（支付成功后回填；未支付为空）',
  `amount`         BIGINT          NOT NULL DEFAULT 0                     COMMENT '支付金额（单位：分，与微信 amount.total 同量纲）',
  `pay_channel`    TINYINT         NOT NULL DEFAULT 1                     COMMENT '支付渠道：1-JSAPI 2-APP 3-Native 4-H5',
  `status`         TINYINT         NOT NULL DEFAULT 0                     COMMENT '支付状态：0-INIT 1-PAYING 2-SUCCESS 3-FAIL 4-CLOSED',
  `expire_time`    DATETIME        NOT NULL                               COMMENT '关单/超时时间（超过后定时任务关单）',
  `create_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP     COMMENT '创建时间',
  `update_time`    DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_out_trade_no` (`out_trade_no`),
  UNIQUE KEY `uk_transaction_id` (`transaction_id`),
  KEY `idx_biz_order_id` (`biz_order_id`),
  KEY `idx_status` (`status`),
  KEY `idx_expire_time` (`expire_time`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='支付单表（微信支付主动查单与补单模块）';

-- ------------------------------------------------------------
-- 2. 微信支付回调日志表 pay_callback_log
--    每次微信回调都原样落库请求体 JSON，便于排查「掉单/重复推送/验签失败」
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `pay_callback_log` (
  `id`              BIGINT          NOT NULL AUTO_INCREMENT                COMMENT '记录ID（主键）',
  `request_body`    TEXT                                                    COMMENT '回调请求体原始 JSON（排查问题用）',
  `transaction_id`  VARCHAR(64)     NULL                                   COMMENT '微信订单号（解密后；验签失败则为空）',
  `handle_status`   TINYINT         NOT NULL DEFAULT 0                     COMMENT '处理状态：0-待处理 1-成功 2-失败 3-已忽略(幂等)',
  `response_result` TEXT                                                   COMMENT '响应结果（SUCCESS / 失败原因 / 幂等说明）',
  `create_time`     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP     COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_transaction_id` (`transaction_id`),
  KEY `idx_handle_status` (`handle_status`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='微信支付回调日志表';

-- ------------------------------------------------------------
-- 3. 注册定时任务：CheckPendingPaymentJob（每 5 分钟主动查单/补单/关单）
--    job_type 用全限定类名（JobService.ResolveJobType 也支持只写类名自动补程序集名）。
--    cron = 0 0/5 * * * ?（Quartz 7 位含秒语法，每 5 分钟）。
--    concurrent=0 禁止并发（避免重复查单放大微信 API 压力）；status=1 启动即运行。
-- ------------------------------------------------------------
INSERT IGNORE INTO `sys_job`
  (`job_name`, `job_group`, `job_type`, `cron_expression`, `job_params`, `status`, `misfire_policy`, `concurrent`, `timeout`, `retry_count`, `retry_interval`, `create_by`, `update_by`, `remark`)
VALUES
  ('微信支付待支付查单任务', 'DEFAULT', 'Jsd.Api.Jobs.CheckPendingPaymentJob', '0 0/5 * * * ?', NULL, 1, 1, 0, 300, 0, 0, 'system', 'system',
   '每5分钟扫描 payment_order 中 PAYING 订单：未超时逐单调微信查单补单；已超时且微信未支付则关单');

-- ------------------------------------------------------------
-- 验证
-- ------------------------------------------------------------
SELECT 'payment_order / pay_callback_log 已就绪' AS result, COUNT(*) AS job_count
FROM sys_job WHERE job_type = 'Jsd.Api.Jobs.CheckPendingPaymentJob';



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








CREATE TABLE IF NOT EXISTS `mkt_banner` (
  `id`          BIGINT       NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `title`       VARCHAR(100) DEFAULT NULL            COMMENT '轮播标题（图片上叠加展示）',
  `image_url`   VARCHAR(512) NOT NULL                COMMENT '图片完整访问URL（必填）',
  `path`        VARCHAR(255) DEFAULT NULL            COMMENT '点击跳转的小程序页面路径，为空不跳转',
  `sort`        INT          NOT NULL DEFAULT 0      COMMENT '排序号，越小越靠前',
  `status`      TINYINT      NOT NULL DEFAULT 1      COMMENT '状态：1-启用 0-禁用',
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `update_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_status_sort` (`status`, `sort`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='首页轮播图运营位';

-- ============================================================
-- 示例数据（image_url 请换成你自己能访问的图片地址，
--   小程序真机需在「小程序后台-开发管理-服务器域名」配置 downloadFile 合法域名；
--   开发者工具勾选「不校验合法域名」时可直接调试）
-- ============================================================

INSERT INTO `mkt_banner` (`title`, `image_url`, `path`, `sort`, `status`) VALUES
  ('镜片专区', 'https://via.placeholder.com/750x340.png?text=Lens', '/pages/category/list', 1, 1),
  ('新品上架', 'https://via.placeholder.com/750x340.png?text=New',  '/pages/category/list', 2, 1);
  
  
-- ============================================================
-- 确认订单页落地：会员地址表 + 订单明细扩展列
-- 适用库：jingsuding_order（root/123456，MySQL 8.0）
-- 风格：幂等、纯增量；已存在则跳过，可重复执行。
-- 执行方式：
--   "/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -uroot -p123456 jingsuding_order < order_confirm_align.sql
-- ============================================================

select * from trx_order_item

-- ------------------------------------------
-- 1) 会员收货地址表 mem_member_address
--    小程序确认订单页「新增/修改收货地址」落库用，会员维度隔离（member_id）。
--    字段对齐前端 Address 模型：province/city/district/detail + name/phone/is_default。
-- ------------------------------------------
CREATE TABLE IF NOT EXISTS `mem_member_address` (
  `id`          BIGINT       NOT NULL AUTO_INCREMENT,
  `member_id`   BIGINT       NOT NULL                  COMMENT '会员ID，关联 mem_member.id',
  `name`        VARCHAR(50)  NOT NULL DEFAULT ''       COMMENT '收货人姓名',
  `phone`       VARCHAR(20)  NOT NULL DEFAULT ''       COMMENT '收货人电话',
  `province`    VARCHAR(50)  NOT NULL DEFAULT ''       COMMENT '省',
  `city`        VARCHAR(50)  NOT NULL DEFAULT ''       COMMENT '市',
  `district`    VARCHAR(50)  NOT NULL DEFAULT ''       COMMENT '区/县',
  `detail`      VARCHAR(255) NOT NULL DEFAULT ''       COMMENT '详细地址',
  `is_default`  TINYINT(1)   NOT NULL DEFAULT 0        COMMENT '是否默认地址 0-否 1-是',
  `is_deleted`  TINYINT      NOT NULL DEFAULT 0        COMMENT '逻辑删除 0-正常 1-删除',
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_member_id` (`member_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='会员收货地址表';

-- ------------------------------------------
-- 2) trx_order_item 扩展列
--    spec_json：镜片规格 + 验光数据 JSON（确认页传入的 spec_json 落库用）
--    processing_status：加工状态（unprocessed-待加工 / processed-已加工）
-- ------------------------------------------
SET @db = DATABASE();

SET @c1 = (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA=@db AND TABLE_NAME='trx_order_item' AND COLUMN_NAME='spec_json');
SET @sql1 = IF(@c1=0,
  "ALTER TABLE `trx_order_item` ADD COLUMN `spec_json` VARCHAR(2000) NULL COMMENT '镜片规格+验光数据JSON（确认页传入）'",
  'SELECT "spec_json 已存在，跳过" AS skip_reason');
PREPARE stmt1 FROM @sql1; EXECUTE stmt1; DEALLOCATE PREPARE stmt1;

SET @c2 = (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA=@db AND TABLE_NAME='trx_order_item' AND COLUMN_NAME='processing_status');
SET @sql2 = IF(@c2=0,
  "ALTER TABLE `trx_order_item` ADD COLUMN `processing_status` VARCHAR(20) NOT NULL DEFAULT 'processed' COMMENT '加工状态:unprocessed-待加工/processed-已加工'",
  'SELECT "processing_status 已存在，跳过" AS skip_reason');
PREPARE stmt2 FROM @sql2; EXECUTE stmt2; DEALLOCATE PREPARE stmt2;

SELECT 'order_confirm_align 执行完成' AS result;


-- ============================================================
-- 客户折扣体系重构：会员等级 ↔ 价格策略 联动
-- 数据库：jingsuding_order（如库名不同，请先改下面的 USE 语句）
--
-- 重构目标：
--   1. 客户折扣率统一来源于【客户等级】基础数据（mem_member_level.default_discount），
--      客户表 mem_member 不再独立存储折扣率；
--   2. 删除冗余列：mem_member.level（旧 0/1/2 枚举）、mem_member.discount_rate（独立折扣率）；
--   3. 客户只保留 customer_level_id 关联等级，折扣在取价时【动态计算、不落库】，
--      保证"改等级折扣 → 所有该等级客户立即生效"，不会出现历史客户折扣僵死。
--
-- ⚠️ 幂等：本脚本可重复执行（列不存在时自动跳过，不会报错）。
-- ⚠️ 执行顺序：建议先跑第 0 段的【体检查询】，确认无异常后再放开第 1/2 段的删除语句。
-- ============================================================

USE jingsuding_order;

-- ============================================================
-- 0. 执行前体检（只读，先跑这段，确认影响面）
-- ============================================================

-- 0.1 现有客户数、其中未设置等级的数量（删除旧 level 列后这些客户将变为"未分级"→按标准售价）
SELECT
    COUNT(*)                                                   AS total_member,
    SUM(CASE WHEN customer_level_id IS NULL OR customer_level_id = 0 THEN 1 ELSE 0 END) AS no_level_member,
    SUM(CASE WHEN discount_rate IS NOT NULL AND discount_rate < 1 THEN 1 ELSE 0 END)     AS had_discount_member
FROM mem_member
WHERE is_deleted = 0;

-- 0.2 客户等级表现状（确认已有等级数据，否则删除旧 level 后所有客户都无折扣）
SELECT id, level_code, level_name, default_discount, status
FROM mem_member_level
ORDER BY sort_order DESC, id;

-- 0.3 【可选】历史等级值迁移提示：
--     旧 level 是 0/1/2 的枚举（普通/VIP/核心），与新表 mem_member_level.id 无对应关系。
--     如需保留历史等级含义，请先手工在新表建好等级（拿到 id），再执行类似下面的映射更新：
-- UPDATE mem_member SET customer_level_id = 1  WHERE level = 1 AND is_deleted = 0;  -- 例：旧 VIP → 新等级
-- UPDATE mem_member SET customer_level_id = 1 WHERE level = 2 AND is_deleted = 0;  -- 例：旧核心 → 新等级


-- ============================================================
-- 1. 删除旧等级枚举列 mem_member.level
-- ============================================================
SET @db := DATABASE();
SET @sql := (
    SELECT IF(
        (SELECT COUNT(*) FROM information_schema.COLUMNS
          WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'mem_member' AND COLUMN_NAME = 'level') > 0,
        'ALTER TABLE mem_member DROP COLUMN `level`',
        "SELECT 'skip: mem_member.level 不存在，无需删除' AS msg"
    )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- ============================================================
-- 2. 删除独立折扣率列 mem_member.discount_rate
--    折扣统一由 customer_level_id → mem_member_level.default_discount 决定
-- ============================================================
SET @sql := (
    SELECT IF(
        (SELECT COUNT(*) FROM information_schema.COLUMNS
          WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'mem_member' AND COLUMN_NAME = 'discount_rate') > 0,
        'ALTER TABLE mem_member DROP COLUMN `discount_rate`',
        "SELECT 'skip: mem_member.discount_rate 不存在，无需删除' AS msg"
    )
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- ============================================================
-- 3. 确认索引存在（等级筛选 + 取价定位）
--    AppDbContext 已声明，若库里缺失可手工补（名字可自定义，MySQL 无 IF NOT EXISTS 语法，重复执行会报错属正常）
-- ============================================================
-- ALTER TABLE mem_member  ADD INDEX idx_member_level (customer_level_id);
-- ALTER TABLE price_rule  ADD INDEX idx_rule_level  (customer_level_id);


-- ============================================================
-- 4. 执行后校验
-- ============================================================
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'mem_member'
  AND COLUMN_NAME IN ('level', 'discount_rate', 'customer_level_id');
-- 预期：level / discount_rate 无结果（已删除），仅剩 customer_level_id


  
-- ============================================================
-- 1. 内容管理 - 公告表 cms_announcement
--    对应后端实体 Jsd.Api/Entities/CmsAnnouncement.cs
--    字段与需求文档一致；create_time / update_time 由数据库自动维护，
--    与 EF 的 DatabaseGenerated(Identity/Computed) 配置对应。
-- ============================================================
CREATE TABLE IF NOT EXISTS `cms_announcement` (
  `id`          BIGINT       NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `title`       VARCHAR(200) NOT NULL DEFAULT ''     COMMENT '公告标题',
  `content`     TEXT         NULL                    COMMENT '公告内容（富文本 HTML）',
  `is_top`      TINYINT      NOT NULL DEFAULT 0      COMMENT '是否置顶：1-置顶 0-不置顶',
  `status`      TINYINT      NOT NULL DEFAULT 1      COMMENT '状态：1-发布 0-草稿',
  `create_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP                  COMMENT '创建时间',
  `update_time` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_status`    (`status`),
  KEY `idx_is_top`    (`is_top`),
  KEY `idx_update_time` (`update_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='内容管理-公告表';

-- ============================================================
-- 2. 后台菜单注册（sys_menu）
--    路由由 src/router/index.ts 的 addDynamicRoutes 按 component 字段动态加载：
--      component = 'content/announcement/index'  =>  src/views/content/announcement/index.vue
--    按钮权限标识与前端 src/types/announcement.ts 的 ANNOUNCEMENT_PERMISSIONS 一一对应。
--    说明：若你已有「内容管理」目录，可删除第 1 条 INSERT，并把第 2 条的 parent_id 改为已有目录 id。
-- ============================================================

-- 2.1 顶级目录：内容管理
INSERT INTO `sys_menu`
  (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
  (1500, 0, '内容管理', '/content', 'Layout', 'BellFilled', 60, 1, NULL, 1, 1, NOW(), NOW());

-- 2.2 菜单：公告管理
INSERT INTO `sys_menu`
  (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
  (1501, 1500, '公告管理', 'announcement', 'content/announcement/index', 'Bell', 10, 2, NULL, 1, 1, NOW(), NOW());

-- 2.3 按钮权限（控制前端 v-permission 显隐）
INSERT INTO `sys_menu`
  (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
  (1502, 1501, '公告列表', '', '', '', 1, 3, 'announcement:list',   1, 1, NOW(), NOW()),
  (1503, 1501, '发布公告', '', '', '', 2, 3, 'announcement:add',    1, 1, NOW(), NOW()),
  (1504, 1501, '编辑公告', '', '', '', 3, 3, 'announcement:edit',   1, 1, NOW(), NOW()),
  (1505, 1501, '删除公告', '', '', '', 4, 3, 'announcement:delete', 1, 1, NOW(), NOW());


  
  
  -- ============================================================
-- 轮播图后台管理 - 菜单注册（sys_menu）
-- 路由由 src/router/index.ts 的 addDynamicRoutes 按 component 字段动态加载：
--      component = 'content/banner/index'  =>  src/views/content/banner/index.vue
-- 前置依赖：必须先有「内容管理」目录（parent_id=1500）。
--     若你已执行过 scripts/cms_announcement.sql，目录 1500 已存在，可删除第 1 条 INSERT。
--     若尚未执行，请保留第 1 条以创建目录。
-- 重要说明：本 SQL 仅注册菜单。要让菜单可用，还需配套：
--     1) 后端后台 CRUD 接口（Jsd.Api/Controllers/BannerController 增加 admin 前缀的增删改查）
--     2) 前端管理页面 src/views/content/banner/index.vue（含图片上传）
--     否则菜单点击后是空白页。
-- ============================================================


-- 2. 菜单：轮播管理（挂在「内容管理」目录下）
INSERT INTO `sys_menu`
  (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
  (1506, 1500, '轮播管理', 'banner', 'content/banner/index', 'PictureFilled', 20, 2, NULL, 1, 1, NOW(), NOW());

-- 3. 按钮权限（控制前端 v-permission 显隐）
INSERT INTO `sys_menu`
  (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
  (1507, 1506, '轮播列表', '', '', '', 1, 3, 'banner:list',  1, 1, NOW(), NOW()),
  (1508, 1506, '新增轮播', '', '', '', 2, 3, 'banner:add',   1, 1, NOW(), NOW()),
  (1509, 1506, '编辑轮播', '', '', '', 3, 3, 'banner:edit',  1, 1, NOW(), NOW()),
  (1510, 1506, '删除轮播', '', '', '', 4, 3, 'banner:delete',1, 1, NOW(), NOW());
  
  
  
  -- =============================================================
-- 售后模块：小程序端申请售后 配套 SQL
-- 适用：Jsd（4L 甄选产品订货系统）
--
-- 【现状】售后主表 trx_refund **已存在**（实体 Jsd.Api/Entities/TrxRefund.cs），
--        字段已覆盖：订单ID、退款金额、退款原因、凭证图片、状态(0待审核 1待退款 2已退款 3已驳回)、
--        类型(1仅退款 2退货退款)。因此本脚本**不需要建表**，只需补 2 个新列。
--
-- 【本次新增】
--   1) original_order_status  申请售后前的订单状态，用于「审核驳回」时恢复订单原状
--   2) source                 售后单来源：1-后台人工创建  2-小程序会员自助申请
--
-- 【订单状态变化】本次同时新增订单状态 5=售后中（OrderStatuses.AfterSale）：
--   会员申请售后 → 订单 order_status 置 5（售后中），并把原状态写入本表 original_order_status
--   审核通过+退款完成 → 订单置 4（已关闭）
--   审核驳回 → 订单恢复 original_order_status
-- =============================================================

-- -------------------------------------------------------------
-- 一、补齐新列（已存在则跳过，MySQL 不支持 IF NOT EXISTS，用存储过程兜底）
-- -------------------------------------------------------------
SET @db = DATABASE();

-- 1. original_order_status
SET @exist = (SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'trx_refund' AND COLUMN_NAME = 'original_order_status');
SET @sql = IF(@exist = 0,
  'ALTER TABLE trx_refund ADD COLUMN original_order_status TINYINT NULL DEFAULT NULL COMMENT ''申请售前订单状态（驳回时恢复）'' AFTER order_no',
  'SELECT ''original_order_status 已存在，跳过''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 2. source
SET @exist = (SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'trx_refund' AND COLUMN_NAME = 'source');
SET @sql = IF(@exist = 0,
  'ALTER TABLE trx_refund ADD COLUMN source TINYINT NOT NULL DEFAULT 1 COMMENT ''来源：1-后台 2-小程序'' AFTER original_order_status',
  'SELECT ''source 已存在，跳过''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- -------------------------------------------------------------
-- 二、建表 SQL（仅供全新环境/迁移参考；当前库已有本表，请勿重复执行）
-- -------------------------------------------------------------
-- DROP TABLE IF EXISTS trx_refund;
-- CREATE TABLE `trx_refund` (
--   `id`                    BIGINT       NOT NULL AUTO_INCREMENT COMMENT '主键',
--   `refund_no`             VARCHAR(32)  NOT NULL                COMMENT '退款单号（唯一，TK+日期+序号）',
--   `order_id`              BIGINT       NOT NULL                COMMENT '订单ID（trx_order.id）',
--   `order_no`              VARCHAR(50)  DEFAULT NULL            COMMENT '订单号（冗余）',
--   `original_order_status` TINYINT      DEFAULT NULL            COMMENT '申请售前订单状态（驳回时恢复）',
--   `source`                TINYINT      NOT NULL DEFAULT 1      COMMENT '来源：1-后台 2-小程序',
--   `member_id`             BIGINT       NOT NULL                COMMENT '申请人（mem_member.id）',
--   `refund_amount`         DECIMAL(12,2) NOT NULL               COMMENT '退款金额（元）',
--   `refund_type`           TINYINT      NOT NULL                COMMENT '1-仅退款 2-退货退款',
--   `refund_reason`         VARCHAR(255) DEFAULT NULL            COMMENT '退款原因',
--   `voucher_images`        VARCHAR(500) DEFAULT NULL            COMMENT '凭证图片（多个逗号分隔）',
--   `status`                TINYINT      NOT NULL DEFAULT 0      COMMENT '0待审核 1待退款 2已退款 3已驳回',
--   `admin_remark`          VARCHAR(255) DEFAULT NULL            COMMENT '处理备注',
--   `operator_id`           BIGINT       DEFAULT NULL            COMMENT '处理人ID',
--   `handle_time`           DATETIME     DEFAULT NULL            COMMENT '审核处理时间',
--   `refund_time`           DATETIME     DEFAULT NULL            COMMENT '实际打款时间',
--   `create_time`           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
--   `update_time`           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
--   PRIMARY KEY (`id`),
--   UNIQUE KEY `uk_refund_no` (`refund_no`),
--   KEY `idx_order_id` (`order_id`),
--   KEY `idx_member_id` (`member_id`),
--   KEY `idx_status` (`status`)
-- ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='退款申请表';

-- -------------------------------------------------------------
-- 三、后台菜单（如尚未执行过 after_sale_menu.sql，请执行它）
--    路径：Jsd.Api/sql/scripts/after_sale_menu.sql
--    菜单：售后管理(150) → 退款申请(151) / 退货管理(152)，按钮权限 aftersale:refund:*
--    后台「退款申请」页会自动展示小程序发起的申请（source=2），审核流程完全一致。
-- -------------------------------------------------------------


-- 订单主表增加物流面单照片字段
ALTER TABLE trx_order
    ADD COLUMN shipping_image_url VARCHAR(500) NULL
    COMMENT '物流面单照片路径（发货时上传，相对路径如 /upload/202610/xxx.jpg）';
