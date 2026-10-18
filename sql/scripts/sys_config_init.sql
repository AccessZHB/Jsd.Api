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
