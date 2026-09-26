using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Entities;

/// <summary>
/// 应用程序数据库上下文（EF Core）
/// 对应数据库：jingsuding_order
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>后台管理员用户</summary>
    public DbSet<SysUser> SysUsers => Set<SysUser>();

    /// <summary>后台角色</summary>
    public DbSet<SysRole> SysRoles => Set<SysRole>();

    /// <summary>操作审计日志（sys_operation_log，系统管理模块）</summary>
    public DbSet<SysOperationLog> SysOperationLogs => Set<SysOperationLog>();

    /// <summary>登录日志（sys_login_log，系统管理模块）</summary>
    public DbSet<SysLoginLog> SysLoginLogs => Set<SysLoginLog>();

    /// <summary>系统配置（sys_config，登录安全策略等键值对；system_align.sql 建表）</summary>
    public DbSet<SysConfig> SysConfigs => Set<SysConfig>();

    /// <summary>字典类型（sys_dict_type，字典管理模块）</summary>
    public DbSet<SysDictType> SysDictTypes => Set<SysDictType>();

    /// <summary>字典数据（sys_dict_data，字典管理模块）</summary>
    public DbSet<SysDictData> SysDictDatas => Set<SysDictData>();

    /// <summary>后台菜单</summary>
    public DbSet<SysMenu> SysMenus => Set<SysMenu>();

    /// <summary>角色-菜单关联</summary>
    public DbSet<SysRoleMenu> SysRoleMenus => Set<SysRoleMenu>();

    /// <summary>供应商</summary>
    public DbSet<SupSupplier> SupSuppliers => Set<SupSupplier>();

    /// <summary>商品分类</summary>
    public DbSet<ProdCategory> ProdCategories => Set<ProdCategory>();

    /// <summary>商品主表（SPU）</summary>
    public DbSet<ProdInfo> ProdInfos => Set<ProdInfo>();

    /// <summary>商品规格项</summary>
    public DbSet<ProdSpecItem> ProdSpecItems => Set<ProdSpecItem>();

    /// <summary>商品规格值</summary>
    public DbSet<ProdSpecValue> ProdSpecValues => Set<ProdSpecValue>();

    /// <summary>商品SKU</summary>
    public DbSet<ProdSku> ProdSkus => Set<ProdSku>();

    /// <summary>入库单主表</summary>
    public DbSet<LogStockIn> LogStockIns => Set<LogStockIn>();

    /// <summary>入库单明细表</summary>
    public DbSet<LogStockInItem> LogStockInItems => Set<LogStockInItem>();

    /// <summary>出库单主表</summary>
    public DbSet<LogStockOut> LogStockOuts => Set<LogStockOut>();

    /// <summary>出库单明细表</summary>
    public DbSet<LogStockOutItem> LogStockOutItems => Set<LogStockOutItem>();

    /// <summary>库存变动日志表</summary>
    public DbSet<LogStockLog> LogStockLogs => Set<LogStockLog>();

    /// <summary>库存盘点单主表</summary>
    public DbSet<LogStockCheck> LogStockChecks => Set<LogStockCheck>();

    /// <summary>库存盘点单明细表</summary>
    public DbSet<LogStockCheckItem> LogStockCheckItems => Set<LogStockCheckItem>();

    /// <summary>库存预警配置表（log_stock_warning；纯配置，不存预警状态）</summary>
    public DbSet<LogStockWarning> LogStockWarnings => Set<LogStockWarning>();

    /// <summary>订单主表（trx_order）</summary>
    public DbSet<TrxOrder> TrxOrders => Set<TrxOrder>();

    /// <summary>订单明细表（trx_order_item）</summary>
    public DbSet<TrxOrderItem> TrxOrderItems => Set<TrxOrderItem>();

    /// <summary>会员 / 客户主表（mem_member，订单 buyer_id 指向本表）</summary>
    public DbSet<MemMember> MemMembers => Set<MemMember>();

    // ==================== 财务结算模块 ====================

    /// <summary>应收账款表（trx_receivable，B2B 赊账/月结）</summary>
    public DbSet<TrxReceivable> TrxReceivables => Set<TrxReceivable>();

    /// <summary>收款记录表（mkt_payment，线下收款）</summary>
    public DbSet<MktPayment> MktPayments => Set<MktPayment>();

    /// <summary>收款核销关联表（mkt_payment_item）</summary>
    public DbSet<MktPaymentItem> MktPaymentItems => Set<MktPaymentItem>();

    /// <summary>支付日志表（trx_payment_log，微信回调对账）</summary>
    public DbSet<TrxPaymentLog> TrxPaymentLogs => Set<TrxPaymentLog>();

    /// <summary>商品图片（prod_image：主图/轮播图/详情图）</summary>
    public DbSet<ProdImage> ProdImages => Set<ProdImage>();

    // ==================== 采购管理模块 ====================

    /// <summary>采购订单主表（pur_order）</summary>
    public DbSet<PurOrder> PurOrders => Set<PurOrder>();

    /// <summary>采购订单明细表（pur_order_item）</summary>
    public DbSet<PurOrderItem> PurOrderItems => Set<PurOrderItem>();

    /// <summary>采购入库单主表（pur_inbound，以采定入）</summary>
    public DbSet<PurInbound> PurInbounds => Set<PurInbound>();

    /// <summary>采购入库明细表（pur_inbound_item）</summary>
    public DbSet<PurInboundItem> PurInboundItems => Set<PurInboundItem>();

    /// <summary>应付账款表（pur_payable，入库确认自动生成）</summary>
    public DbSet<PurPayable> PurPayables => Set<PurPayable>();

    // ==================== 售后管理模块 ====================

    /// <summary>退款申请表（trx_refund，资金流主表）</summary>
    public DbSet<TrxRefund> TrxRefunds => Set<TrxRefund>();

    /// <summary>退货单表（trx_return，物流流主表）</summary>
    public DbSet<TrxReturn> TrxReturns => Set<TrxReturn>();

    /// <summary>退货明细表（trx_return_item）</summary>
    public DbSet<TrxReturnItem> TrxReturnItems => Set<TrxReturnItem>();

    /// <summary>售后日志表（trx_refund_log，操作留痕）</summary>
    public DbSet<TrxRefundLog> TrxRefundLogs => Set<TrxRefundLog>();

    // ==================== 价格策略与会员余额模块 ====================

    /// <summary>客户等级表（mem_member_level）</summary>
    public DbSet<MemMemberLevel> MemMemberLevels => Set<MemMemberLevel>();

    /// <summary>价格策略主表（price_strategy）</summary>
    public DbSet<PriceStrategy> PriceStrategies => Set<PriceStrategy>();

    /// <summary>价格规则表（price_rule）</summary>
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();

    /// <summary>价格规则明细表（price_rule_item，指定商品 / 阶梯价）</summary>
    public DbSet<PriceRuleItem> PriceRuleItems => Set<PriceRuleItem>();

    /// <summary>价格变更日志表（price_change_log，审计留痕）</summary>
    public DbSet<PriceChangeLog> PriceChangeLogs => Set<PriceChangeLog>();

    /// <summary>订单价格快照表（order_price_snapshot，历史价格追溯唯一依据）</summary>
    public DbSet<OrderPriceSnapshot> OrderPriceSnapshots => Set<OrderPriceSnapshot>();

    /// <summary>充值订单表（mem_recharge，资金入口）</summary>
    public DbSet<MemRecharge> MemRecharges => Set<MemRecharge>();

    /// <summary>余额流水表（mkt_balance_log，余额真值台账）</summary>
    public DbSet<MktBalanceLog> MktBalanceLogs => Set<MktBalanceLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- sys_user 用户表 ----------
        modelBuilder.Entity<SysUser>(entity =>
        {
            // 登录账号唯一索引
            entity.HasIndex(e => e.UserName).IsUnique();
            entity.HasIndex(e => e.RoleId);

            // 一个角色对应多个用户；删除角色时用户的 role_id 置空（与数据库外键 ON DELETE SET NULL 一致）
            entity.HasOne(e => e.Role)
                  .WithMany(r => r.Users)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- sys_role 角色表 ----------
        modelBuilder.Entity<SysRole>(entity =>
        {
            // 角色编码唯一索引
            entity.HasIndex(e => e.RoleCode).IsUnique();
        });

        // ---------- sys_dict_type 字典类型表 ----------
        // 与 Jsd_order.sql 的 uk_dict_type / idx_status 保持一致
        modelBuilder.Entity<SysDictType>(entity =>
        {
            entity.HasIndex(e => e.DictType).IsUnique();
            entity.HasIndex(e => e.Status);
        });

        // ---------- sys_dict_data 字典数据表 ----------
        // 复合索引 (dict_type_id, dict_sort) 覆盖「按类型取启用数据并排序」的核心查询
        modelBuilder.Entity<SysDictData>(entity =>
        {
            entity.HasIndex(e => new { e.DictTypeId, e.DictSort });
            entity.HasIndex(e => e.Status);
        });

        // ---------- sys_menu 菜单表 ----------
        modelBuilder.Entity<SysMenu>(entity =>
        {
            entity.HasIndex(e => e.ParentId);
        });

        // ---------- sys_role_menu 角色菜单关联表 ----------
        modelBuilder.Entity<SysRoleMenu>(entity =>
        {
            // 同一角色下同一菜单只能授权一次
            entity.HasIndex(e => new { e.RoleId, e.MenuId }).IsUnique();

            entity.HasOne(e => e.Role)
                  .WithMany()
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Menu)
                  .WithMany()
                  .HasForeignKey(e => e.MenuId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // 系统管理模块（操作日志 / 登录日志 / 登录安全策略）
        // 索引与 Jsd_order.sql 建表语句保持一致
        // ============================================================

        // ---------- sys_operation_log 操作审计日志表 ----------
        modelBuilder.Entity<SysOperationLog>(entity =>
        {
            entity.HasIndex(e => e.OperatorId);
            entity.HasIndex(e => e.Module);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreateTime);
        });

        // ---------- sys_login_log 登录日志表 ----------
        modelBuilder.Entity<SysLoginLog>(entity =>
        {
            entity.HasIndex(e => e.MemberId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreateTime);
        });

        // ---------- sys_config 系统配置表 ----------
        modelBuilder.Entity<SysConfig>(entity =>
        {
            entity.HasIndex(e => e.ConfigKey).IsUnique();   // 配置键唯一（uk_config_key）
        });

        // ============================================================
        // 商品管理模块（表结构以 Jsd_order.sql 为准，列映射由实体 [Column] 特性完成）
        // ============================================================

        // ---------- prod_info 商品主表 ----------
        modelBuilder.Entity<ProdInfo>(entity =>
        {
            // 商品 → 分类（prod_category_id，NOT NULL）
            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.ProdCategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            // 商品 → 供应商（supplier_id，可空；删除供应商时置空）
            entity.HasOne(e => e.Supplier)
                  .WithMany()
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- prod_sku 商品SKU表 ----------
        modelBuilder.Entity<ProdSku>(entity =>
        {
            // sku_code 全局唯一（对应数据库 UNIQUE KEY uk_sku_code），后端自动生成
            entity.HasIndex(e => e.SkuCode).IsUnique();

            // 按商品查 SKU（对应 KEY idx_sku_prod_info）
            entity.HasIndex(e => e.ProdInfoId);
        });

        // prod_spec_item / prod_spec_value / prod_sku 的关联列（prod_info_id、spec_id）
        // 由实体 [Column] 特性映射；数据库未启用物理外键（SQL 中为注释状态），删除保护在 Service 层实现

        // ============================================================
        // 入库管理模块（stock_in_id / supplier_id / sku_id 等关联列由 [Column] 映射；
        // SQL 中未建物理外键，库存一致性由 Service 层事务保证）
        // ============================================================
        modelBuilder.Entity<LogStockIn>(entity =>
        {
            // 入库单号唯一索引（与数据库 uk_stock_in_no 一致）
            entity.HasIndex(e => e.StockInNo).IsUnique();
            entity.HasIndex(e => e.SupplierId);
            entity.HasIndex(e => e.CreateTime);

            // 入库单 → 供应商（可空；不配置物理级联，避免供应商删除影响历史单据）
            entity.HasOne(e => e.Supplier)
                  .WithMany()
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LogStockInItem>(entity =>
        {
            entity.HasIndex(e => e.StockInId);
            entity.HasIndex(e => e.ProdInfoId);
        });

        // ---------- 出库管理模块 ----------
        modelBuilder.Entity<LogStockOut>(entity =>
        {
            // 出库单号唯一索引（与数据库 uk_stock_out_no 一致）
            entity.HasIndex(e => e.StockOutNo).IsUnique();
            entity.HasIndex(e => e.OutType);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreateTime);
        });

        modelBuilder.Entity<LogStockOutItem>(entity =>
        {
            entity.HasIndex(e => e.StockOutId);
            entity.HasIndex(e => e.ProdInfoId);
        });

        modelBuilder.Entity<LogStockLog>(entity =>
        {
            entity.HasIndex(e => e.ProdInfoId);
            entity.HasIndex(e => e.SkuId);
            entity.HasIndex(e => e.ChangeType);
            entity.HasIndex(e => e.CreateTime);
        });

        // ---------- 库存盘点模块 ----------
        modelBuilder.Entity<LogStockCheck>(entity =>
        {
            // 盘点单号唯一索引（与数据库 uk_check_no 一致）
            entity.HasIndex(e => e.CheckNo).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreateTime);
        });

        modelBuilder.Entity<LogStockCheckItem>(entity =>
        {
            entity.HasIndex(e => e.CheckId);
            entity.HasIndex(e => e.ProdInfoId);
            entity.HasIndex(e => e.SkuId);
        });

        // ---------- 库存预警模块 ----------
        // 说明：log_stock_warning 是纯配置表，预警状态由 Service 实时计算，不落库；
        // "同一商品+同一SKU 只能一条"的唯一性约束由 Service 层校验（ExistsConflictAsync），
        // 数据库侧仅建普通索引（与 Jsd_order.sql 建表语句保持一致）。
        modelBuilder.Entity<LogStockWarning>(entity =>
        {
            entity.HasIndex(e => e.ProdInfoId);
            entity.HasIndex(e => e.SkuId);
            entity.HasIndex(e => e.Status);
        });

        // ---------- 订单管理模块 ----------
        // 说明：索引与 Jsd_order.sql 建表语句保持一致（uk_order_no 为唯一索引）；
        // order_no 唯一是"订单号不重复"的最后一道防线，业务侧还会先查重再重试生成。
        modelBuilder.Entity<TrxOrder>(entity =>
        {
            // 订单号唯一索引（与数据库 uk_order_no 一致）
            entity.HasIndex(e => e.OrderNo).IsUnique();
            entity.HasIndex(e => e.BuyerId);
            entity.HasIndex(e => e.OrderStatus);
            entity.HasIndex(e => e.CreateTime);
            entity.HasIndex(e => e.PayTime);
            entity.HasIndex(e => e.ReceiverPhone);
        });

        modelBuilder.Entity<TrxOrderItem>(entity =>
        {
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.ProdInfoId);
            entity.HasIndex(e => e.SkuId);
        });

        // ---------- 客户管理模块（会员） ----------
        // 索引与 customer_init.sql 建表语句保持一致：
        // uk_member_no 唯一索引是会员编号不重复的最后一道防线（业务侧也会先查重再重试）。
        modelBuilder.Entity<MemMember>(entity =>
        {
            entity.HasIndex(e => e.MemberNo).IsUnique();   // 会员编号唯一
            entity.HasIndex(e => e.UserName).IsUnique();   // 登录账号唯一
            entity.HasIndex(e => e.Level);                 // 按等级筛选
            entity.HasIndex(e => e.Status);                // 按状态筛选
            entity.HasIndex(e => e.CreateTime);            // 按注册时间范围筛选
        });

        // ---------- 财务结算模块 ----------
        // 索引与财务建表语句保持一致（idx_member_id / idx_order_id / idx_status_due_date / uk_payment_no 等）
        modelBuilder.Entity<TrxReceivable>(entity =>
        {
            entity.HasIndex(e => e.MemberId);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => new { e.Status, e.DueDate });   // 账龄/逾期查询
        });

        modelBuilder.Entity<MktPayment>(entity =>
        {
            entity.HasIndex(e => e.PaymentNo).IsUnique();        // 收款单号唯一（uk_payment_no）
            entity.HasIndex(e => e.MemberId);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<MktPaymentItem>(entity =>
        {
            entity.HasIndex(e => e.PaymentId);
            entity.HasIndex(e => e.ReceivableId);
        });

        modelBuilder.Entity<TrxPaymentLog>(entity =>
        {
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.OrderNo);
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.Status);
        });

        // ============================================================
        // 采购管理模块（表结构以 purchase_module.sql 为准，列映射由实体 [Column] 特性完成）
        // ============================================================

        // ---------- pur_order 采购订单主表 ----------
        modelBuilder.Entity<PurOrder>(entity =>
        {
            // 采购单号唯一索引（与数据库 uk_order_no 一致）
            entity.HasIndex(e => e.OrderNo).IsUnique();
            entity.HasIndex(e => e.SupplierId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.OrderDate);

            // 采购订单 → 供应商（删除供应商时禁止级联，保留历史单据）
            entity.HasOne(e => e.Supplier)
                  .WithMany()
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- pur_order_item 采购订单明细表 ----------
        modelBuilder.Entity<PurOrderItem>(entity =>
        {
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.MaterialId);   // material_id 实际关联 prod_sku.id

            // 明细 → 订单（级联删除，删订单时明细一并删除）
            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- pur_inbound 采购入库单主表 ----------
        modelBuilder.Entity<PurInbound>(entity =>
        {
            // 入库单号唯一索引（与数据库 uk_inbound_no 一致）
            entity.HasIndex(e => e.InboundNo).IsUnique();
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.Status);

            // 入库单 → 采购订单（删除订单时禁止级联，保留历史入库单）
            entity.HasOne(e => e.Order)
                  .WithMany()
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- pur_inbound_item 采购入库明细表 ----------
        modelBuilder.Entity<PurInboundItem>(entity =>
        {
            entity.HasIndex(e => e.InboundId);
            entity.HasIndex(e => e.OrderItemId);   // 精确关联采购订单明细行

            // 入库明细 → 入库单（级联删除）
            entity.HasOne(e => e.Inbound)
                  .WithMany(i => i.Items)
                  .HasForeignKey(e => e.InboundId)
                  .OnDelete(DeleteBehavior.Cascade);

            // 入库明细 → 采购订单明细（删除订单明细时禁止级联）
            entity.HasOne(e => e.OrderItem)
                  .WithMany()
                  .HasForeignKey(e => e.OrderItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- pur_payable 应付账款表 ----------
        modelBuilder.Entity<PurPayable>(entity =>
        {
            // 应付单号唯一索引（与数据库 uk_payable_no 一致）
            entity.HasIndex(e => e.PayableNo).IsUnique();
            entity.HasIndex(e => e.SupplierId);
            entity.HasIndex(e => e.RelatedOrderId);
            entity.HasIndex(e => e.Status);

            // 应付 → 供应商（删除供应商时禁止级联）
            entity.HasOne(e => e.Supplier)
                  .WithMany()
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.Restrict);

            // 应付 → 采购订单（删除订单时置空关联，保留应付记录用于财务对账）
            entity.HasOne(e => e.Order)
                  .WithMany()
                  .HasForeignKey(e => e.RelatedOrderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ============================================================
        // 售后管理模块（表结构以 Jsd_order.sql 为准，列映射由实体 [Column] 特性完成）
        // ============================================================

        // ---------- trx_refund 退款申请表 ----------
        modelBuilder.Entity<TrxRefund>(entity =>
        {
            entity.HasIndex(e => e.RefundNo).IsUnique();   // 退款单号唯一（uk_refund_no）
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.MemberId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreateTime);

            // 退款单 → 订单（删除订单时禁止级联，保留历史退款单）
            entity.HasOne(e => e.Order)
                  .WithMany()
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- trx_return 退货单表 ----------
        modelBuilder.Entity<TrxReturn>(entity =>
        {
            entity.HasIndex(e => e.ReturnNo).IsUnique();   // 退货单号唯一（uk_return_no）
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.RefundId);
            entity.HasIndex(e => e.MemberId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreateTime);

            // 退货单 → 退款单（删除退款单时禁止级联）
            entity.HasOne(e => e.Refund)
                  .WithMany()
                  .HasForeignKey(e => e.RefundId)
                  .OnDelete(DeleteBehavior.Restrict);

            // 退货单 → 明细（级联删除）
            entity.HasMany(e => e.Items)
                  .WithOne(i => i.Return)
                  .HasForeignKey(i => i.ReturnId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- trx_return_item 退货明细表 ----------
        modelBuilder.Entity<TrxReturnItem>(entity =>
        {
            entity.HasIndex(e => e.ReturnId);
            entity.HasIndex(e => e.OrderItemId);
            entity.HasIndex(e => e.ProdInfoId);
            entity.HasIndex(e => e.SkuId);
        });

        // ---------- trx_refund_log 售后日志表 ----------
        modelBuilder.Entity<TrxRefundLog>(entity =>
        {
            entity.HasIndex(e => e.RefundId);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.CreateTime);
        });

        // ============================================================
        // 价格策略与会员余额模块（表结构以 price_balance_module.sql 为准，
        // 列映射由实体 [Column] 特性完成；数据库未建物理外键，一致性由 Service 层事务保证）
        // ============================================================

        // ---------- mem_member_level 客户等级表 ----------
        modelBuilder.Entity<MemMemberLevel>(entity =>
        {
            entity.HasIndex(e => e.LevelCode).IsUnique();   // 等级编码唯一（uk_level_code）
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.SortOrder);
        });

        // ---------- price_strategy 价格策略主表 ----------
        modelBuilder.Entity<PriceStrategy>(entity =>
        {
            entity.HasIndex(e => e.StrategyNo).IsUnique();  // 策略编号唯一（uk_strategy_no）
            entity.HasIndex(e => e.StrategyType);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.EffectiveDate);
            entity.HasIndex(e => e.ExpireDate);

            // 策略 → 规则（删除策略时级联删除其下规则，避免孤儿规则参与取价）
            entity.HasMany(e => e.Rules)
                  .WithOne(r => r.Strategy)
                  .HasForeignKey(r => r.StrategyId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- price_rule 价格规则表 ----------
        modelBuilder.Entity<PriceRule>(entity =>
        {
            entity.HasIndex(e => e.StrategyId);
            entity.HasIndex(e => e.MemMemberId);            // 客户专属价定位
            entity.HasIndex(e => e.CustomerLevelId);        // 客户等级价定位
            entity.HasIndex(e => e.Status);
        });

        // ---------- price_rule_item 价格规则明细表 ----------
        modelBuilder.Entity<PriceRuleItem>(entity =>
        {
            entity.HasIndex(e => e.RuleId);
            entity.HasIndex(e => e.MaterialId);             // 指定商品定位

            // 明细 → 规则（级联删除，规则重建时明细一并清理）
            entity.HasOne(e => e.Rule)
                  .WithMany(r => r.Items)
                  .HasForeignKey(e => e.RuleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- price_change_log 价格变更日志表 ----------
        modelBuilder.Entity<PriceChangeLog>(entity =>
        {
            entity.HasIndex(e => e.StrategyId);
            entity.HasIndex(e => e.RuleId);
            entity.HasIndex(e => e.OperationType);
            entity.HasIndex(e => e.OperateTime);
        });

        // ---------- order_price_snapshot 订单价格快照表 ----------
        modelBuilder.Entity<OrderPriceSnapshot>(entity =>
        {
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.OrderItemId);
            entity.HasIndex(e => e.MaterialId);
        });

        // ---------- mem_recharge 充值订单表 ----------
        modelBuilder.Entity<MemRecharge>(entity =>
        {
            entity.HasIndex(e => e.RechargeNo).IsUnique();  // 充值单号唯一（uk_recharge_no）
            entity.HasIndex(e => e.TransactionId).IsUnique(); // 【幂等键】微信流水号唯一，防重复入账
            entity.HasIndex(e => e.MemMemberId);
            entity.HasIndex(e => e.Status);
        });

        // ---------- mkt_balance_log 余额流水表 ----------
        modelBuilder.Entity<MktBalanceLog>(entity =>
        {
            entity.HasIndex(e => e.MemMemberId);
            entity.HasIndex(e => e.ChangeType);
            entity.HasIndex(e => new { e.RelatedType, e.RelatedId });  // 关联业务反查
            entity.HasIndex(e => e.CreateTime);
        });
    }
}
