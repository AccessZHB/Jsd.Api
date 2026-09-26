using System.Text;
using Jsd.Api.Entities;
using Jsd.Api.Filters;
using Jsd.Api.Middlewares;
using Jsd.Api.Repositories;
using Jsd.Api.Services;
using Jsd.Api.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. 注册数据库上下文（Pomelo MySQL 提供程序）
// ============================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString!)));

// ============================================================
// 2. 依赖注入 —— 仓储层（Repository）
//    泛型仓储用开放泛型注册：注入 IRepository<SysRole> 时自动给 Repository<SysRole>
// ============================================================
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ISysUserRepository, SysUserRepository>();
builder.Services.AddScoped<ISysMenuRepository, SysMenuRepository>();

// 基础数据 & 商品管理模块 —— 专用仓储（其余模块直接用泛型 IRepository<T>）
builder.Services.AddScoped<ISupSupplierRepository, SupSupplierRepository>();
builder.Services.AddScoped<IProdInfoRepository, ProdInfoRepository>();

// 入库管理模块 —— 专用仓储
builder.Services.AddScoped<ILogStockInRepository, LogStockInRepository>();
builder.Services.AddScoped<ILogStockInItemRepository, LogStockInItemRepository>();

// 出库管理模块 —— 专用仓储
builder.Services.AddScoped<ILogStockOutRepository, LogStockOutRepository>();
builder.Services.AddScoped<ILogStockOutItemRepository, LogStockOutItemRepository>();

// 库存盘点模块 —— 专用仓储
builder.Services.AddScoped<ILogStockCheckRepository, LogStockCheckRepository>();
builder.Services.AddScoped<ILogStockCheckItemRepository, LogStockCheckItemRepository>();

// 库存预警模块 —— 专用仓储
builder.Services.AddScoped<IStockWarningRepository, StockWarningRepository>();

// 库存查询模块 —— 只读仓储（5 张现有表关联查询）
builder.Services.AddScoped<IStockQueryRepository, StockQueryRepository>();

// 订单管理模块 —— 专用仓储
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();

// 客户管理模块 —— 专用仓储
builder.Services.AddScoped<IMemMemberRepository, MemMemberRepository>();

// 财务结算模块 —— 专用仓储（应收 + 收款 + 支付日志）
builder.Services.AddScoped<IReceivableRepository, ReceivableRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

// 财务结算模块 —— FluentValidation 校验器（由 Service 显式调用 ValidateAndThrow）
builder.Services.AddScoped<PaymentCreateValidator>();
builder.Services.AddScoped<WriteOffInputValidator>();

// 采购管理模块 —— FluentValidation 校验器（由 Service 显式调用 ValidateAndThrow）
builder.Services.AddScoped<PurchaseOrderCreateValidator>();
builder.Services.AddScoped<PurchaseOrderItemValidator>();
builder.Services.AddScoped<InboundCreateValidator>();
builder.Services.AddScoped<PayPaymentValidator>();

// ============================================================
// 3. 依赖注入 —— 服务层（Service）
// ============================================================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISysMenuService, SysMenuService>();
builder.Services.AddScoped<ISysUserService, SysUserService>();
builder.Services.AddScoped<ISysRoleService, SysRoleService>();
builder.Services.AddSingleton<JwtService>();          // JWT 无状态，单例即可
builder.Services.AddScoped<CurrentUserService>();     // 依赖 HttpContext，按请求域
builder.Services.AddHttpContextAccessor();            // 让 Service 能拿到 HttpContext

// 基础数据 & 商品管理模块 —— 服务层
builder.Services.AddScoped<ISupSupplierService, SupSupplierService>();
builder.Services.AddScoped<IProdCategoryService, ProdCategoryService>();
builder.Services.AddScoped<IProdInfoService, ProdInfoService>();
builder.Services.AddScoped<IProdSpecService, ProdSpecService>();
builder.Services.AddScoped<IProdSkuService, ProdSkuService>();

// 入库管理模块 —— 服务层
builder.Services.AddScoped<IStockInService, StockInService>();
builder.Services.AddScoped<IStockOutService, StockOutService>();

