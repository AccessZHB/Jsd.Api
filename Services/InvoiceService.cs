using System.Text;
using System.Text.Json;
using AutoMapper;
using FluentValidation;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Invoice;
using Jsd.Api.Models.Payment;
using Jsd.Api.Repositories;
using Jsd.Api.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jsd.Api.Services;

/// <summary>
/// 发票与税务管理服务实现（发票与税务管理模块）
///
/// ┌── 为什么发票要独立成模块 ─────────────────────────────────────────────┐
/// │ 发票是税务凭证：抬头/税号/金额一旦开错就要红冲重开，且已开票记录必须与当时的   │
/// │ 申请一致。所以本模块刻意做了两件事：                                      │
/// │   1) 开票时把抬头 + 税号 + 开户行/账号/地址/电话【整体快照】到 order_invoice， │
/// │      客户事后改 customer_invoice_info 不影响历史发票；                    │
/// │   2) 金额以【服务端订单实付】为上限，绝不信任前端传值（防超额开票）。         │
/// └─────────────────────────────────────────────────────────────────────┘
///
/// 【状态机 order_invoice.status】
///   0-申请中 ──财务开票/平台开票──&gt; 1-已开票 ──红冲──&gt; 2-已作废
///   0-申请中 ──撤销申请─────────────────────────&gt; 2-已作废
///
/// 【幂等】
///   · 申请：同一订单存在「申请中/已开票」记录时拒绝重复申请（需先作废）；
///   · 作废：已是「已作废」直接返回成功，不重复写库；
///   · 回写：发票号码唯一索引 + 预查重，双重防重复录入。
///
/// 【异常纪律】业务不合法抛 InvalidOperationException（Controller 转成 Fail）；
///            第三方开票平台调用失败不阻断申请，只降级为「人工开票」。
/// </summary>
public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _db;
    private readonly IRepository<OrderInvoice> _invoiceRepo;
    private readonly IRepository<CustomerInvoiceInfo> _customerInvoiceRepo;
    private readonly IRepository<TrxOrder> _orderRepo;
    private readonly IRepository<PaymentOrder> _paymentRepo;
    private readonly IRepository<MemMember> _memberRepo;
    private readonly IMapper _mapper;
    private readonly CurrentUserService _currentUser;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InvoiceService> _logger;

    private readonly SaveCustomerInvoiceValidator _saveInfoValidator;
    private readonly ApplyInvoiceValidator _applyValidator;
    private readonly CancelInvoiceValidator _cancelValidator;
    private readonly IssueInvoiceValidator _issueValidator;

    public InvoiceService(
        AppDbContext db,
        IRepository<OrderInvoice> invoiceRepo,
        IRepository<CustomerInvoiceInfo> customerInvoiceRepo,
        IRepository<TrxOrder> orderRepo,
        IRepository<PaymentOrder> paymentRepo,
        IRepository<MemMember> memberRepo,
        IMapper mapper,
        CurrentUserService currentUser,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<InvoiceService> logger,
        SaveCustomerInvoiceValidator saveInfoValidator,
        ApplyInvoiceValidator applyValidator,
        CancelInvoiceValidator cancelValidator,
        IssueInvoiceValidator issueValidator)
    {
        _db = db;
        _invoiceRepo = invoiceRepo;
        _customerInvoiceRepo = customerInvoiceRepo;
        _orderRepo = orderRepo;
        _paymentRepo = paymentRepo;
        _memberRepo = memberRepo;
        _mapper = mapper;
        _currentUser = currentUser;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _saveInfoValidator = saveInfoValidator;
        _applyValidator = applyValidator;
        _cancelValidator = cancelValidator;
        _issueValidator = issueValidator;
    }

    // ============================================================
    // 一、客户开票信息维护
    // ============================================================

    public async Task<ApiResponse<long>> SaveCustomerInvoiceAsync(SaveCustomerInvoiceDto dto)
    {
        _saveInfoValidator.ValidateAndThrow(dto);

        // 客户必须存在（客户 = mem_member）
        var member = await _memberRepo.FirstOrDefaultAsync(m => m.Id == dto.CustomerId);
        if (member == null)
        {
            return ApiResponse<long>.Fail("客户不存在");
        }

        var now = DateTime.Now;
        var operatorName = _currentUser.UserName;

        if (dto.Id > 0)
        {
            var exist = await _customerInvoiceRepo.FirstOrDefaultAsync(c => c.Id == dto.Id);
            if (exist == null)
            {
                return ApiResponse<long>.Fail("开票信息不存在", 404);
            }

            exist.CustomerId = dto.CustomerId;
            exist.TitleType = dto.TitleType;
            exist.TitleName = dto.TitleName.Trim();
            exist.TaxNo = Normalize(dto.TaxNo);
            exist.BankName = Normalize(dto.BankName);
            exist.BankAccount = Normalize(dto.BankAccount);
            exist.Address = Normalize(dto.Address);
            exist.Phone = Normalize(dto.Phone);
            exist.IsDefault = dto.IsDefault;

            if (dto.IsDefault == 1)
            {
                await ClearOtherDefaultAsync(dto.CustomerId, exist.Id);
            }

            _customerInvoiceRepo.Update(exist);
            await _db.SaveChangesAsync();
            return ApiResponse<long>.Success(exist.Id, "开票信息已更新");
        }

        var entity = new CustomerInvoiceInfo
        {
            CustomerId = dto.CustomerId,
            TitleType = dto.TitleType,
            TitleName = dto.TitleName.Trim(),
            TaxNo = Normalize(dto.TaxNo),
            BankName = Normalize(dto.BankName),
            BankAccount = Normalize(dto.BankAccount),
            Address = Normalize(dto.Address),
            Phone = Normalize(dto.Phone),
            IsDefault = dto.IsDefault,
            Status = 0,
            CreateBy = operatorName,
            CreateTime = now
        };

        await _customerInvoiceRepo.AddAsync(entity);
        await _db.SaveChangesAsync();

        if (entity.IsDefault == 1)
        {
            await ClearOtherDefaultAsync(entity.CustomerId, entity.Id);
            await _db.SaveChangesAsync();
        }

        return ApiResponse<long>.Success(entity.Id, "开票信息已保存");
    }

    public async Task<ApiResponse<List<CustomerInvoiceInfoDto>>> GetCustomerInvoiceListAsync(long customerId)
    {
        if (customerId <= 0)
        {
            return ApiResponse<List<CustomerInvoiceInfoDto>>.Fail("客户ID不合法");
        }

        var list = await _customerInvoiceRepo.Query()
            .Where(c => c.CustomerId == customerId && c.Status == 0)
            .OrderByDescending(c => c.IsDefault)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

        var dtos = list.Select(ToDto).ToList();
        return ApiResponse<List<CustomerInvoiceInfoDto>>.Success(dtos);
    }

    public async Task<ApiResponse<CustomerInvoiceInfoDto>> GetCustomerInvoiceAsync(long id)
    {
        var entity = await _customerInvoiceRepo.FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null)
        {
            return ApiResponse<CustomerInvoiceInfoDto>.Fail("开票信息不存在", 404);
        }

        return ApiResponse<CustomerInvoiceInfoDto>.Success(ToDto(entity));
    }

    // ============================================================
    // 二、开票申请（ApplyInvoice）
    // ============================================================

    public async Task<ApiResponse<ApplyInvoiceResult>> ApplyInvoiceAsync(ApplyInvoiceDto dto)
    {
        _applyValidator.ValidateAndThrow(dto);

        // ---- 1. 订单校验：必须存在、未删除，且已支付（待付款/已关闭不允许开票）----
        var order = await _orderRepo.FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.IsDeleted == 0);
        if (order == null)
        {
            return ApiResponse<ApplyInvoiceResult>.Fail("订单不存在");
        }

        if (order.OrderStatus == 0)
        {
            return ApiResponse<ApplyInvoiceResult>.Fail("订单尚未支付，支付完成后才能申请开票");
        }

        if (order.OrderStatus == 4)
        {
            return ApiResponse<ApplyInvoiceResult>.Fail("订单已关闭，不能申请开票");
        }

        // ---- 2. 查重：同一订单已有「申请中 / 已开票」记录 → 拒绝（先作废才能重开）----
        var exist = await _invoiceRepo.FirstOrDefaultAsync(i => i.OrderId == dto.OrderId && i.Status != (int)InvoiceStatus.Canceled);
        if (exist != null)
        {
            return ApiResponse<ApplyInvoiceResult>.Fail(
                $"该订单已有开票记录（{InvoiceStatusNames.GetName(exist.Status)}），如需重新开票请先作废原记录");
        }

        // ---- 3. 支付单校验（可空；传了就要属于该订单且已支付成功）----
        if (dto.PaymentId.HasValue && dto.PaymentId.Value > 0)
        {
            var payment = await _paymentRepo.FirstOrDefaultAsync(p => p.Id == dto.PaymentId.Value);
            if (payment == null)
            {
                return ApiResponse<ApplyInvoiceResult>.Fail("支付单不存在");
            }

            if (payment.BizOrderId != dto.OrderId)
            {
                return ApiResponse<ApplyInvoiceResult>.Fail("支付单与订单不匹配");
            }

            if (payment.Status != (int)PayOrderStatus.Success)
            {
                return ApiResponse<ApplyInvoiceResult>.Fail("支付单未支付成功，不能开票");
            }
        }

        // ---- 4. 金额：以订单实付金额为上限（服务端权威，不信任前端）----
        //      不传金额时按订单实付自动填充；税额不传时按 13% 从价税合计反算。
        var payAmount = order.PayAmount;
        var totalAmount = dto.TotalAmount > 0 ? dto.TotalAmount : payAmount;
        if (totalAmount <= 0)
        {
            return ApiResponse<ApplyInvoiceResult>.Fail("开票金额必须大于 0");
        }

        if (totalAmount > payAmount)
        {
            return ApiResponse<ApplyInvoiceResult>.Fail($"开票金额不能超过订单实付金额（¥{payAmount:0.00}）");
        }

        var taxAmount = dto.TaxAmount ?? TaxNoRule.CalcTaxAmount(totalAmount, TaxNoRule.DefaultTaxRate);
        if (taxAmount < 0 || taxAmount >= totalAmount)
        {
            return ApiResponse<ApplyInvoiceResult>.Fail("税额不合法（须 ≥ 0 且小于发票总金额）");
        }

        // ---- 5. 落库：状态 = 申请中，抬头信息整体快照 ----
        var invoice = new OrderInvoice
        {
            OrderId = dto.OrderId,
            PaymentId = dto.PaymentId.HasValue && dto.PaymentId.Value > 0 ? dto.PaymentId.Value : null,
            InvoiceType = dto.InvoiceType,
            TitleType = dto.TitleType,
            TitleName = dto.TitleName.Trim(),
            TaxNo = Normalize(dto.TaxNo),
            BankName = Normalize(dto.BankName),
            BankAccount = Normalize(dto.BankAccount),
            Address = Normalize(dto.Address),
            Phone = Normalize(dto.Phone),
            TotalAmount = decimal.Round(totalAmount, 2),
            TaxAmount = decimal.Round(taxAmount, 2),
            Status = (int)InvoiceStatus.Applying,
            ApplicantId = _currentUser.UserId,
            ApplicantName = _currentUser.UserName,
            Remark = Normalize(dto.Remark),
            CreateTime = DateTime.Now
        };

        await _invoiceRepo.AddAsync(invoice);
        await _db.SaveChangesAsync();

        // ---- 6. 第三方开票平台：对接了就直接开票，未对接则降级为人工开票 ----
        var platform = await TryIssueByPlatformAsync(invoice);
        await _db.SaveChangesAsync();

        var result = new ApplyInvoiceResult
        {
            InvoiceId = invoice.Id,
            Status = invoice.Status,
            StatusName = InvoiceStatusNames.GetName(invoice.Status),
            TotalAmount = invoice.TotalAmount,
            TaxAmount = invoice.TaxAmount,
            NeedManualIssue = !platform.Success,
            Message = platform.Success
                ? "开票申请已提交，开票平台已受理并开票成功"
                : "开票申请已提交，等待财务人工开票（开票后回写发票代码/号码）"
        };

        return ApiResponse<ApplyInvoiceResult>.Success(result, result.Message);
    }

    // ============================================================
    // 三、作废 / 红冲（CancelInvoice）
    // ============================================================

    public async Task<ApiResponse<CancelInvoiceResult>> CancelInvoiceAsync(long id, CancelInvoiceDto dto)
    {
        _cancelValidator.ValidateAndThrow(dto);

        var invoice = await _invoiceRepo.FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null)
        {
            return ApiResponse<CancelInvoiceResult>.Fail("开票记录不存在", 404);
        }

        // 幂等：已作废直接返回成功，不重复写库
        if (invoice.Status == (int)InvoiceStatus.Canceled)
        {
            return ApiResponse<CancelInvoiceResult>.Success(new CancelInvoiceResult
            {
                InvoiceId = invoice.Id,
                Status = invoice.Status,
                StatusName = InvoiceStatusNames.GetName(invoice.Status),
                OldStatus = invoice.Status,
                IsRed = false,
                RedReason = invoice.RedReason ?? string.Empty,
                BlueInvoiceNo = invoice.BlueInvoiceNo,
                Message = "该发票此前已作废，本次未重复处理"
            });
        }

        // 已开票 → 红冲（必须填关联蓝票号码）；申请中 → 撤销申请
        var isRed = invoice.Status == (int)InvoiceStatus.Issued;
        if (isRed && string.IsNullOrWhiteSpace(dto.BlueInvoiceNo))
        {
            throw new InvalidOperationException("红冲已开具的发票必须填写【关联蓝票发票号码】");
        }

        invoice.Status = (int)InvoiceStatus.Canceled;
        invoice.RedReason = dto.RedReason.Trim();
        invoice.BlueInvoiceNo = isRed ? (dto.BlueInvoiceNo ?? string.Empty).Trim() : null;
        invoice.OperatorId = _currentUser.UserId;
        invoice.OperatorName = _currentUser.UserName;

        _invoiceRepo.Update(invoice);
        await _db.SaveChangesAsync();

        var message = isRed ? "发票已红冲" : "开票申请已撤销";
        return ApiResponse<CancelInvoiceResult>.Success(new CancelInvoiceResult
        {
            InvoiceId = invoice.Id,
            Status = invoice.Status,
            StatusName = InvoiceStatusNames.GetName(invoice.Status),
            OldStatus = isRed ? (int)InvoiceStatus.Issued : (int)InvoiceStatus.Applying,
            IsRed = isRed,
            RedReason = invoice.RedReason,
            BlueInvoiceNo = invoice.BlueInvoiceNo,
            Message = message
        }, message);
    }

    // ============================================================
    // 四、财务回写票面信息（IssueInvoice）
    // ============================================================

    public async Task<ApiResponse<bool>> IssueInvoiceAsync(long id, IssueInvoiceDto dto)
    {
        _issueValidator.ValidateAndThrow(dto);

        var invoice = await _invoiceRepo.FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null)
        {
            return ApiResponse<bool>.Fail("开票记录不存在", 404);
        }

        if (invoice.Status == (int)InvoiceStatus.Issued)
        {
            return ApiResponse<bool>.Fail("该发票已开票，请勿重复回写");
        }

        if (invoice.Status == (int)InvoiceStatus.Canceled)
        {
            return ApiResponse<bool>.Fail("该发票已作废，不能开票");
        }

        var invoiceNo = dto.InvoiceNo.Trim();

        // 发票号码唯一索引兜底：先查一次，给出友好提示而不是抛数据库异常
        var duplicated = await _invoiceRepo.AnyAsync(i => i.InvoiceNo == invoiceNo && i.Id != id);
        if (duplicated)
        {
            return ApiResponse<bool>.Fail($"发票号码 {invoiceNo} 已存在，不能重复录入");
        }

        invoice.InvoiceCode = string.IsNullOrWhiteSpace(dto.InvoiceCode) ? null : dto.InvoiceCode.Trim();
        invoice.InvoiceNo = invoiceNo;
        invoice.InvoiceDate = (dto.InvoiceDate ?? DateTime.Today).Date;
        invoice.Status = (int)InvoiceStatus.Issued;
        invoice.OperatorId = _currentUser.UserId;
        invoice.OperatorName = _currentUser.UserName;
        if (!string.IsNullOrWhiteSpace(dto.Remark))
        {
            invoice.Remark = dto.Remark.Trim();
        }

        _invoiceRepo.Update(invoice);
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "开票信息已回写，状态更新为已开票");
    }

    // ============================================================
    // 五、查询：分页列表 / 详情
    // ============================================================

    public async Task<ApiResponse<PagedResult<InvoiceListDto>>> GetPagedListAsync(InvoiceQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

        // 开票记录 left join 订单：列表要展示订单号，按下单买家过滤也要用到订单表
        var source = from inv in _db.OrderInvoices
                     join ord in _db.TrxOrders on inv.OrderId equals ord.Id into gj
                     from ord in gj.DefaultIfEmpty()
                     select new { inv, ord };

        if (query.OrderId.HasValue && query.OrderId.Value > 0)
        {
            source = source.Where(x => x.inv.OrderId == query.OrderId.Value);
        }

        if (query.BuyerId.HasValue && query.BuyerId.Value > 0)
        {
            source = source.Where(x => x.ord != null && x.ord.BuyerId == query.BuyerId.Value);
        }

        if (query.Status.HasValue)
        {
            source = source.Where(x => x.inv.Status == query.Status.Value);
        }

        if (query.InvoiceType.HasValue)
        {
            source = source.Where(x => x.inv.InvoiceType == query.InvoiceType.Value);
        }

        if (query.StartTime.HasValue)
        {
            source = source.Where(x => x.inv.CreateTime >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            source = source.Where(x => x.inv.CreateTime < query.EndTime.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            source = source.Where(x =>
                (x.inv.InvoiceNo != null && x.inv.InvoiceNo.Contains(kw))
                || x.inv.TitleName.Contains(kw)
                || (x.ord != null && x.ord.OrderNo.Contains(kw)));
        }

        var total = await source.CountAsync();

        var rows = await source
            .OrderByDescending(x => x.inv.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InvoiceListDto
            {
                Id = x.inv.Id,
                OrderId = x.inv.OrderId,
                OrderNo = x.ord == null ? string.Empty : x.ord.OrderNo,
                PaymentId = x.inv.PaymentId,
                InvoiceType = x.inv.InvoiceType,
                TitleType = x.inv.TitleType,
                TitleName = x.inv.TitleName,
                TaxNo = x.inv.TaxNo,
                InvoiceCode = x.inv.InvoiceCode,
                InvoiceNo = x.inv.InvoiceNo,
                InvoiceDate = x.inv.InvoiceDate,
                TotalAmount = x.inv.TotalAmount,
                TaxAmount = x.inv.TaxAmount,
                Status = x.inv.Status,
                ApplicantName = x.inv.ApplicantName,
                CreateTime = x.inv.CreateTime
            })
            .ToListAsync();

        // 中文名在内存补：EF 无法把 C# switch 表达式翻译成 SQL
        foreach (var row in rows)
        {
            row.InvoiceTypeName = InvoiceTypeNames.GetName(row.InvoiceType);
            row.StatusName = InvoiceStatusNames.GetName(row.Status);
            row.TitleTypeName = InvoiceTitleTypeNames.GetName(row.TitleType);
        }

        return ApiResponse<PagedResult<InvoiceListDto>>.Success(
            PagedResult<InvoiceListDto>.Create(rows, total, page, pageSize));
    }

    public async Task<ApiResponse<InvoiceDetailDto>> GetDetailAsync(long id)
    {
        var row = await (from inv in _db.OrderInvoices
                         join ord in _db.TrxOrders on inv.OrderId equals ord.Id into gj
                         from ord in gj.DefaultIfEmpty()
                         where inv.Id == id
                         select new { inv, ord })
            .FirstOrDefaultAsync();

        if (row == null)
        {
            return ApiResponse<InvoiceDetailDto>.Fail("开票记录不存在", 404);
        }

        var dto = _mapper.Map<InvoiceDetailDto>(row.inv);
        dto.OrderNo = row.ord == null ? string.Empty : row.ord.OrderNo;
        dto.OrderPayAmount = row.ord == null ? 0m : row.ord.PayAmount;
        dto.InvoiceTypeName = InvoiceTypeNames.GetName(dto.InvoiceType);
        dto.TitleTypeName = InvoiceTitleTypeNames.GetName(dto.TitleType);
        dto.StatusName = InvoiceStatusNames.GetName(dto.Status);

        return ApiResponse<InvoiceDetailDto>.Success(dto);
    }

    // ============================================================
    // 内部：第三方开票平台（百望云 / 航信 等）
    // ============================================================

    /// <summary>
    /// 尝试调用第三方开票平台开票。
    ///
    /// 配置（appsettings.json 的 Invoice 节）：
    ///   Provider  —— 平台标识，留空表示未对接；
    ///   ApiUrl    —— 开票接口地址；
    ///   AppKey / AppSecret —— 平台鉴权；
    ///   Mock      —— true 时强制走人工开票分支（联调用）。
    ///
    /// ⚠️ 平台返回结构各家不同，这里做【宽容解析】：只要能取到发票号码就视为开票成功；
    ///    任何异常都只降级为「人工开票」，绝不阻断开票申请本身（申请记录已落库）。
    /// </summary>
    private async Task<(bool Success, string Message)> TryIssueByPlatformAsync(OrderInvoice invoice)
    {
        var provider = _configuration["Invoice:Provider"];
        var apiUrl = _configuration["Invoice:ApiUrl"];
        var mock = string.Equals(_configuration["Invoice:Mock"], "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(apiUrl) || mock)
        {
            AppendRemark(invoice, "未对接第三方开票平台，转人工开票");
            return (false, "未对接第三方开票平台");
        }

        try
        {
            var payload = new
            {
                provider,
                orderId = invoice.OrderId,
                invoiceType = invoice.InvoiceType,
                titleType = invoice.TitleType,
                titleName = invoice.TitleName,
                taxNo = invoice.TaxNo,
                bankName = invoice.BankName,
                bankAccount = invoice.BankAccount,
                address = invoice.Address,
                phone = invoice.Phone,
                totalAmount = invoice.TotalAmount,
                taxAmount = invoice.TaxAmount
            };

            using var client = _httpClientFactory.CreateClient("invoice");
            using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            req.Headers.Add("X-App-Key", _configuration["Invoice:AppKey"] ?? string.Empty);
            req.Headers.Add("X-App-Secret", _configuration["Invoice:AppSecret"] ?? string.Empty);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("开票平台调用失败 HTTP {Status}：{Body}", (int)resp.StatusCode, body);
                AppendRemark(invoice, $"开票平台调用失败（HTTP {(int)resp.StatusCode}），转人工开票");
                return (false, "开票平台调用失败");
            }

            // 宽容解析：平台字段名可能是 invoiceNo / invoice_no / number
            var (ok, invoiceNo, invoiceCode) = ParsePlatformResponse(body);
            if (!ok || string.IsNullOrWhiteSpace(invoiceNo))
            {
                _logger.LogWarning("开票平台未返回发票号码：{Body}", body);
                AppendRemark(invoice, "开票平台未返回发票号码，转人工开票");
                return (false, "开票平台未返回发票号码");
            }

            invoice.InvoiceNo = invoiceNo;
            invoice.InvoiceCode = string.IsNullOrWhiteSpace(invoiceCode) ? null : invoiceCode;
            invoice.InvoiceDate = DateTime.Today;
            invoice.Status = (int)InvoiceStatus.Issued;
            invoice.OperatorName = string.IsNullOrWhiteSpace(invoice.OperatorName) ? "开票平台" : invoice.OperatorName;
            AppendRemark(invoice, $"由开票平台 {provider} 自动开票成功");

            return (true, "开票平台开票成功");
        }
        catch (Exception ex)
        {
            // 网络/解析异常：只降级，不抛
            _logger.LogWarning(ex, "调用开票平台异常，order_invoice.id={InvoiceId}", invoice.Id);
            AppendRemark(invoice, $"开票平台异常：{ex.Message}（已转人工开票）");
            return (false, "开票平台调用异常");
        }
    }

    /// <summary>宽容解析开票平台响应，返回 (是否成功, 发票号码, 发票代码)</summary>
    private static (bool Ok, string? InvoiceNo, string? InvoiceCode) ParsePlatformResponse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // 成功判定：有 success=true，或 code 为 0/200/SUCCESS 之一
            var ok = true;
            if (root.TryGetProperty("success", out var successProp) && successProp.ValueKind == JsonValueKind.False)
            {
                ok = false;
            }
            if (root.TryGetProperty("code", out var codeProp))
            {
                var codeText = codeProp.ValueKind == JsonValueKind.Number
                    ? codeProp.GetRawText()
                    : codeProp.GetString() ?? string.Empty;
                ok = ok && (codeText == "0" || codeText == "200" ||
                            string.Equals(codeText, "SUCCESS", StringComparison.OrdinalIgnoreCase));
            }

            string? invoiceNo = null;
            string? invoiceCode = null;

            // 发票号码可能在根上，也可能在 data 里
            var candidates = new List<JsonElement> { root };
            if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Object)
            {
                candidates.Add(dataProp);
            }

            foreach (var node in candidates)
            {
                invoiceNo ??= FirstString(node, "invoiceNo", "invoice_no", "number", "fpqqlsh");
                invoiceCode ??= FirstString(node, "invoiceCode", "invoice_code", "code");
            }

            return (ok, invoiceNo, invoiceCode);
        }
        catch
        {
            return (false, null, null);
        }
    }

    private static string? FirstString(JsonElement node, params string[] names)
    {
        foreach (var name in names)
        {
            if (node.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var v = prop.GetString();
                if (!string.IsNullOrWhiteSpace(v)) return v!.Trim();
            }
        }
        return null;
    }

    // ============================================================
    // 内部：小工具
    // ============================================================

    /// <summary>去空白；空串统一转 null，避免库里出现大量 ''</summary>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>把同一客户的其它开票信息的默认标记清掉（保证至多一条默认）</summary>
    private async Task ClearOtherDefaultAsync(long customerId, long keepId)
    {
        var others = await _customerInvoiceRepo.Query()
            .Where(c => c.CustomerId == customerId && c.Id != keepId && c.IsDefault == 1)
            .ToListAsync();

        foreach (var item in others)
        {
            item.IsDefault = 0;
            _customerInvoiceRepo.Update(item);
        }
    }

    /// <summary>追加备注（保留原有内容，便于追溯）</summary>
    private static void AppendRemark(OrderInvoice invoice, string text)
    {
        invoice.Remark = string.IsNullOrWhiteSpace(invoice.Remark)
            ? text
            : (invoice.Remark.Length > 400 ? invoice.Remark[..400] : invoice.Remark) + "；" + text;
    }

    /// <summary>实体 → 客户开票信息 DTO（含中文名）</summary>
    private static CustomerInvoiceInfoDto ToDto(CustomerInvoiceInfo entity)
    {
        var dto = new CustomerInvoiceInfoDto
        {
            Id = entity.Id,
            CustomerId = entity.CustomerId,
            TitleType = entity.TitleType,
            TitleName = entity.TitleName,
            TaxNo = entity.TaxNo,
            BankName = entity.BankName,
            BankAccount = entity.BankAccount,
            Address = entity.Address,
            Phone = entity.Phone,
            IsDefault = entity.IsDefault,
            Status = entity.Status,
            CreateTime = entity.CreateTime
        };
        dto.TitleTypeName = InvoiceTitleTypeNames.GetName(entity.TitleType);
        return dto;
    }
}
