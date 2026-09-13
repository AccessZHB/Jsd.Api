namespace Jsd.Api.Models.StockOut;

/// <summary>
/// 出库单详情 DTO（GET /api/stockOut/detail/{id} 返回）。
/// 继承列表 DTO 的全部主表字段，Items 中返回完整明细（含商品名称、SKU 编码等回显字段）。
/// </summary>
public class StockOutDetailDto : StockOutListDto
{
}