// 库存盘点模块 —— 服务层
builder.Services.AddScoped<IStockCheckService, StockCheckService>();

// 库存预警模块 —— 服务层
builder.Services.AddScoped<IStockWarningService, StockWarningService>();

// 库存查询模块 —— 服务层（只读）
builder.Services.AddScoped<IStockQueryService, StockQueryService>();

// 订单管理模块 —— 服务层（状态流转 + 库存/销量联动）
builder.Services.AddScoped<IOrderService, OrderService>();

// 客户管理模块 —— 服务层
builder.Services.AddScoped<IMemMemberService, MemMemberService>();

// 财务结算模块 —— 服务层（应收账款 + 收款核销 + 支付回调）
builder.Services.AddScoped<IReceivableService, ReceivableService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// 采购管理模块 —— 服务层（采购订单 + 入库 + 应付）
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IInboundService, InboundService>();
builder.Services.AddScoped<IPayableService, PayableService>();

// 售后管理模块 —— 仓储层（退款 + 退货专用仓储；日志/明细用泛型 IRepository<T>）
builder.Services.AddScoped<IRefundRepository, RefundRepository>();
builder.Services.AddScoped<IReturnRepository, ReturnRepository>();

// 售后管理模块 —— FluentValidation 校验器（Service 层显式 ValidateAndThrow）
builder.Services.AddScoped<CreateRefundValidator>();
builder.Services.AddScoped<ApproveRefundValidator>();
builder.Services.AddScoped<CreateReturnValidator>();
builder.Services.AddScoped<ReceiveReturnValidator>();

// 售后管理模块 —— 服务层（退款 + 退货 + 日志）
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IAfterSaleLogService, AfterSaleLogService>();

// 价格策略与会员余额模块 —— 仓储层（等级 / 策略 / 充值 / 余额流水专用仓储；
// 规则、明细、变更日志、价格快照用泛型 IRepository<T>）
builder.Services.AddScoped<IMemberLevelRepository, MemberLevelRepository>();
builder.Services.AddScoped<IPriceStrategyRepository, PriceStrategyRepository>();
builder.Services.AddScoped<IMemRechargeRepository, MemRechargeRepository>();
builder.Services.AddScoped<IBalanceLogRepository, BalanceLogRepository>();

// 价格策略与会员余额模块 —— FluentValidation 校验器（Service 层显式 ValidateAndThrow）
builder.Services.AddScoped<CreateMemberLevelValidator>();
builder.Services.AddScoped<UpdateMemberLevelValidator>();
builder.Services.AddScoped<CreatePriceStrategyValidator>();
builder.Services.AddScoped<UpdatePriceStrategyValidator>();
builder.Services.AddScoped<PriceRuleBatchSaveValidator>();
builder.Services.AddScoped<QuotationRequestValidator>();
builder.Services.AddScoped<RechargeCreateValidator>();
builder.Services.AddScoped<AdminAdjustBalanceValidator>();
builder.Services.AddScoped<FreezeBalanceValidator>();
builder.Services.AddScoped<UnfreezeBalanceValidator>();
builder.Services.AddScoped<DeductBalanceValidator>();

// 价格策略与会员余额模块 —— 服务层（价格策略 / 取价引擎 / 会员余额）
builder.Services.AddScoped<IPriceService, PriceService>();
builder.Services.AddScoped<IBalanceService, BalanceService>();

// ============================================================
// 字典管理模块（字典类型 / 字典数据 / 字典缓存）
// ============================================================
// 缓存默认走进程内 MemoryCache（MemoryDictCacheStore）。多实例部署时把下面两行
// 换成 Redis 实现即可，上层 DictCacheHelper / DictCacheService 一行都不用改。
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IDictCacheStore, MemoryDictCacheStore>();
builder.Services.AddSingleton<DictCacheHelper>();          // 缓存 Key / TTL 策略
builder.Services.AddScoped<IDictCacheService, DictCacheService>();
builder.Services.AddHostedService<DictCachePreloadService>();  // 应用启动预热字典缓存

builder.Services.AddScoped<IDictTypeRepository, DictTypeRepository>();
builder.Services.AddScoped<IDictDataRepository, DictDataRepository>();

