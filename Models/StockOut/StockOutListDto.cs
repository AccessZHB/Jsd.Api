namespace Jsd.Api.Models.StockOut;

/// <summary>
/// 出库单列表项 DTO（分页查询返回）。
/// 列表场景为性能考虑不填充明细（Items 保留为空集合），明细请走详情接口。
/// </summary>
public class StockOutListDto
{
    /// <summary>出库单ID</summary>
    public long Id { get; set; }

    /// <summary>出库单号（CK + 时间戳 + 随机数）</summary>
    public string StockOutNo { get; set; } = string.Empty;

    /// <summary>出库类型：1-样品领用 2-报损 3-其他</summary>
    public int OutType { get; set; }

    /// <summary>出库总数量（各明细 quantity 之和，服务端计算）</summary>
    public int TotalQty { get; set; }

    /// <summary>状态：0-待出库 1-已完成</summary>
    public int Status { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>操作人</summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>
    /// 明细数量（列表页展示用，后端单独统计）。
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// 出库明细列表（列表查询不填充；详情接口返回完整内容）。
    /// </summary>
    public List<StockOutItemDto> Items { get; set; } = new();
}
