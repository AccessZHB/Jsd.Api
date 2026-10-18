-- ================================================================
-- prod_sku 表结构变更脚本（MySQL 5.7 / 8.x 通用，8.x 部分语句更推荐）
-- 数据库：jingsuding_order
--
-- 变更点：
--   1. sku_code    —— 改为【后端自动生成】：固定前缀 SKU + yyyyMMddHHmmss + 4位随机数
--                     例：SKU202609082310451234
--                     字段仍为 VARCHAR(50) NOT NULL，新增 UNIQUE 唯一索引 uk_sku_code
--   2. spec_values —— 语义变更：由"规格值展示文本"改为"规格值ID组合"
--                     例："10" 或 "10,15"（对应 prod_spec_value.id）
--                     字段仍为 VARCHAR(255)
--
-- 执行顺序：备份 → 存量数据体检 → 修复空/重复编码 → 改字段 → 加索引 → （可选）存量文本转ID
-- 建议：业务低峰期执行，先备份。
-- ================================================================

USE `jingsuding_order`;

-- ---------------------------------------------------------------
-- 0. 备份（务必执行）
-- ---------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `prod_sku_bak_20260908` AS SELECT * FROM `prod_sku`;

-- ---------------------------------------------------------------
-- 1. 存量数据体检（两条查询的结果都必须是空/0，否则先做第 2、3 步修复）
-- ---------------------------------------------------------------
-- 1.1 重复编码
SELECT `sku_code`, COUNT(*) AS cnt
FROM `prod_sku`
GROUP BY `sku_code`
HAVING cnt > 1;

-- 1.2 空编码（历史代码会把空串写进去，空串重复会直接导致唯一索引创建失败）
SELECT COUNT(*) AS empty_code_count
FROM `prod_sku`
WHERE `sku_code` IS NULL OR `sku_code` = '';

-- ---------------------------------------------------------------
-- 2. 修复空编码：按新规则生成（SKU + 时间戳 + 4位序号，保证本批次内不重复）
--    说明：这里用序号代替随机数，是为了保证批量更新 100% 不撞号（≤9999 行时绝对安全）
-- ---------------------------------------------------------------
SET @rn := 0;
UPDATE `prod_sku` s
    JOIN (
        SELECT `id`,
               CONCAT('SKU',
                      DATE_FORMAT(IFNULL(`create_time`, NOW()), '%Y%m%d%H%i%s'),
                      LPAD(@rn := @rn + 1, 4, '0')) AS new_code
        FROM `prod_sku`, (SELECT @rn := 0) r
        WHERE `sku_code` IS NULL OR `sku_code` = ''
    ) t ON t.`id` = s.`id`
SET s.`sku_code` = t.new_code;

-- ---------------------------------------------------------------
-- 3. 修复重复编码：保留同编码中 id 最小的一条，其余重新生成（MySQL 8.0 写法）
--    MySQL 5.7 没有窗口函数，可改用第 2 步的 @rn 变量写法（把 WHERE 条件换成"非最小id"）
-- ---------------------------------------------------------------
UPDATE `prod_sku` s
    JOIN (
        SELECT `id`,
               CONCAT('SKU',
                      DATE_FORMAT(NOW(), '%Y%m%d%H%i%s'),
                      LPAD(ROW_NUMBER() OVER (ORDER BY `id`), 4, '0')) AS new_code
        FROM `prod_sku`
        WHERE `id` NOT IN (
            SELECT `min_id` FROM (
                SELECT MIN(`id`) AS `min_id` FROM `prod_sku` GROUP BY `sku_code`
            ) x
        )
    ) t ON t.`id` = s.`id`
SET s.`sku_code` = t.new_code;

-- MySQL 5.7 等价写法（若上面报语法错，改用这段）：
-- SET @rn2 := 0;
-- UPDATE `prod_sku` s
--     JOIN (
--         SELECT a.`id`,
--                CONCAT('SKU', DATE_FORMAT(NOW(), '%Y%m%d%H%i%s'), LPAD(@rn2 := @rn2 + 1, 4, '0')) AS new_code
--         FROM `prod_sku` a
--         LEFT JOIN (SELECT MIN(`id`) AS `min_id` FROM `prod_sku` GROUP BY `sku_code`) b
--                ON a.`id` = b.`min_id`
--         WHERE b.`min_id` IS NULL
--     ) t ON t.`id` = s.`id`
-- SET s.`sku_code` = t.new_code;