builder.Services.AddScoped<IDictTypeService, DictTypeService>();
builder.Services.AddScoped<IDictDataService, DictDataService>();

// 字典管理模块 —— FluentValidation 校验器（Service 层显式 ValidateAndThrow）
builder.Services.AddScoped<CreateDictTypeValidator>();
builder.Services.AddScoped<UpdateDictTypeValidator>();
builder.Services.AddScoped<CreateDictDataValidator>();
builder.Services.AddScoped<UpdateDictDataValidator>();

// ============================================================
// 系统管理模块（操作日志 / 登录日志 / 登录安全策略）
// ============================================================
builder.Services.AddMemoryCache();   // 验证码（TTL 5 分钟）/ refresh jti / 密码版本缓存

// 系统管理模块 —— 仓储层（操作日志 + 登录日志专用仓储；sys_config 用泛型仓储即可，这里直接注入 DbContext）
builder.Services.AddScoped<ISysOperationLogRepository, SysOperationLogRepository>();
builder.Services.AddScoped<ISysLoginLogRepository, SysLoginLogRepository>();

// 系统管理模块 —— FluentValidation 校验器（Controller 显式 ValidateAndThrow）
builder.Services.AddScoped<BatchDeleteValidator>();
builder.Services.AddScoped<CleanLogValidator>();
builder.Services.AddScoped<SecurityConfigValidator>();
builder.Services.AddScoped<ChangePasswordValidator>();
builder.Services.AddScoped<RefreshTokenValidator>();

// 系统管理模块 —— 操作日志异步写入（Channel 生产者 + 后台消费者，落库不阻塞业务）
builder.Services.AddSingleton<OperationLogWriter>();
builder.Services.AddHostedService<OperationLogBackgroundService>();

// 系统管理模块 —— 服务层（登录安全策略 / 日志查询维护）
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddScoped<ISystemLogService, SystemLogService>();

// ============================================================
// 4. AutoMapper（自动扫描当前程序集中的 MappingProfile）
// ============================================================
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// ============================================================
// 5. JWT 身份认证
// ============================================================
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,              // 校验签发方
            ValidateAudience = true,            // 校验接收方
            ValidateLifetime = true,            // 校验过期时间
            ValidateIssuerSigningKey = true,    // 校验签名密钥
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!))
        };
    });
builder.Services.AddAuthorization();

// ============================================================
// 6. 跨域 CORS（开发期允许前端任意地址访问）
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ============================================================
// 7. Swagger 接口文档（支持在页面上输入 JWT 调试）
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "镜速订产品订货系统 API",
        Version = "v1",
        Description = "镜速订 B2B 订货系统 —— 系统管理模块接口文档"
    });

    // Swagger 页面上的 Bearer Token 输入框
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Description = "JWT 认证，请输入：Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });
    options.OperationFilter<SecurityRequirementsOperationFilter>();
});

// ============================================================
// 8. 控制器
//    显式声明 JSON 序列化采用 camelCase 驼峰命名（ASP.NET Core 默认即此策略，
//    此处显式配置以消除歧义，确保前端 camelCase 字段能正确绑定到后端 PascalCase 属性）。
// ============================================================
builder.Services.AddControllers(options =>
{
    // 系统管理模块：全局注册操作日志采集过滤器（仅对标记 [OperationLog] 的写操作接口生效）
    options.Filters.Add<OperationLogFilter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// 确保静态文件根目录存在（图片上传保存到 wwwroot/upload）
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "upload"));

var app = builder.Build();

// ============================================================
// HTTP 请求管道配置
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Jsd.Api v1");
    });
}

app.UseCors("AllowAll");            // 跨域
app.UseStaticFiles();               // 静态文件（wwwroot，上传的图片通过 /upload/... 访问，img 标签不带 JWT 故放行）
app.UseAuthentication();            // 认证（先）
app.UseMiddleware<TokenVersionMiddleware>();   // 密码版本校验：改密后旧 JWT 强制下线（系统管理模块）
app.UseAuthorization();             // 授权（后）
app.MapControllers();               // 映射控制器路由

app.Run();
