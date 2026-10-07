-- ============================================================
-- 首页轮播图（运营位）表 mkt_banner
-- 对应实体：Jsd.Api/Entities/MktBanner.cs
-- 对应接口：GET /api/banner/list（小程序首页读取 status=1 的记录，按 sort 升序）
-- 说明：path 为小程序内部页面路径（如 /pages/category/list），为空表示点击不跳转
-- ============================================================

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
