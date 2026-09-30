namespace Jsd.Api.Models.Invoice;

/// <summary>保存客户开票信息入参（新增 or 修改；Id &gt; 0 为修改）</summary>
public class SaveCustomerInvoiceDto
{
    /// <summary>开票信息ID（0=新增，大于0=修改）</summary>
    public long Id { get; set; }

    /// <summary>关联客户ID（mem_member.id）</summary>
    public long CustomerId { get; set; }

    /// <summary>抬头类型（见 InvoiceTitleType：1-个人 2-企业）</summary>
    public int TitleType { get; set; } = (int)InvoiceTitleType.Enterprise;

    /// <summary>抬头名称</summary>
    public string TitleName { get; set; } = string.Empty;

    /// <summary>纳税人识别号（企业必填）</summary>
    public string? TaxNo { get; set; }

    /// <summary>开户银行（专票需要）</summary>
    public string? BankName { get; set; }

    /// <summary>银行账号（专票需要）</summary>
    public string? BankAccount { get; set; }

    /// <summary>注册地址（专票需要）</summary>
    public string? Address { get; set; }

    /// <summary>联系电话（专票需要）</summary>
    public string? Phone { get; set; }

    /// <summary>是否设为该客户的默认开票信息（0/1）</summary>
    public int IsDefault { get; set; }
}

/// <summary>客户开票信息列表项</summary>
public class CustomerInvoiceInfoDto
{
    /// <summary>ID</summary>
    public long Id { get; set; }

    /// <summary>客户ID</summary>
    public long CustomerId { get; set; }

    /// <summary>抬头类型</summary>
    public int TitleType { get; set; }

    /// <summary>抬头类型名称</summary>
    public string TitleTypeName { get; set; } = string.Empty;

    /// <summary>抬头名称</summary>
    public string TitleName { get; set; } = string.Empty;

    /// <summary>纳税人识别号</summary>
    public string? TaxNo { get; set; }

    /// <summary>开户银行</summary>
    public string? BankName { get; set; }

    /// <summary>银行账号</summary>
    public string? BankAccount { get; set; }

    /// <summary>注册地址</summary>
    public string? Address { get; set; }

    /// <summary>联系电话</summary>
    public string? Phone { get; set; }

    /// <summary>是否默认</summary>
    public int IsDefault { get; set; }

