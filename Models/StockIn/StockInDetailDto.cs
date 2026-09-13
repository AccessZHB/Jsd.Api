namespace Jsd.Api.Models.StockIn;

/// <summary>
/// 入库单详情 DTO（GET /api/stockIn/detail/{id} 返回）。
/// 继承列表 DTO 的全部主表字段，Items 中返回完整明细（含商品名称、SKU 编码等回显字段）。
/// </summary>
public class StockInDetailDto : StockInListDto
{
}
