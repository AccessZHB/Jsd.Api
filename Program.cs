using System.Text;
using Jsd.Api.Entities;
using Jsd.Api.Filters;
using Jsd.Api.Middlewares;
using Jsd.Api.Repositories;
using Jsd.Api.Jobs;
using Jsd.Api.Services;
using Jsd.Api.Validators;
using Quartz;
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

// 发票与税务管理模块 —— FluentValidation 校验器（由 Service 显式调用 ValidateAndThrow）
builder.Services.AddScoped<SaveCustomerInvoiceValidator>();
builder.Services.AddScoped<ApplyInvoiceValidator>();
builder.Services.AddScoped<CancelInvoiceValidator>();
builder.Services.AddScoped<IssueInvoiceValidator>();

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
// 系统管理 - 系统配置模块（9 个接口）
// ============================================================
// 缓存默认走进程内 MemoryCache（MemorySysConfigCacheStore）。多实例部署时把下面两行
// 换成 Redis 实现即可，上层 SysConfigCacheHelper / SysConfigService 一行都不用改。
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISysConfigCacheStore, MemorySysConfigCacheStore>();
builder.Services.AddSingleton<SysConfigCacheHelper>();            // 缓存 Key / TTL 策略
builder.Services.AddScoped<ISysConfigRepository, SysConfigRepository>();
builder.Services.AddScoped<ISysConfigService, SysConfigService>();
builder.Services.AddHostedService<SysConfigPreloadService>();     // 应用启动预热配置缓存

// 系统配置模块 —— FluentValidation 校验器（Service / Controller 显式 ValidateAndThrow）
builder.Services.AddScoped<CreateSysConfigValidator>();
builder.Services.AddScoped<UpdateSysConfigValidator>();
builder.Services.AddScoped<ConfigKeysValidator>();

// ============================================================
// 自动任务模块（定时任务中心，9 个接口 + Quartz 调度器）
// ============================================================
// 调度器持久化到 qrtz_* 11 张表（由 Jsd_order.sql 建好），业务侧只读写 sys_job / sys_job_log。
// 集群部署时多个实例会共享同一套 qrtz 表，Quartz 自带分布式选主，无需额外处理。
builder.Services.AddQuartz(options =>
{
    options.UsePersistentStore(store =>
    {
        // 用 JSON 序列化 JobData（Quartz.Serialization.Json 提供的扩展）。
        // 默认的 binary 序列化跨实例（换机器 / 换版本）会直接反序列化失败。
        store.UseNewtonsoftJsonSerializer();

        // 用 MySqlConnector provider 而不是 MySql：内置的 MySql provider 需要独立的
        // MySql.Data 程序集，本项目走 EF Core/Pomelo，只会带入 MySqlConnector，
        // 选错 provider 会在启动期报 "Error while reading metadata information for provider 'MySql'"。
        // 表前缀仍是 Quartz 默认 QRTZ_，与 Jsd_order.sql 的建表前缀一致。
        store.UseMySqlConnector(o => o.ConnectionString = connectionString ?? string.Empty);

        // 用字符串存 JobData 里的值（与 JobDataHelper 的读法保持一致）；
        // 关掉后二进制序列化也不再参与，换实例不会炸。
        store.UseProperties = false;
        // 建表脚本用 QRTZ_ 前缀，与 UseMySql 的默认前缀一致，这里显式关掉 schema 校验的干扰
        store.PerformSchemaValidation = false;
    });
});

// 全局任务监听器统一在 JobSchedulePreloadService 里挂到 Scheduler 上
// （Quartz 3.x 的 AddQuartzListener 扩展在 3.12.0 里已移除，改为手动挂载）。

// 调度器托管服务（应用启停时自动 Start/Shutdown 调度器）
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

// ⚠️ Quartz 3.12 的 AddQuartz 只注册了 ISchedulerFactory，**没有注册 IScheduler**，
// 必须自己补这一行。少了它，凡是构造时注入 IScheduler 的服务（JobService）会在
// 首次解析时报 "Unable to resolve service for type 'Quartz.IScheduler'"——
// 而且是启动期抛在 IHostedService 里，表现为「应用照常启动但所有定时任务不生效」，极难发现。
// 用 Singleton 保证每次解析拿到同一个 IScheduler 实例（QuartzHostedService 启动时已取过一次）。
builder.Services.AddSingleton<IScheduler>(sp =>
    sp.GetRequiredService<ISchedulerFactory>().GetScheduler().GetAwaiter().GetResult());

builder.Services.AddScoped<ISysJobRepository, SysJobRepository>();
builder.Services.AddScoped<ISysJobLogRepository, SysJobLogRepository>();
builder.Services.AddScoped<IJobService, JobService>();

// 自动任务模块 —— FluentValidation 校验器（Service / Controller 显式 ValidateAndThrow）
builder.Services.AddScoped<CreateJobValidator>();
builder.Services.AddScoped<UpdateJobValidator>();
builder.Services.AddScoped<JobLogQueryValidator>();

builder.Services.AddHostedService<JobSchedulePreloadService>();  // 启动后把 status=1 的任务注册进调度器

// ============================================================
// 物流轨迹查询模块（3 个 GET 接口 + 快递100 对接 + 进程内缓存）
// ============================================================
// 命名 HttpClient：超时 10s，超时在 LogisticsService 内重试 1 次。
builder.Services.AddHttpClient("kuaidi", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<ILogisticsService, LogisticsService>();

// ============================================================
// 微信支付 主动查单与补单模块（V3：真实验签 + AES 解密 + 主动查单；Quartz 定时查单）
// ============================================================
// 命名 HttpClient：调微信支付 V3 接口（查询订单等），超时 10s。
// 配置项来自 appsettings.json 的 WeChatPay 节（未配置时 PayService 直接 Fail，不影响启动）。
builder.Services.AddHttpClient("wechatpay", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<IPayService, PayService>();

// ============================================================
// 微信支付 T+1 对账模块（拉账单 → 解析 CSV → 双向勾稽 → 差异核查；Quartz 每天凌晨3点自动跑）
// ============================================================
// 复用 WeChatPay 节配置 + wechatpay 客户端申请账单；账单文件下载量大且慢，单独给它 180s 超时。
builder.Services.AddHttpClient("wechatbill", c => c.Timeout = TimeSpan.FromSeconds(180));
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();

// ============================================================
// 发票与税务管理模块（开票抬头维护 → 开票申请 → 财务回写票号 / 第三方平台开票 → 作废红冲）
// ============================================================
// 第三方开票平台（百望云/航信等）客户端：未配置 Invoice 节时不调用，走人工开票分支。
builder.Services.AddHttpClient("invoice", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IInvoiceService, InvoiceService>();


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
