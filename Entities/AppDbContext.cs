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

    /// <summary>商品图片（prod_image：主图/轮播图/详情图）</summary>
    public DbSet<ProdImage> ProdImages => Set<ProdImage>();

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
    }
}
