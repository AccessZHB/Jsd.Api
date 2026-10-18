-- ============================================================
-- 镜速订订货系统 —— 基础数据 & 商品管理模块建表脚本（v2）
-- 执行数据库：jingsuding_order（与 appsettings.json 连接串一致；
-- 如果你的实际库名是 db_jingsuding_order，请修改下面的 USE 语句）
-- 字符集：utf8mb4
-- ============================================================
USE jingsuding_order;

-- ------------------------------------------------------------
-- 1. 供应商表
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sup_supplier (
  id              BIGINT        NOT NULL AUTO_INCREMENT COMMENT '主键',
  supplier_name   VARCHAR(200)  NOT NULL COMMENT '供应商名称',
  contact_person  VARCHAR(100)  NULL COMMENT '联系人',
  contact_phone   VARCHAR(50)   NULL COMMENT '联系电话',
  status          TINYINT       NOT NULL DEFAULT 1 COMMENT '合作状态 (0:禁用, 1:启用)',
  remark          VARCHAR(500)  NULL COMMENT '备注',
  create_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  update_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (id),
  KEY idx_supplier_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='供应商表';

-- ------------------------------------------------------------
-- 2. 商品分类表（多级树形结构）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prod_category (
  id              BIGINT        NOT NULL AUTO_INCREMENT COMMENT '主键',
  category_name   VARCHAR(200)  NOT NULL COMMENT '分类名称',
  parent_id       BIGINT        NOT NULL DEFAULT 0 COMMENT '父分类ID (0为顶级分类)',
  sort_order      INT           NOT NULL DEFAULT 0 COMMENT '排序',
  status          TINYINT       NOT NULL DEFAULT 1 COMMENT '状态 (0:禁用, 1:启用)',
  create_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  update_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (id),
  KEY idx_category_parent (parent_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='商品分类表';

-- ------------------------------------------------------------
-- 3. 商品主表（SPU）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prod_info (
  id              BIGINT        NOT NULL AUTO_INCREMENT COMMENT '主键',
  product_name    VARCHAR(300)  NOT NULL COMMENT '商品名称',
  category_id     BIGINT        NULL COMMENT '分类ID (关联 prod_category)',
  supplier_id     BIGINT        NULL COMMENT '供应商ID (关联 sup_supplier)',
  description     TEXT          NULL COMMENT '商品描述',
  main_image      VARCHAR(500)  NULL COMMENT '主图URL',
  status          TINYINT       NOT NULL DEFAULT 1 COMMENT '状态 (0:下架, 1:上架)',
  create_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  update_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (id),
  KEY idx_product_category (category_id),
  KEY idx_product_supplier (supplier_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='商品主表(SPU)';

-- ------------------------------------------------------------
-- 4. 商品规格项表（属性维度：颜色/尺码/折射率/膜层…）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prod_spec_item (
  id              BIGINT        NOT NULL AUTO_INCREMENT COMMENT '主键',
  product_id      BIGINT        NOT NULL COMMENT '商品ID (关联 prod_info)',
  spec_name       VARCHAR(100)  NOT NULL COMMENT '规格项名称',
  sort_order      INT           NOT NULL DEFAULT 0 COMMENT '排序',
  create_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  update_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (id),
  KEY idx_spec_item_product (product_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='商品规格项表';

-- ------------------------------------------------------------
-- 5. 商品规格值表（规格项下的可选值）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prod_spec_value (
  id              BIGINT        NOT NULL AUTO_INCREMENT COMMENT '主键',
  spec_item_id    BIGINT        NOT NULL COMMENT '规格项ID (关联 prod_spec_item)',
  spec_value      VARCHAR(100)  NOT NULL COMMENT '规格值 (如: 红色、M、1.56)',
  sort_order      INT           NOT NULL DEFAULT 0 COMMENT '排序',
  create_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  update_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (id),
  KEY idx_spec_value_item (spec_item_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='商品规格值表';

-- ------------------------------------------------------------
-- 6. 商品SKU表（规格组合后的具体商品，含价格/库存/编码）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prod_sku (
  id              BIGINT         NOT NULL AUTO_INCREMENT COMMENT '主键',
  product_id      BIGINT         NOT NULL COMMENT '商品ID (关联 prod_info)',
  sku_code        VARCHAR(100)   NULL COMMENT 'SKU编码 (唯一索引)',
  spec_values     JSON           NULL COMMENT '规格值组合 (如: {"颜色":"红色","尺码":"M"})',
  price           DECIMAL(10,2)  NOT NULL DEFAULT 0.00 COMMENT '价格',
  stock           INT            NOT NULL DEFAULT 0 COMMENT '库存',
  create_time     DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  update_time     DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (id),
  UNIQUE KEY uk_sku_code (sku_code),
  KEY idx_sku_product (product_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='商品SKU表';

-- ------------------------------------------------------------
-- 7. 商品图片表
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prod_image (
  id              BIGINT        NOT NULL AUTO_INCREMENT COMMENT '主键',
  product_id      BIGINT        NOT NULL COMMENT '商品ID (关联 prod_info)',
  image_url       VARCHAR(500)  NOT NULL COMMENT '图片URL',
  sort_order      INT           NOT NULL DEFAULT 0 COMMENT '排序',
  create_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  update_time     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (id),
  KEY idx_image_product (product_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='商品图片表';
