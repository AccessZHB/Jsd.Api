-- ============================================================
-- 物流轨迹查询模块 增量脚本（已有库专用，纯增量、幂等）
-- 执行前请在 mysql 客户端加 --default-character-set=utf8mb4
--   "C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -uroot -p123456 --default-character-set=utf8mb4 jingsuding_order < logistics_align.sql
-- ============================================================

-- ------------------------------------------------------------
-- 1. 物流轨迹查询日志表 logistics_query_log（CREATE TABLE IF NOT EXISTS，可重复执行）
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `logistics_query_log` (
  `id`               BIGINT          NOT NULL AUTO_INCREMENT                COMMENT '主键ID',
  `order_id`         BIGINT          NOT NULL DEFAULT 0                     COMMENT '关联订单ID（无则0）',
  `tracking_number`  VARCHAR(50)     NOT NULL                               COMMENT '快递单号',
  `carrier_code`     VARCHAR(50)     NOT NULL DEFAULT ''                    COMMENT '快递公司编码（如 shunfeng）',
  `carrier_name`     VARCHAR(100)    NOT NULL DEFAULT ''                    COMMENT '快递公司名称',
  `query_result`     TEXT                                                    COMMENT '完整响应JSON（排查问题用）',
  `status`           TINYINT         NOT NULL DEFAULT 1                     COMMENT '查询状态：1-成功 0-失败',
  `error_message`    VARCHAR(500)    NOT NULL DEFAULT ''                    COMMENT '错误信息',
  `create_time`      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP     COMMENT '查询时间',
  PRIMARY KEY (`id`),
  KEY `idx_tracking_number` (`tracking_number`),
  KEY `idx_order_id` (`order_id`),
  KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='物流轨迹查询日志表';

-- ------------------------------------------------------------
-- 2. 菜单：物流查询（页面，挂在「订单管理」目录下）
--    说明：本系统「订单管理」顶级菜单 menu_id = 3（不是 1100，1100 是定时任务占位段）。
--    若 208 已存在则跳过（INSERT IGNORE）。
-- ------------------------------------------------------------
INSERT IGNORE INTO `sys_menu`
  (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
  (208, 3, '物流查询', 'logistics', 'order/logistics/index', 'shipping', 2, 2, 'order:logistics:query', 1, 1, NOW(), NOW());

-- ------------------------------------------------------------
-- 3. 按钮：刷新物流（挂在「物流查询」页面下，menu_type=3）
-- ------------------------------------------------------------
INSERT IGNORE INTO `sys_menu`
  (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
  (209, 208, '刷新物流', '', '', NULL, 1, 3, 'order:logistics:refresh', 1, 1, NOW(), NOW());

-- ------------------------------------------------------------
-- 4. 角色授权（INSERT IGNORE 幂等）：
--    role_id=2 运营专员、role_id=3 仓库管理员 均授予 物流查询 页面 + 刷新物流 按钮
--    （超级管理员 role_id=1 由后端自动拥有全部菜单，无需在此授权）
-- ------------------------------------------------------------
INSERT IGNORE INTO `sys_role_menu` (`role_id`, `menu_id`) VALUES
  (2, 208), (2, 209),
  (3, 208), (3, 209);

-- ------------------------------------------------------------
-- 验证
-- ------------------------------------------------------------
SELECT 'logistics_query_log 已就绪' AS result, COUNT(*) AS menu_count
FROM sys_menu WHERE id IN (208, 209);
