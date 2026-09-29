using System.Text.RegularExpressions;

namespace Jsd.Api.Models.Invoice;

/// <summary>抬头类型（对应 customer_invoice_info.title_type / order_invoice.title_type）</summary>
public enum InvoiceTitleType : int
{
    /// <summary>个人（自然人）—— 税号可为空</summary>
    Personal = 1,

    /// <summary>企业（单位）—— 税号必填</summary>
    Enterprise = 2
}

/// <summary>发票类型（对应 order_invoice.invoice_type）</summary>
public enum InvoiceType : int
{
    /// <summary>增值税电子普通发票</summary>
    ElectronicNormal = 1,

    /// <summary>增值税专用发票（必须企业抬头 + 完整开户行/账号/地址/电话）</summary>
    SpecialVat = 2
}

/// <summary>开票状态（对应 order_invoice.status）</summary>
public enum InvoiceStatus : int
{
    /// <summary>申请中（已提交，等待开票）</summary>
    Applying = 0,

    /// <summary>已开票（财务已回写发票代码/号码）</summary>
    Issued = 1,

    /// <summary>已作废（撤销申请 或 红冲）</summary>
    Canceled = 2
}

/// <summary>抬头类型 → 中文名</summary>
public static class InvoiceTitleTypeNames
{
    public static string GetName(int titleType) => titleType switch
    {
        (int)InvoiceTitleType.Personal => "个人",
        (int)InvoiceTitleType.Enterprise => "企业",
        _ => "未知"
    };
}

/// <summary>发票类型 → 中文名</summary>
public static class InvoiceTypeNames
{
    public static string GetName(int invoiceType) => invoiceType switch
    {
        (int)InvoiceType.ElectronicNormal => "增值税电子普通发票",
        (int)InvoiceType.SpecialVat => "增值税专用发票",
        _ => "未知"
    };
}

/// <summary>开票状态 → 中文名</summary>
public static class InvoiceStatusNames
{
    public static string GetName(int status) => status switch
    {
        (int)InvoiceStatus.Applying => "申请中",
        (int)InvoiceStatus.Issued => "已开票",
        (int)InvoiceStatus.Canceled => "已作废",
        _ => "未知"
    };
}

/// <summary>
/// 纳税人识别号（统一社会信用代码）校验规则。
///
/// 国内现行规则：长度为 15 / 17 / 18 / 20 位，且只允许大写字母与数字。
///   · 18 位 = 统一社会信用代码（企业最常见）
///   · 15 位 = 老版纳税人识别号
///   · 17 / 20 位 = 部分地区/特殊主体
/// ⚠️ 这里只做「格式校验」，不校验校验位与工商真实性 —— 真实性只有在开票平台侧才校验得了。
/// </summary>
public static class TaxNoRule
{
    /// <summary>合法长度集合</summary>
    private static readonly int[] ValidLengths = { 15, 17, 18, 20 };

    private static readonly Regex Pattern = new("^[0-9A-Z]+$", RegexOptions.Compiled);

    /// <summary>默认税率（税额反算用：税额 = 价税合计 / (1 + 税率) * 税率）</summary>
    public const decimal DefaultTaxRate = 0.13m;

    /// <summary>
    /// 校验税号格式。
    /// </summary>
    /// <param name="taxNo">待校验税号（可为空）</param>
    /// <param name="required">是否必填（企业抬头为 true，个人为 false）</param>
    /// <param name="error">不合法时的中文提示；合法时为 null</param>
    public static bool IsValid(string? taxNo, bool required, out string? error)
    {
        error = null;

        var value = (taxNo ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            if (required)
            {
                error = "企业抬头必须填写纳税人识别号";
                return false;
            }
            return true;
        }

        value = value.ToUpperInvariant();

        if (!Pattern.IsMatch(value))
        {
            error = "纳税人识别号只能包含数字和大写字母";
            return false;
        }

        if (!ValidLengths.Contains(value.Length))
        {
            error = "纳税人识别号长度不正确（应为 15/17/18/20 位）";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 按「价税合计」反算税额：税额 = 价税合计 / (1 + 税率) × 税率，保留 2 位（四舍五入）。
    /// 例：1130 元、13% → 130.00 元。
    /// </summary>
    public static decimal CalcTaxAmount(decimal totalAmount, decimal taxRate)
    {
        if (totalAmount <= 0 || taxRate <= 0) return 0m;
        var tax = totalAmount / (1 + taxRate) * taxRate;
        return decimal.Round(tax, 2, MidpointRounding.AwayFromZero);
    }
}
