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
