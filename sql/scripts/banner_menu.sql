-- ============================================================
-- 轮播图后台管理 - 菜单注册（sys_menu）
-- 路由由 src/router/index.ts 的 addDynamicRoutes 按 component 字段动态加载：
--      component = 'content/banner/index'  =>  src/views/content/banner/index.vue
-- 前置依赖：必须先有「内容管理」目录（parent_id=1500）。
--     若你已执行过 scripts/cms_announcement.sql，目录 1500 已存在，可删除第 1 条 INSERT。
--     若尚未执行，请保留第 1 条以创建目录。
-- 配套代码（已实现，菜单可直接使用）：
--     1) 后端后台 CRUD 接口：Jsd.Api/Controllers/BannerController 的 admin/* 增删改查
--        （GET admin/list / POST admin/add / PUT admin/edit / DELETE admin/{id} / PUT admin/update-status）
--     2) 前端管理页面：Jsd.Web/src/views/content/banner/index.vue（含图片上传）
--     后端路由由 utils/request 的 baseURL /api 拼接为 /api/banner/admin/*。
-- ============================================================

-- 1. 顶级目录：内容管理（如已存在请删除本条）
INSERT INTO `sys_menu`
  (`id`, `parent_id`, `menu_name`, `path`, `component`, `icon`, `sort_order`, `menu_type`, `permission`, `visible`, `status`, `create_time`, `update_time`)
VALUES
  (1500, 0, '内容管理', '/content', 'Layout', 'BellFilled', 60, 1, NULL, 1, 1, NOW(), NOW());

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