    /// <summary>状态：0-正常 1-停用</summary>
    public int Status { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>提交开票申请入参</summary>
public class ApplyInvoiceDto
{
    /// <summary>订单ID（trx_order.id）</summary>
    public long OrderId { get; set; }

    /// <summary>关联支付单ID（payment_order.id，可空：线下付款/信用订单）</summary>
    public long? PaymentId { get; set; }

    /// <summary>发票类型（见 InvoiceType：1-电子普票 2-增值税专用发票）</summary>
    // ⚠️ 必须写全限定名：本类属性也叫 InvoiceType，直接写 (int)InvoiceType.X 会被编译器当成引用实例属性（CS0236）
    public int InvoiceType { get; set; } = (int)Models.Invoice.InvoiceType.ElectronicNormal;

    /// <summary>抬头类型（1-个人 2-企业）</summary>
    public int TitleType { get; set; } = (int)InvoiceTitleType.Enterprise;

    /// <summary>抬头名称</summary>
    public string TitleName { get; set; } = string.Empty;

    /// <summary>纳税人识别号（企业抬头必填）</summary>
    public string? TaxNo { get; set; }

    /// <summary>开户银行（专票必填）</summary>
    public string? BankName { get; set; }

    /// <summary>银行账号（专票必填）</summary>
    public string? BankAccount { get; set; }

    /// <summary>注册地址（专票必填）</summary>
    public string? Address { get; set; }

    /// <summary>联系电话（专票必填）</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// 发票总金额（价税合计，元）。服务端会校验：&gt; 0 且不超过订单实付金额。
    /// 不传或传 0 时，服务端按【订单实付金额】自动填充。
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 税额（元）。不传时按 13% 税率从价税合计反算（见 TaxNoRule.CalcTaxAmount）。
    /// </summary>
    public decimal? TaxAmount { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>开票申请结果</summary>
public class ApplyInvoiceResult
{
    /// <summary>开票记录ID</summary>
    public long InvoiceId { get; set; }

    /// <summary>开票状态（见 InvoiceStatus）</summary>
    public int Status { get; set; }

    /// <summary>开票状态名称</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>发票总金额（价税合计，元）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>税额（元）</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>是否需要财务人工开票（未对接第三方开票平台时为 true）</summary>
    public bool NeedManualIssue { get; set; }

    /// <summary>提示信息</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>作废 / 红冲入参</summary>
public class CancelInvoiceDto
{
    /// <summary>红冲/作废原因（必填，2~200 字）</summary>
    public string RedReason { get; set; } = string.Empty;

    /// <summary>
    /// 关联蓝票发票号码。已开票(1)的红冲场景必填（即被红冲的那张票号）；
    /// 申请中(0)撤销时可留空。
    /// </summary>
    public string? BlueInvoiceNo { get; set; }
}

/// <summary>作废 / 红冲结果</summary>
public class CancelInvoiceResult
{
    /// <summary>开票记录ID</summary>
    public long InvoiceId { get; set; }

    /// <summary>作废后状态（固定 2-已作废）</summary>
    public int Status { get; set; }

    /// <summary>状态名称</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>原状态（0-申请中 1-已开票）</summary>
    public int OldStatus { get; set; }

    /// <summary>是否红冲（true=红冲已开票的票；false=撤销申请中的申请）</summary>
    public bool IsRed { get; set; }

    /// <summary>红冲原因</summary>
    public string RedReason { get; set; } = string.Empty;

    /// <summary>关联蓝票号码</summary>
    public string? BlueInvoiceNo { get; set; }

    /// <summary>提示信息</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>财务回写票面信息入参（未对接开票平台时由财务手工开票后回填）</summary>
public class IssueInvoiceDto
{
    /// <summary>发票代码（全电发票可留空）</summary>
    public string? InvoiceCode { get; set; }

    /// <summary>发票号码（必填，唯一）</summary>
    public string InvoiceNo { get; set; } = string.Empty;

    /// <summary>开票日期（不传则取当天）</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>开票记录列表查询入参</summary>
public class InvoiceQueryDto
{
    /// <summary>订单ID（精确）</summary>
    public long? OrderId { get; set; }

    /// <summary>
    /// 下单买家ID（按 trx_order.buyer_id 过滤）。
    ///
    /// ✅ 口径澄清（BUG-05 修正，此前注释有误）：
    ///    trx_order.buyer_id 指向的就是【mem_member.id】（会员/客户），
    ///    与后台管理账号 sys_user 物理隔离（依据 customer_init.sql 建表注释，
    ///    以及 OrderService 中 MemMemberId = dto.BuyerId、PriceService 中
    ///    MemMembers.Any(m => m.Id == x.o.BuyerId) 的实际用法）。
    ///    因此它与 customer_invoice_info.customer_id 是【同一套 ID】，
    ///    可直接用 buyer_id 关联该客户保存的开票抬头，并非"两套互不相干的人"。
    /// </summary>
    public long? BuyerId { get; set; }

    /// <summary>开票状态（见 InvoiceStatus）</summary>
    public int? Status { get; set; }

    /// <summary>发票类型（见 InvoiceType）</summary>
    public int? InvoiceType { get; set; }

    /// <summary>关键词（发票号码 / 抬头名称 / 订单号）</summary>
    public string? Keyword { get; set; }

    /// <summary>申请时间起</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>申请时间止</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>页码</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>开票记录列表项</summary>
public class InvoiceListDto
{
    /// <summary>ID</summary>
    public long Id { get; set; }

    /// <summary>订单ID</summary>
    public long OrderId { get; set; }

    /// <summary>订单号（联查 trx_order.order_no）</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>支付单ID</summary>
    public long? PaymentId { get; set; }

    /// <summary>发票类型</summary>
    public int InvoiceType { get; set; }

    /// <summary>发票类型名称</summary>
    public string InvoiceTypeName { get; set; } = string.Empty;

    /// <summary>抬头类型（1-个人 2-企业）</summary>
    public int TitleType { get; set; }

    /// <summary>抬头类型名称</summary>
    public string TitleTypeName { get; set; } = string.Empty;

    /// <summary>抬头名称</summary>
    public string TitleName { get; set; } = string.Empty;

    /// <summary>纳税人识别号</summary>
    public string? TaxNo { get; set; }

    /// <summary>发票代码</summary>
    public string? InvoiceCode { get; set; }

    /// <summary>发票号码</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>开票日期</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>发票总金额（元）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>税额（元）</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>开票状态</summary>
    public int Status { get; set; }

    /// <summary>开票状态名称</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>申请人名称</summary>
    public string? ApplicantName { get; set; }

    /// <summary>创建时间（申请时间）</summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>开票记录详情</summary>
public class InvoiceDetailDto
{
    /// <summary>ID</summary>
    public long Id { get; set; }

    /// <summary>订单ID</summary>
    public long OrderId { get; set; }

    /// <summary>订单号</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>订单实付金额（元，用于核对票面金额）</summary>
    public decimal OrderPayAmount { get; set; }

    /// <summary>支付单ID</summary>
    public long? PaymentId { get; set; }

    /// <summary>发票类型</summary>
    public int InvoiceType { get; set; }

    /// <summary>发票类型名称</summary>
    public string InvoiceTypeName { get; set; } = string.Empty;

    /// <summary>抬头类型</summary>
    public int TitleType { get; set; }

    /// <summary>抬头类型名称</summary>
    public string TitleTypeName { get; set; } = string.Empty;

    /// <summary>抬头名称</summary>
    public string TitleName { get; set; } = string.Empty;

    /// <summary>纳税人识别号</summary>
    public string? TaxNo { get; set; }

    /// <summary>开户银行</summary>
    public string? BankName { get; set; }

    /// <summary>银行账号</summary>
    public string? BankAccount { get; set; }

    /// <summary>注册地址</summary>
    public string? Address { get; set; }

    /// <summary>联系电话</summary>
    public string? Phone { get; set; }

    /// <summary>发票代码</summary>
    public string? InvoiceCode { get; set; }

    /// <summary>发票号码</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>开票日期</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>发票总金额（价税合计，元）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>税额（元）</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>开票状态</summary>
    public int Status { get; set; }

    /// <summary>开票状态名称</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>红冲原因</summary>
    public string? RedReason { get; set; }

    /// <summary>关联蓝票号码</summary>
    public string? BlueInvoiceNo { get; set; }

    /// <summary>申请人名称</summary>
    public string? ApplicantName { get; set; }

    /// <summary>操作人名称（开票/作废的财务）</summary>
    public string? OperatorName { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
}
