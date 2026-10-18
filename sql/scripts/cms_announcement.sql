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
