namespace Jsd.Api.Models.StockCheck;

/// <summary>盘点单状态：0-盘点中 1-已完成（本系统无审核环节）</summary>
public enum StockCheckStatus
{
    /// <summary>盘点中（已创建、待录入实盘数量）</summary>
    Ongoing = 0,

    /// <summary>已完成（已录入实盘并计算盈亏、调整库存）</summary>
    Completed = 1
}

/// <summary>
/// 盘点单列表项 DTO（GET /api/stockCheck 返回）。
/// 差异汇总由后端统计：明细条数、系统库存合计、实盘合计、盈亏合计。
/// </summary>
public class StockCheckListDto
{
    /// <summary>盘点单ID</summary>
    public long Id { get; set; }

    /// <summary>盘点单号（PD + 时间戳 + 4位随机数）</summary>
    public string CheckNo { get; set; } = string.Empty;

    /// <summary>状态：0-盘点中 1-已完成</summary>
    public int Status { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>操作人</summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>明细条数</summary>
    public int ItemCount { get; set; }

    /// <summary>系统库存合计（所有明细 system_stock 之和）</summary>
    public int TotalSystemStock { get; set; }

    /// <summary>实盘合计（所有明细 actual_stock 之和）</summary>
    public int TotalActualStock { get; set; }

    /// <summary>盈亏合计（实盘-系统，正为盈、负为亏）</summary>
    public int TotalDiffQty { get; set; }

    /// <summary>明细列表（列表查询不填充；详情接口返回完整内容）</summary>
    public List<StockCheckItemDto> Items { get; set; } = new();
}

/// <summary>
/// 盘点单详情 DTO（GET /api/stockCheck/{id}），Items 含完整明细与回显字段。
/// </summary>
public class StockCheckDetailDto : StockCheckListDto
{
}
