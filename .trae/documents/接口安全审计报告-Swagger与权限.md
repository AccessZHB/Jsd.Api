# Jsd.Api 接口安全审计报告（预生产级）

> 审计对象：`f:\WorkSpace\Jsd\Jsd.Api`（ASP.NET Core net9.0 + EF Core + JWT + Swashbuckle）
> 审计依据：`Program.cs`、`Attributes/PermissionFilter.cs`、56 个 Controller、`appsettings.json`
> 说明：用户上传的 OpenAPI JSON 为空文件，本次改为源码级审计（覆盖面等同且更准确）。
> 本地环境：http://localhost:5266

---

## 0. 关键机制说明（理解后续结论的前提）

`PermissionFilter` 是全局 `IAsyncActionFilter`，其判定逻辑为：

| 场景 | 行为 | 位置 |
|---|---|---|
| 会员令牌 + 路径命中白名单 | **直接放行**（跳过角色权限校验） | [PermissionFilter.cs#L56-L80](../../Attributes/PermissionFilter.cs#L56-L80) |
| 会员令牌 + 路径不在白名单 | 403 | 同上 |
| 管理员令牌 + Action 未标 `[Permission]` | **直接放行**（仅需登录） | [PermissionFilter.cs#L84-L89](../../Attributes/PermissionFilter.cs#L84-L89) |
| 管理员令牌 + 标了 `[Permission]` | 查 `sys_role_menu`，无权限 403 | 同上 |

**两个"默认放行"分支 = 本次绝大多数 P0 的根因。**

---

## 0.5 OpenAPI 实测校验（补充：基于用户提供的 `/swagger/v1/swagger.json`，831KB）

| 项目 | 实测值 |
|---|---|
| OpenAPI 版本 | 3.0.1 |
| 路径数 / 操作数 | 246 个 Path / **282 个 Operation** |
| Schema 数 | 428 |
| 安全方案 | 仅 1 个 `apiKey`（name=Authorization, in=header），命名 `oauth2` |
| 根级 `security` | **未设置**（每个 operation 各自声明） |
| 匿名操作数 | **14** |
| 带认证操作数 | 268 |
| 敏感示例数据 | **0**（全文件无 `example`、无手机号、无邮箱、无身份证号、无 Token 样例） |

### 0.5.1 14 个匿名接口逐条裁定

| # | 接口 | 裁定 |
|---|---|---|
| 1 | `POST /api/auth/login` | ✅ 合理（登录） |
| 2 | `POST /api/auth/captcha` | ✅ 合理 |
| 3 | `POST /api/auth/refresh` | ✅ 合理（refresh token 在 body） |
| 4 | `POST /api/member/login` | ✅ 合理 |
| 5 | `POST /api/member/captcha` | ✅ 合理 |
| 6 | `GET /api/prod-info/page` | ✅ 合理（商品浏览） |
| 7 | `GET /api/prod-info/{id}/detail` | ✅ 合理 |
| 8 | `GET /api/prod/category/tree` | ✅ 合理 |
| 9 | `GET /api/banner/list` | ✅ 合理 |
| 10 | `GET /api/cms/announcement/latest` | ✅ 合理 |
| 11 | `POST /api/pay/notify` | ✅ 合理（已验签） |
| 12 | `POST /api/recharge/wechat-notify` | ✅ 合理（已验签） |
| 13 | `POST /api/payment-callback/wechat` | ⚠️ 应删除（无验签，仅靠配置开关关闭，见 P1-08） |
| 14 | `GET /api/system/security/config` | 🔴 **异常：安全策略配置对外匿名可读（见 P1-13）** |

### 0.5.2 OpenAPI 暴露的"默认匿名"倾向

根级 `security` 为空、由每个 operation 单独声明，意味着**任何新增接口只要忘了加 `[Authorize]`，就会自动变成匿名接口，且在 Swagger 中毫无警示**。这与 `PermissionFilter` 的「未标注 `[Permission]` 即放行」形成叠加，是同一类设计缺陷在文档层的映射。建议在 CI 中加校验：除白名单路径外，`swagger.json` 中不允许出现 `security` 缺失的 operation。

---

## 1. 高危问题清单

### 🔴 P0-01 会员令牌可改写 SKU 价格 / 商品 / 横幅 / 公告（直接资金损失）

- **问题描述**：会员端白名单用「前缀正则」放行整个控制器，而 `ProdSkuController`、`ProdInfoController`、`BannerController`、`CmsAnnouncementController` 的**写接口没有任何 `[Permission]`** 且与读接口同前缀。会员令牌命中白名单后被 `await next()` 直接放行。
- **风险等级**：**P0**
- **影响范围**：任何注册会员（小程序端即可）→ 改任意 SKU 售价 → 以 0.01 元下单 → 直接资金损失；可删除/下架全平台商品；可篡改首页横幅与公告（钓鱼）。
- **证据**：
  - 白名单：`^/api/prod/(sku|category)(/|$)`、`^/api/prod-info(/|$)`、`^/api/banner(/|$)`、`^/api/cms(/|$)`（[PermissionFilter.cs#L149-L152](../../Attributes/PermissionFilter.cs#L149-L152)）
  - `ProdSkuController`：`[Route("api/prod/sku")]` + `[HttpPost]` / `[HttpPut]` / `[HttpDelete("{id}")]`，**全控制器 0 个 `[Permission]`**（[ProdSkuController.cs#L16-L88](../../Controllers/ProdSkuController.cs#L16-L88)）
  - `ProdInfoController`：`[HttpPost]` / `[HttpPut("{id}/status")]` / `[HttpDelete("{id}")]` 无权限（[ProdInfoController.cs#L58-L94](../../Controllers/ProdInfoController.cs#L58-L94)）
- **复现思路**：用会员令牌 `POST /api/prod/sku`（或 `PUT /api/prod/sku`）提交 `{ "id": 1, "salePrice": 0.01 }` → 返回 200 且落库；随后正常下单即按 0.01 成交。
- **修复建议**：白名单从「控制器前缀」收敛为「精确方法+路径」；写接口补 `[Permission]` 并放在会员不可达的路径下。
- **代码改动方向**：
```csharp
// ① 白名单改为精确匹配，只放行读接口
new(@"^/api/prod/sku/(list|\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
new(@"^/api/prod-info/(page|\d+/detail)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
// 移除 ^/api/banner(/|$) 与 ^/api/cms(/|$) 的宽前缀，改为仅 list/latest
new(@"^/api/banner/list$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
new(@"^/api/cms/announcement/(latest|list)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),

// ② ProcSkuController 写接口补权限
[HttpPut]
[Permission("prod:sku:edit")]
public async Task<ApiResponse<object>> Update([FromBody] ProdSkuUpdateDto dto)
```

---

### 🔴 P0-02 角色管理零权限 → 任意后台账号自我提权为超管

- **问题描述**：`SysRoleController` 全部 8 个 Action **均无 `[Permission]`**，且 `AssignMenus` 可任意给角色分配菜单。
- **风险等级**：**P0**
- **影响范围**：任何能登录后台的低权限账号（运营/仓库）→ 给自己的角色勾选全部菜单 → 等同于超管；也可新建角色并分配给自己。
- **证据**：`[Authorize]` 类级，`Create`/`Update`/`Delete`/`DeleteBatch`/`AssignMenus` 均无权限特性（[SysRoleController.cs#L15-L111](../../Controllers/SysRoleController.cs#L15-L111)）
- **复现思路**：仓库账号登录 → `PUT /api/role/assign-menus` body 含全部菜单 ID → 重新登录后拥有财务/系统管理全部权限。
- **修复建议**：全部补 `system:role:*`；`AssignMenus` 需额外校验「不可授予超出自身权限范围的菜单」。
- **代码改动方向**：
```csharp
[HttpPost]
[Permission(SysRolePermissions.Add)]        // system:role:add
public async Task<ApiResponse<object>> Create([FromBody] SysRoleCreateDto dto)

[HttpPut("assign-menus")]
[Permission(SysRolePermissions.Assign)]     // system:role:assign
public async Task<ApiResponse<object>> AssignMenus([FromBody] SysRoleAuthDto dto)
```

---

### 🔴 P0-03 系统用户管理：仅 Create 有权限，重置密码/删除/改用户无权限 → 超管被接管

- **问题描述**：`SysUserController` 只有 `Create` 标了 `[Permission(SysUserPermissions.Add)]`，`Update`、`Delete`、`ResetPassword`、`DeleteBatch` 全部裸奔。
- **风险等级**：**P0**
- **影响范围**：任意后台账号 `PUT /api/user/{超管id}/reset-password` → 直接接管超管；或删除/篡改其他账号。
- **证据**：[SysUserController.cs#L56-L107](../../Controllers/SysUserController.cs#L56-L107)（第 56 行有 `[Permission]`，第 67/80/96/106 行均无）
- **复现思路**：用仓库账号调 `reset-password` 接口重置 id=1 的超管密码 → 用新密码登录。
- **修复建议**：补 `system:user:edit/delete/reset-password`；重置密码类再加 `[OperationLog]` 审计。
- **代码改动方向**：
```csharp
[HttpPut("{id}")]
[Permission(SysUserPermissions.Edit)]
public async Task<ApiResponse<object>> Update(int id, [FromBody] SysUserUpdateDto dto)

[HttpPut("{id}/reset-password")]
[Permission(SysUserPermissions.ResetPassword)]
[OperationLog("重置用户密码")]
public async Task<ApiResponse<object>> ResetPassword(long id, [FromBody] ResetPasswordDto dto)
```

---

### 🔴 P0-04 JWT 密钥硬编码在仓库 → 泄露即可伪造任意身份（含超管）

- **问题描述**：`appsettings.json` 明文提交 `Jwt.SecretKey`，且与数据库 `root/123456` 同文件。
- **风险等级**：**P0**
- **影响范围**：拿到仓库读权限（或任何一次代码外泄）的人可离线签发 `{ roleId:1, isSuper:"1" }` 的 JWT，绕过全部认证与 `[Permission]` 校验，直接操作资金接口。
- **证据**：[appsettings.json#L23-L28](../../appsettings.json#L23-L28)、[appsettings.json#L11-L13](../../appsettings.json#L11-L13)
- **复现思路**：用已知 SecretKey 按同 Issuer/Audience 生成 token，访问 `/api/balance/admin-adjust` 成功。
- **修复建议**：密钥移出仓库（环境变量 / User Secrets / KMS），启动时校验长度与占位值；泄露过的密钥需**立即轮换**（轮换会让现有 token 全部失效）。
- **代码改动方向**：
```csharp
var jwtSecret = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new InvalidOperationException("Jwt:SecretKey 未配置或长度不足 32 位");
// 生产用：环境变量 Jwt__SecretKey 注入，appsettings.json 只保留空串
```

---

### 🟠 P1-05 一批后台控制器完全没有 `[Permission]`（仅登录即可写）

| 控制器 | 高危 Action | 影响 |
|---|---|---|
| `SysConfigController` | Create/Update/Delete/RefreshCache/ClearCache | 篡改系统配置（含安全策略开关） |
| `TaskJobController` | Create/Update/Start/Stop/Run | 任意创建并执行 Quartz 任务 |
| `CustomerController` | Create/Update/Delete/ResetPassword/{id}/reset-password | 重置任意会员密码 = 会员账号接管；客户资料批量泄露 |
| `DictTypeController` / `DictDataController` / `DictCacheController` | 全部写接口 | 篡改业务字典，影响全站枚举展示与校验 |
| `ProdInfoController` | Create/Update/SetStatus/Delete | 改商品、改价（与 P0-01 叠加） |
| `ProdSkuController` / `ProdSpecItemController` / `ProdSpecValueController` | 全部写接口 | 改 SKU 价格与规格 |
| `SupSupplierController` | Create/Update/Delete | 篡改供应商 |
| `PaymentLogController` | GetByOrder | 读取全平台支付流水 |
| `AfterSaleLogController` | GetByRefund/GetByReturn | 读取全平台售后日志 |
| `SystemOperationLogController` / `SystemLoginLogController` | 查询/清理 | 读取并清除审计日志（反取证） |
| `AdminUploadController` / `FileController` | 上传/下载 | 若扩展名与大小未白名单，存在上传恶意文件风险 |

- **修复建议**：统一补 `[Permission]`；**建议把 `PermissionFilter` 改为「默认拒绝」**（未标注 `[Permission]` 的后台接口直接 403，用 `[PublicEndpoint]` 显式放行），从机制上杜绝新增接口漏配。

---

### 🟠 P1-06 无速率限制（登录/验证码/回调/充值可被刷）

- **证据**：全项目 grep `RateLimiter` 零命中（[Program.cs](../../Program.cs) 无相关注册）
- **影响**：验证码短信轰炸、登录撞库、支付回调/充值接口被高频伪造重放（虽有验签，但会造成无效请求打满）。
- **修复方向**：
```csharp
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("auth", opt => { opt.PermitLimit = 5; opt.Window = TimeSpan.FromMinutes(1); });
    o.AddFixedWindowLimiter("callback", opt => { opt.PermitLimit = 100; opt.Window = TimeSpan.FromMinutes(1); });
});
app.UseRateLimiter();
// [EnableRateLimiting("auth")] 标在 AuthController / MemberAuthController / 回调接口
```

---

### 🟠 P1-07 CORS 完全放开

- **证据**：`AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()`（[Program.cs#L362-L370](../../Program.cs#L362-L370)）
- **影响**：任意站点可跨域读取 API 响应。当前 token 走 `Authorization` 头而非 Cookie，CSRF 风险较低，但一旦 token 通过 URL/日志泄露，任何第三方页面即可直接消费全部接口。
- **修复方向**：按环境白名单化，生产只允许后台域与小程序 H5 域：
```csharp
options.AddPolicy("AllowAll", p => p.WithOrigins(
    builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>())
    .AllowAnyMethod().AllowAnyHeader());
```

---

### 🟠 P1-08 遗留无验签支付回调通道（配置炸弹）

- **证据**：`PaymentCallbackController` 类级 `[AllowAnonymous]`，靠 `WeChat:LegacyCallbackEnabled` 默认 false 关闭（[PaymentCallbackController.cs#L21-L63](../../Controllers/PaymentCallbackController.cs#L21-L63)）
- **影响**：一旦生产环境因排查把开关置 true，任何人 POST 一个 JSON 即可把订单置为已支付、写支付日志。
- **修复方向**：直接删除该控制器与对应 `HandleWechatCallbackAsync`；如必须保留，加 `#if DEBUG` 编译期隔离。

---

### 🟠 P1-09 物流接口 `identify` / `map` 无权限

- **证据**：`Identify`、`GetMap` 无 `[Permission]`，仅 `Track` 有（[LogisticsController.cs#L35-L79](../../Controllers/LogisticsController.cs#L35-L79)）
- **影响**：任意后台账号可消耗快递100 付费配额（成本攻击）。
- **修复方向**：两个接口补 `[Permission(LogisticsPermissions.Query)]`。

---

### 🟠 P1-10 发票第三方平台 `Mock=true`、数据库 root 明文

- **证据**：[appsettings.json#L92-L98](../../appsettings.json#L92-L98)（Invoice Mock=true）、[appsettings.json#L11-L13](../../appsettings.json#L11-L13)
- **影响**：生产沿用 Mock 会「假开票成功」写库但未真开票 → 税务风险；DB 用 root 全权限连接，注入/误操作无隔离。
- **修复方向**：生产强制 `Invoice:Mock=false` 并校验必填项；DB 改为最小权限业务账号（仅 DML，无 DDL）。

---

### 🔴 P1-13 【OpenAPI 实测】安全策略配置匿名可读

- **接口**：`GET /api/system/security/config`（OpenAPI 中 `security` 字段缺失 = 匿名）
- **证据**：同一路径的 `PUT` 有 `security`，`GET` 没有（[SystemSecurityController.cs](../../Controllers/SystemSecurityController.cs)）
- **返回字段（OpenAPI schema 实测）**：`maxLoginAttempts`、`lockDurationMinutes`、`passwordMinLength`、`passwordRequireUpper/Lower/Digit/Special`、`captchaEnabled`、`captchaExpireMinutes`、`captchaLength`、`autoUnlock`、`passwordValidDays`、`tokenExpireHours`、`allowConcurrentLogin`、`maxConcurrentSessions`
- **影响**：这是攻破登录的"说明书"——攻击者据此得知**锁定阈值前可安全尝试几次**、**验证码是否开启**、**密码复杂度下限**、**Token 有效期**。若 `captchaEnabled=false` 且 `maxLoginAttempts` 较大，可直接把撞库速率调到极限；`autoUnlock=true` 时还可配合 `POST /api/system/login-log/unlock/{memberId}` 无限重置锁定。
- **复现思路**：不带任何 Token `GET http://localhost:5266/api/system/security/config` → 200 返回完整策略。
- **修复方向**：改为需登录且 `[Permission("system:security:config")]`；登录页只需要"是否开启验证码 + 验证码长度"两项，可单独开一个最小匿名接口 `GET /api/auth/captcha-policy` 只返回这 2 个字段。
- **代码改动方向**：
```csharp
[HttpGet("config")]
[Authorize]
[Permission("system:security:config")]
public async Task<ApiResponse<SecurityConfigDto>> GetConfig()

// 登录页专用最小披露
[HttpGet("/api/auth/captcha-policy")]
[AllowAnonymous]
public ApiResponse<object> CaptchaPolicy()
    => ApiResponse<object>.Success(new { captchaEnabled = _cfg.CaptchaEnabled, captchaLength = _cfg.CaptchaLength });
```

---

### 🟠 P1-14 【OpenAPI 实测】审计日志导出/清理、账号解锁无权限

- **证据（OpenAPI 实测路径）**：
  - `GET /api/system/login-log/export`、`GET /api/system/operation-log/export` —— 全量导出审计日志（含账号、IP、操作内容）
  - `DELETE /api/system/login-log/batch`、`POST /api/system/login-log/clean`、`POST /api/system/operation-log/clean` —— **删除审计日志**
  - `POST /api/system/login-log/unlock/{memberId}` —— 解锁任意会员账号（绕过锁定策略）
- **影响**：低权限后台账号可**先导出日志、再清空日志**完成反取证；`unlock` 接口可把撞库锁定策略彻底废掉。
- **修复方向**：日志查询/导出挂 `system:log:list` / `system:log:export`；清理挂 `system:log:clean`（仅超管）；`unlock` 挂 `member:unlock` 并写 `[OperationLog]`。
- **代码改动方向**：
```csharp
[HttpDelete("batch")]
[Permission("system:log:delete")]      // 或 [SuperAdminOnly]
[OperationLog("批量删除登录日志")]
public async Task<ApiResponse<bool>> DeleteBatch(string ids)
```

---

### 🟡 P2-11 错误信息泄露

- **证据**：多处把底层异常原文回传前端，如 `$"回调解密失败：{ex.Message}"`（BalanceService）、Service 层 `DbUpdateException` 原文、`InvalidOperationException` 消息直接 `Fail(ex.Message)`
- **影响**：泄露表名/字段/内网地址/驱动版本，辅助攻击者构造 SQL 与越权路径。
- **修复方向**：统一业务异常码；`ex.Message` 只写日志，前端只返回「操作失败，请联系管理员（追踪号 {traceId}）」。

---

### 🟡 P2-12 无安全响应头 / 无 HSTS

- **证据**：grep `X-Frame-Options|Content-Security-Policy|UseHsts|UseHttpsRedirection` 全部零命中
- **影响**：Swagger UI 与后台可被点击劫持；无 HSTS 存在降级到 HTTP 的空间（本地 5266 为 HTTP）。
- **修复方向**：
```csharp
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
    await next();
});
// 生产环境额外 app.UseHsts(); app.UseHttpsRedirection();
```

---

### 🟡 P2-15 【OpenAPI 实测】用 GET 承载"清缓存"等有副作用的操作

- **证据**：`GET /api/dict/cache/clear`、`GET /api/dict/cache/refresh/{dictType}`、`GET /api/dict/cache/reflesh/{dictType}`（注意存在拼写错误 `reflesh` 的重复路由）
- **影响**：
  1. 违反 HTTP 语义——GET 会被浏览器预取、代理缓存、爬虫抓取；一旦被缓存，后续"刷新缓存"根本不生效，排查时极易误判；
  2. 若未来鉴权改为 Cookie 方案，GET 即可被 `<img src="...">` 跨站触发（CSRF）；
  3. 两个拼写不同的重复路由（`refresh` / `reflesh`）形成双份攻击面与维护负债。
- **修复方向**：改为 `POST` 并删除拼写错误路由。
```csharp
[HttpPost("cache/refresh/{dictType}")]
[Permission("system:dict:cache")]
public async Task<ApiResponse<object>> Refresh(string dictType)
```

---

## 2. 必须立即修复的 P0 问题（速查表）

| 编号 | 接口/位置 | 缺失项 | 攻击后果 |
|---|---|---|---|
| P0-01 | `POST/PUT/DELETE /api/prod/sku`、`/api/prod-info`、`/api/banner/admin/*`、`/api/cms/announcement/*` | 会员白名单过宽 + 无 `[Permission]` | **改价后低价下单**、删商品、发钓鱼公告 |
| P0-02 | `/api/sys/role/*`（含提权入口 `PUT /api/sys/role/auth`） | 全部无 `[Permission]` | 自我提权为超管 |
| P0-03 | `PUT/DELETE /api/sys/user/{id}`、`PUT /api/sys/user/{id}/password`、`DELETE /api/sys/user/batch` | 除 `POST /api/sys/user` 外无 `[Permission]` | 重置超管密码接管系统 |
| P0-04 | `appsettings.json` → `Jwt:SecretKey` | 密钥入库 | 离线伪造任意 JWT |

**已具备良好防护、无需整改的资金类接口**（复核结论）：
- 支付回调 `/api/pay/notify`：微信 V3 平台证书 RSA-SHA256 验签 + AES-GCM 解密（[PayController.cs#L34-L49](../../Controllers/PayController.cs#L34-L49)）
- 充值回调 `/api/recharge/wechat-notify`：已验签 + 解密 + 金额校验 + `transaction_id` 幂等
- 退款/退货/核销/开票：已有事务 + 条件更新抢占 + 唯一索引兜底
- 余额扣减：`FOR UPDATE` 行锁 + `ExecuteConditionalUpdate`

---

## 3. 越权风险矩阵

| # | 接口 | 客户端可控 ID 参数 | 归属校验 | 风险判定 |
|---|---|---|---|---|
| 1 | `GET /api/order/page` | `buyerId`、`buyerKeyword` | 后台接口，会员被白名单阻断 | ✅ 无水平越权（垂直依赖 `order:list`） |
| 2 | `GET /api/order/{id}/detail` | `id`(orderId) | Service 校验 `BuyerId == 当前会员` | ✅ 已防护 |
| 3 | `POST /api/order/{id}/cancel` | `id`(orderId) | 同上 | ✅ 已防护 |
| 4 | `GET /api/order/my` | `status` | 强制 `BuyerId = 会员ID` | ✅ 已防护 |
| 5 | `GET/POST /api/address/*` | `id`(addressId)、`memberId` | 强制 `MemberId == 当前会员`（[AddressController.cs#L96-L103](../../Controllers/AddressController.cs#L96-L103)、[L170-L176](../../Controllers/AddressController.cs#L170-L176)） | ✅ 已防护 |
| 6 | `GET /api/balance/logs` | `MemMemberId` | 会员强制覆盖为自身 | ✅ 已修复 |
| 7 | `POST /api/recharge/create` | `MemMemberId` | 会员强制覆盖为自身 | ✅ 已修复 |
| 8 | `POST /api/balance/admin-adjust`、`/freeze`、`/deduct` | `memberId` | `[SuperAdminOnly]` | ✅ 仅超管 |
| 9 | `GET /api/balance/{id}` | `id`(memberId) | Service 校验「只能查自己」（白名单仅放行 `/api/balance/\d+`） | ✅ 已防护 |
| 10 | `POST /api/invoice/info` | `dto.CustomerId` | **未校验调用者与客户归属** | ⚠️ **存在越权**（后台账号可改任意客户开票抬头）→ 需补归属/权限 |
| 11 | `GET /api/invoice/{id}` | `id`(invoiceId) | 仅 `invoice:list` 权限，无归属 | ⚠️ 后台内部越权（同角色可见全部），可接受但建议加数据范围 |
| 12 | `POST /api/payment/{id}/write-off` | `id`(paymentId)、`items[].ReceivableId` | 校验客户一致性 + `FOR UPDATE` | ✅ 已防护 |
| 13 | `PUT /api/refund/{id}/approve`、`POST /{id}/execute` | `id`(refundId) | 权限 + 乐观锁抢占 | ✅ 已防护 |
| 14 | `PUT /api/return/{id}/receive` | `id`(returnId) | 权限 + 条件更新 | ✅ 已防护 |
| 15 | `POST /api/order-price-snapshots/batch` | `orderId`、`price` | `price:snapshot:write`（财务/超管） | ✅ 已防护 |
| 16 | `POST /api/payment-callback/wechat` | `outTradeNo`、`amount` | 无验签，靠配置开关关闭 | ⚠️ 配置一开即可伪造（P1-08） |
| 17 | `GET /api/finance/payment-log/by-order/{orderId}` | `orderId` | **无权限、无归属** | ⚠️ 任意后台账号可读全平台支付流水 |
| 18 | `GET /api/after-sale/log/by-refund/{refundId}`、`/by-return/{returnId}` | `refundId`/`returnId` | **无权限、无归属** | ⚠️ 售后日志泄露 |
| 19 | `PUT/DELETE /api/customer/{id}`、`POST /api/customer/{id}/reset-password` | `id`(memberId) | **无权限** | ⚠️ 会员账号接管 |
| 20 | `PUT /api/sys/user/{id}/password` | `id`(userId) | **无权限** | ⚠️ 超管账号接管（P0-03） |
| 21 | `PUT /api/sys/role/auth` | `roleId`、`menuIds[]` | **无权限** | ⚠️ 自我提权（P0-02） |
| 22 | `PUT /api/prod-info/{id}`、`PUT /api/prod/sku` | `id`、`salePrice` | **会员白名单可直达 + 无权限** | ⚠️ 改价（P0-01） |
| 23 | `POST /api/system/login-log/unlock/{memberId}` | `memberId` | **无权限** | ⚠️ 解锁任意账号，绕过锁定策略（P1-14） |
| 24 | `GET /api/balance/{memberId}`、`GET /api/balance/logs` | `memberId`/`MemMemberId` | 会员强制归属 | ✅ 已防护（OpenAPI 确认参数名为 `MemMemberId`，大小写不敏感） |

---

## 4. Swagger 暴露风险评估

| 检查项 | 现状（含 OpenAPI 实测值） | 结论 |
|---|---|---|
| 是否注册 Swagger | 是（`AddSwaggerGen`，[Program.cs#L375-L396](../../Program.cs#L375-L396)） | — |
| 是否仅在开发环境启用 | 是，`if (app.Environment.IsDevelopment())`（[Program.cs#L459-L466](../../Program.cs#L459-L466)） | ✅ **基本安全** |
| 是否 `RequireAuthorization` | 否 | ⚠️ 依赖 `ASPNETCORE_ENVIRONMENT` 配置正确 |
| 暴露的接口规模 | **246 Path / 282 Operation / 428 Schema**（实测） | ⚠️ 误开即暴露完整攻击面字典 |
| 匿名接口数量 | **14 个**（实测，其中 13 个合理、1 个异常：`GET /api/system/security/config`） | ⚠️ 见 P1-13 |
| 是否暴露敏感示例数据 | **0 处 `example`**；全文件扫不到手机号、邮箱、身份证号、Token 样例 | ✅ 文档侧数据卫生良好 |
| 安全方案声明 | 仅 1 个 `apiKey`（Authorization header），无 OAuth2/OIDC 混淆 | ✅ 清晰 |
| 根级 `security` | **未声明**（逐 operation 声明） | ⚠️ 新增接口漏 `[Authorize]` 即成匿名且文档无警示 |
| 生产误开的后果 | 直接给出全部路由、DTO 结构、参数名与 **19 处敏感 ID 参数位置** | ⚠️ 高风险 |
| swagger-ui 版本与安全头 | 依赖 `Swashbuckle.AspNetCore` 默认版本；无 `X-Frame-Options` / CSP，UI 可被 iframe 嵌套点击劫持 | ⚠️ 中风险 |

**整改建议**：
```csharp
// ① 用显式配置开关控制，不再依赖环境名（防止 prod 误配 Development）
if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Jsd.Api v1"));
}
// ② 非本地环境强制登录 + 超管才能看
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/swagger"),
    branch => branch.UseAuthentication().UseAuthorization());
// ③ 补安全头（见 P2-12），并把 Swashbuckle 升级到当前最新安全版本
```

> 当前实际风险敞口：**本地/开发可接受**；**生产需确保 `ASPNETCORE_ENVIRONMENT=Production`，并建议按上面 ①② 双保险**。

---

## 5. 接口加固建议（按优先级）

### 5.1 机制层（一次改动解决一批问题，优先做）

1. **`PermissionFilter` 改为默认拒绝**
   现状：未标 `[Permission]` = 放行。改为：后台控制器（`/api/*` 且非会员白名单）未标 `[Permission]` → 403，用新特性 `[PublicEndpoint]` 显式放行登录、验证码、商品浏览、支付回调。
   收益：新增接口漏配权限时「默认安全」，而非「默认裸奔」。

2. **会员白名单改为精确路径白名单**
   禁止使用控制器级前缀正则；只登记「方法 + 精确路径」。并在单元测试中断言：白名单每条路径对应的 Action 必须是 `HttpGet`。

3. **高危接口统一 `[Authorize] + [Permission] + [OperationLog]` 三件套**
   资金/权限/配置类写操作必须落审计（现有 `OperationLogFilter` 已就绪）。

### 5.2 代码层

```csharp
// ✅ 资源归属一律从 Claims 取，禁止信任前端 ID
var memberId = _currentUser.MemberId;           // JWT member_id
if (memberId <= 0) return Fail("未登录", 401);
var entity = await _db.Xxx.FirstOrDefaultAsync(x => x.Id == id && x.MemberId == memberId);
if (entity == null) return Fail("资源不存在或无权访问", 404);

// ✅ 金额一律 decimal + 精度显式声明
[Column("amount", TypeName = "decimal(12,2)")] public decimal Amount { get; set; }

// ✅ 余额扣减原子化（已在用，保持）
await _db.Members.Where(m => m.Id == id && m.Balance >= amt)
    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Balance, p => p.Balance - amt));

// ✅ 第三方回调必须验签 + 金额校验 + 幂等（已在用，保持）
if (!WeChatPayCrypto.VerifySignature(ts, nonce, body, sig, platformKey)) return Fail("验签失败");
var dup = await _db.PaymentLogs.AnyAsync(l => l.TransactionId == txnId);
if (dup) return Success("已处理");

// ✅ 导出/全量查询必须权限 + 分页 + 归属 + 审计
[Permission("finance:balance:log")]
[OperationLog("导出余额流水")]
[HttpGet("logs")]
public async Task<...> GetLogs([FromQuery] BalanceLogQueryDto q)
{
    if (_currentUser.IsMember) q.MemMemberId = _currentUser.MemberId;
    if (q.PageSize > 100) q.PageSize = 100;          // 硬上限
    if (q.PageSize <= 0 && !q.MemMemberId.HasValue)  // 禁止无条件全表
        return Fail("请指定会员或分页参数");
}
```

### 5.3 配置层

- `Jwt:SecretKey` → 环境变量，仓库留空；**已泄露则立即轮换**
- `ConnectionStrings` → 最小权限业务账号
- `Invoice:Mock` → 生产 false，启动校验
- `Cors:Origins` → 白名单
- 新增 `Swagger:Enabled` 配置项

---

## 6. 人工验证清单（本地 5266 / Postman）

### 6.1 认证绕过
| # | 步骤 | 期望结果 |
|---|---|---|
| 1 | 不带 Token 调 `GET /api/order/page` | 401 |
| 2 | 不带 Token 调 `GET /api/system/config/list` | 401 |
| 3 | 用过期 Token 调任意接口 | 401 |
| 3b | 不带 Token 调 `GET /api/system/security/config` | **修复前 200（漏洞 P1-13）**；修复后 401 |
| 3c | 对照 `GET /api/pay/notify` 与 `POST /api/payment-callback/wechat` | 均匿名，但前者验签、后者靠开关关闭 |

### 6.2 垂直越权（普通后台账号 → 管理功能）
| # | 步骤 | 期望结果 |
|---|---|---|
| 4 | 仓库账号 Token → `PUT /api/sys/role/auth`，给自己角色勾全部菜单 | **修复前 200（漏洞）**；修复后 403 |
| 5 | 仓库账号 Token → `PUT /api/sys/user/1/password` | **修复前 200（漏洞）**；修复后 403 |
| 6 | 仓库账号 Token → `POST /api/task/job`（创建定时任务） | 修复前 200；修复后 403 |
| 7 | 仓库账号 Token → `PUT /api/system/config` | 修复前 200；修复后 403 |
| 7b | 仓库账号 Token → `GET /api/system/operation-log/export` | 修复前 200（全量审计日志外泄）；修复后 403 |
| 7c | 仓库账号 Token → `POST /api/system/operation-log/clean` | 修复前 200（**可删除审计日志**）；修复后 403 |
| 7d | 仓库账号 Token → `POST /api/system/login-log/unlock/{任意memberId}` | 修复前 200（绕过锁定）；修复后 403 |
| 8 | 上述操作后查 `sys_operation_log` | 应有完整操作人/时间/IP 审计 |

### 6.3 会员越权改价（本次最高危）
| # | 步骤 | 期望结果 |
|---|---|---|
| 9 | 会员 Token → `POST /api/prod/sku`（新建 SKU，价格 0.01） | **修复前 200（漏洞）**；修复后 403 |
| 10 | 会员 Token → `PUT /api/prod/sku` body `{id:1, salePrice:0.01}` | 修复前 200；修复后 403 |
| 11 | 会员 Token → `DELETE /api/prod-info/1` | 修复前 200；修复后 403 |
| 12 | 会员 Token → `POST /api/banner/admin/add` | 修复前 200；修复后 403 |
| 13 | 会员 Token → `POST /api/cms/announcement/add` | 修复前 200；修复后 403 |
| 14 | 会员 Token → `GET /api/prod-info/page` | 200（正常浏览必须保留） |

### 6.4 水平越权
| # | 步骤 | 期望结果 |
|---|---|---|
| 15 | 会员 A Token → `GET /api/order/{B的订单ID}/detail` | 404/403，不返回 B 的数据 |
| 16 | 会员 A Token → `POST /api/address/save` body `{id: B的地址ID, ...}` | 400「地址不存在或不属于当前会员」 |
| 17 | 会员 A Token → `POST /api/address/delete` body `{id: B的地址ID}` | 404 |
| 18 | 会员 A Token → `GET /api/balance/logs?memMemberId=B` | 仅返回 A 的流水 |
| 19 | 会员 A Token → `GET /api/balance/{B的会员ID}` | 403/失败「只能查看本人余额」 |

### 6.5 支付回调与幂等
| # | 步骤 | 期望结果 |
|---|---|---|
| 20 | 无 `Wechatpay-Signature` 头 → `POST /api/recharge/wechat-notify` | 失败「验签失败」，余额不变 |
| 21 | 伪造签名 → 同上 | 失败，余额不变 |
| 22 | 同一 `transaction_id` 合法回调连发 2 次 | 第一次入账，第二次返回成功但余额不再增加 |
| 23 | `POST /api/payment-callback/wechat`（默认配置） | 403「该回调通道已停用」 |
| 24 | 核销同一收款单并发 2 次（Postman Runner） | 仅 1 次成功，`mkt_payment_item` 无重复行 |

### 6.6 Swagger 与信息泄露
| # | 步骤 | 期望结果 |
|---|---|---|
| 25 | 生产环境变量下访问 `/swagger` | 404 |
| 26 | 访问 `/swagger/v1/swagger.json` 检查是否含手机号/身份证/Token 示例 | 无此类示例数据 |
| 27 | 触发一个 DB 异常（如超长字符串写入） | 返回统一业务提示，不返回表名/SQL |
| 28 | 检查响应头 | 含 `X-Frame-Options`、`X-Content-Type-Options` |
| 29 | 1 分钟内连发 20 次登录请求 | 第 6 次起 429（需先实施 P1-06） |

---

## 附：修复优先级建议

| 批次 | 内容 | 说明 |
|---|---|---|
| 第一批（当天） | P0-01 会员白名单收敛 + 商品/SKU/横幅/公告写接口补权限 | 唯一「可被普通会员直接变现」的漏洞 |
| 第二批（当天） | P0-02 角色、P0-03 用户管理补权限 | 后台内部提权链路 |
| 第三批（当天） | P0-04 JWT 密钥移出仓库并轮换 | 一次性操作，影响面最大 |
| 第四批（本周） | `PermissionFilter` 默认拒绝改造 + 批量补权限 | 机制性解决，避免新增接口再漏 |
| 第五批（本周） | 限流、CORS 白名单、安全头、Swagger 双保险 | 纵深防御 |
| 第六批（本周） | P1-13 `security/config` 收敛为最小披露；P1-14 日志导出/清理/解锁补权限 | 由 OpenAPI 实测新增 |
| 第七批（本周） | 配置治理（Invoice Mock、DB 账号）、错误信息统一 | 合规与运维安全 |
| 长期（CI） | 在流水线断言：除 14 条白名单外，`swagger.json` 中不得出现 `security` 缺失的 operation；且会员白名单每条必须对应 `GET` | 机制化防回归 |

---

## 附二：本轮已落地修复清单（代码级）

> 编译验证：`dotnet build` → 0 警告 0 错误；数据库权限种子已执行。

| # | 文件 | 改动 |
|---|---|---|
| 1 | `Attributes/PermissionFilter.cs` | ① 会员令牌对 `prod-info / prod/sku / prod/category / banner / cms / balance/{id}` 仅允许 GET/HEAD/OPTIONS，写方法一律 403（修 P0-01 改价）；② 未标 `[Permission]` 的**写操作默认拒绝**（403）；③ 新增 `LegacyUnpermissionedControllers` 迁移豁免名单（放行但打告警日志）；④ 支持 `[PermissionFree]` 显式放行 |
| 2 | `Attributes/PermissionFreeAttribute.cs` | 新增。放行必须写明理由，让"不做权限校验"成为可见决定 |
| 3 | `Program.cs` | `AddAuthorization` 增加 `FallbackPolicy = RequireAuthenticatedUser()`：今后新增接口漏写 `[Authorize]` 也不会变成匿名 |
| 4 | `Controllers/SystemSecurityController.cs` | `GET /config` 移除匿名，改 `[Permission("system:security:list")]`；`PUT /config` 加 `[Permission("system:security:update")]`；新增匿名最小披露 `GET /captcha-policy`（只返回 captchaEnabled/captchaLength） |
| 5 | `Controllers/SystemOperationLogController.cs` | list/detail/statistics → `system:operation:list`；export → `:export`；delete/batch → `:remove`；clean → `:clean` |
| 6 | `Controllers/SystemLoginLogController.cs` | list/detail/statistics → `system:loginlog:list`；export/remove/clean 分别补齐；`lock/list` → `system:lock:list`；`unlock` → `system:lock:unlock` |
| 7 | `Controllers/DictCacheController.cs` | `clear`、`refresh` 由 **GET 改 POST**，补 `dict:cache:clear` / `dict:cache:refresh`，删除拼写错误的 `reflesh` 重复路由 |
| 8 | `Controllers/LogisticsController.cs` | `identify`、`track/map` 补 `order:logistics:query`（防白嫖快递100 配额） |
| 9 | `Models/System/SystemPermissions.cs` | 新增权限常量，**全部对齐 sys_menu 现网取值**（避免与库中码不一致导致非超管全 403） |
| 10 | `Helpers/ImageSaver.cs` | 在扩展名白名单（jpg/jpeg/png/gif/webp/bmp）+ 10MB 上限之外，新增**文件头魔数校验**，拒绝改后缀的脚本/SVG |
| 11 | `sql/scripts/system_security_permission_sync.sql` | 新增 `system:security:update`、`system:operation:remove`、`system:loginlog:remove`、`dict:cache:refresh` 四个按钮权限（幂等） |

### 需要前端配合的改动（否则页面报错）

1. **登录安全策略页**（`Jsd.Web/src/views/system/login-security/index.vue`）：`GET /api/system/security/config` 现在需要 `system:security:list` 权限，非超管会 403。这是预期行为（策略详情只给超管看）。
2. **登录页**：若要根据后端开关决定是否显示验证码，改调 `GET /api/system/security/captcha-policy`（匿名、只返回 2 个字段）。
3. **字典缓存按钮**：`GET /api/dict/cache/clear`、`GET /api/dict/cache/refresh/{type}` 已改为 **POST**，前端请求方法需同步修改；`reflesh` 路由已删除。

### 仍待迁移（已列入告警名单，调用时会打 `安全告警` 日志）

`SysRole`、`SysUser`（Create 已补，其余待补）、`SysConfig`、`TaskJob`、`Customer`、`ProdInfo`、`ProdSku`、`ProdSpecItem`、`ProdSpecValue`、`SupSupplier`、`Banner`、`CmsAnnouncement`、`DictType`、`DictData`、`File`、`AdminUpload`、`PaymentLog`、`AfterSaleLog`。

> 其中 **SysRole / SysUser / Customer** 属 P0/P1，建议下一批立即补——它们分别对应「自我提权」「重置超管密码」「会员账号接管」。补完后把对应控制器从 `LegacyUnpermissionedControllers` 删除即可，无需改机制。

### 回滚开关

若某次发版因权限漏配导致业务受阻：

```json
// appsettings.json
"Security": { "DenyUnpermissionedWrites": false }
```

改配置即可让「未标权限的写接口」恢复放行（FallbackPolicy 与已挂 `[Permission]` 的接口不受影响），无需回滚代码。