-- ---------------------------------------------------------------
-- 4. 修改字段注释 + 新增索引（核心 DDL）
-- ---------------------------------------------------------------
ALTER TABLE `prod_sku`
    MODIFY COLUMN `sku_code` VARCHAR(50) NOT NULL
        COMMENT 'SKU编码（后端自动生成：SKU+yyyyMMddHHmmss+4位随机数，唯一）',
    MODIFY COLUMN `spec_values` VARCHAR(255) NULL DEFAULT ''
        COMMENT '规格值ID组合（如 10 或 10,15，对应 prod_spec_value.id）',
    ADD UNIQUE KEY `uk_sku_code` (`sku_code`),
    ADD KEY `idx_sku_prod_info` (`prod_info_id`);

-- ---------------------------------------------------------------
-- 5.（可选）存量 spec_values 由"展示文本"迁移为"ID组合"
--    仅当 spec_values 里存的是 "1.60/防蓝光膜" 这类文本时才需要执行；
--    已经是纯数字ID串的行（REGEXP 判断）会被跳过。
--    注意：若同一商品下存在同名规格值，可能匹配到多个ID，请先在测试库验证。
-- ---------------------------------------------------------------
UPDATE `prod_sku` s
    JOIN (
        SELECT s2.`id`,
               (SELECT GROUP_CONCAT(v.`id` ORDER BY v.`sort` SEPARATOR ',')
                FROM `prod_spec_value` v
                WHERE v.`prod_info_id` = s2.`prod_info_id`
                  AND FIND_IN_SET(v.`spec_value`, REPLACE(s2.`spec_values`, '/', ',')) > 0
               ) AS new_ids
        FROM `prod_sku` s2
        WHERE s2.`spec_values` IS NOT NULL
          AND s2.`spec_values` <> ''
          AND s2.`spec_values` REGEXP '^[0-9,]+$' = 0   -- 不是纯ID串的行才处理
    ) t ON t.`id` = s.`id`
SET s.`spec_values` = IFNULL(t.new_ids, s.`spec_values`);

-- 迁移后核对：还有哪些行没转成ID
-- SELECT id, prod_info_id, spec_values FROM prod_sku
-- WHERE spec_values IS NOT NULL AND spec_values <> '' AND spec_values REGEXP '^[0-9,]+$' = 0;

-- ================================================================
-- 附：全新环境建表语句（已含本次变更，老库不要执行）
-- ================================================================
-- DROP TABLE IF EXISTS `prod_sku`;
-- CREATE TABLE `prod_sku` (
--   `id`            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT '主键ID',
--   `prod_info_id`  BIGINT UNSIGNED NOT NULL COMMENT '商品ID，关联prod_info表',
--   `sku_code`      VARCHAR(50)  NOT NULL COMMENT 'SKU编码（后端自动生成：SKU+yyyyMMddHHmmss+4位随机数，唯一）',
--   `sku_name`      VARCHAR(100) DEFAULT '' COMMENT 'SKU名称',
--   `spec_values`   VARCHAR(255) DEFAULT '' COMMENT '规格值ID组合（如 10 或 10,15，对应 prod_spec_value.id）',
--   `price`          DECIMAL(12,2) NOT NULL DEFAULT 0.00 COMMENT 'SKU销售单价',
--   `original_price` DECIMAL(12,2) DEFAULT 0.00 COMMENT 'SKU划线原价',
--   `cost_price`     DECIMAL(12,2) DEFAULT 0.00 COMMENT 'SKU成本价',
--   `stock`          INT NOT NULL DEFAULT 0 COMMENT 'SKU库存量',
--   `sales_count`    INT DEFAULT 0 COMMENT 'SKU销量',
--   `image`          VARCHAR(255) DEFAULT '' COMMENT 'SKU图片URL',
--   `status`         TINYINT NOT NULL DEFAULT 1 COMMENT '状态：1-启用 0-停用',
--   `create_time`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
--   `update_time`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
--   PRIMARY KEY (`id`),
--   UNIQUE KEY `uk_sku_code` (`sku_code`),
--   KEY `idx_sku_prod_info` (`prod_info_id`)
-- ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='商品SKU明细表';
