namespace Jsd.Api.Models.StockIn;

/// <summary>
/// 入库单列表项 DTO（分页查询返回）。
/// 列表场景为性能考虑不填充明细（Items 保留为空集合），明细请走详情接口。
/// </summary>
public class StockInListDto
{
    /// <summary>入库单ID</summary>
    public long Id { get; set; }

    /// <summary>入库单号（RK + 时间戳 + 随机数）</summary>
    public string StockInNo { get; set; } = string.Empty;

    /// <summary>供应商ID（可空）</summary>
    public long? SupplierId { get; set; }

    /// <summary>供应商名称（Include 导航属性映射，可空）</summary>
    public string? SupplierName { get; set; }

    /// <summary>入库总成本金额</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>状态：0-待入库 1-已完成 2-已取消</summary>
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
    /// 入库明细列表（列表查询不填充；详情接口返回完整内容）。
    /// </summary>
    public List<StockInItemDto> Items { get; set; } = new();
}
