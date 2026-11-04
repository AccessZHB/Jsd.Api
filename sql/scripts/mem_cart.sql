-- ============================================================
-- 会员购物车表 mem_cart
-- 数据库：jingsuding_order（MySQL 8.0）
-- 目的：把小程序购物车从前端 localStorage 迁移到服务端，
--       并修复「需要加工 / 不需要加工」被错误合并成数量×2 的严重 Bug。
--
-- 合并判定唯一键 cart_key =
--   memberId_prodInfoId_skuId_processingType_prescriptionHash
-- 任意一项不同（尤其 processingType 0/1、验光不同）→ 必然是两条独立记录。
--
-- 本脚本幂等，可重复执行。
-- ============================================================

CREATE TABLE IF NOT EXISTS `mem_cart` (
  `id`                BIGINT       NOT NULL AUTO_INCREMENT COMMENT '主键',
  `member_id`         BIGINT       NOT NULL                COMMENT '会员ID（mem_member.id）',
  `prod_info_id`      BIGINT       NOT NULL                COMMENT '商品ID（prod_info.id）',
  `sku_id`            BIGINT       NOT NULL                COMMENT '规格SKU ID（prod_sku.id）',
  `quantity`          INT          NOT NULL DEFAULT 1      COMMENT '数量',
  `processing_type`   TINYINT      NOT NULL DEFAULT 0      COMMENT '是否需要加工：0-不需要(只发片) 1-需要(邮寄镜架)',
  `prescription_data` LONGTEXT         NULL                COMMENT '验光数据JSON（{left:{sphere,cylinder,axis,pd,height},right:{...}}）',
  `prescription_hash` VARCHAR(64)  NOT NULL DEFAULT 'NORX' COMMENT '验光数据规范化SHA256（无验光=NORX）',
  `cart_key`          VARCHAR(128) NOT NULL                COMMENT '唯一键：member_prod_sku_pt_rxhash',
  `is_selected`       TINYINT      NOT NULL DEFAULT 1      COMMENT '是否勾选：0-否 1-是',
  `add_time`          DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '加入时间',
  `update_time`       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_cart_key` (`cart_key`) COMMENT '不同加工属性/验光绝不合并的最后防线',
  KEY `idx_member` (`member_id`),
  KEY `idx_member_selected` (`member_id`, `is_selected`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='会员购物车（服务端持久化，跨设备同步）';

-- ============================================================
-- 旧数据清理
-- 说明：旧购物车是纯前端 localStorage，数据库里没有历史购物车数据，
--       小程序端已删除 jsd_mp_cart_* 的读写逻辑（旧缓存会被忽略并自然过期）。
-- 下面只清理「本表启用后可能产生的脏数据」，可安全重复执行。
-- ============================================================

-- 1) 清理 SKU 已被物理删除的购物车行（商品不存在则无法下单）
DELETE c FROM `mem_cart` c
LEFT JOIN `prod_sku` s ON s.`id` = c.`sku_id`
WHERE s.`id` IS NULL;

-- 2) 纠正脏数据：非镜片商品不应存在「需要加工」标记
UPDATE `mem_cart` c
JOIN `prod_info` p ON p.`id` = c.`prod_info_id`
SET c.`processing_type` = 0,
    c.`prescription_data` = NULL,
    c.`prescription_hash` = 'NORX'
WHERE p.`is_prescription` <> 1
  AND c.`processing_type` <> 0;

-- 3) 数量非法（<=0）纠正为 1
UPDATE `mem_cart` SET `quantity` = 1 WHERE `quantity` <= 0;

-- 4) 自检：查看是否存在 cart_key 重复（应始终为 0 行）
-- SELECT cart_key, COUNT(*) cnt FROM mem_cart GROUP BY cart_key HAVING cnt > 1;

SELECT 'mem_cart 建表与清理完成' AS result;
