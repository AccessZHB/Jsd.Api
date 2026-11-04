namespace Jsd.Api.Models.Cart;

// ============================================================
// 购物车 DTO（小程序 /api/cart/*）
// 说明：JSON 全局 camelCase 序列化，前端拿到的是 skuId / processingType 等。
// ============================================================

/// <summary>单眼验光数据（球镜 / 柱镜 / 轴位 / 瞳距 / 瞳高，均为字符串保留原始精度）</summary>
public class CartPrescriptionEyeDto
{
    /// <summary>球镜 SPH（如 -1.75 / +1.00）</summary>
    public string? Sphere { get; set; }

    /// <summary>柱镜 CYL（如 -2.75）</summary>
    public string? Cylinder { get; set; }

    /// <summary>轴位 AXIS（0~180）</summary>
    public string? Axis { get; set; }

    /// <summary>瞳距 PD（单眼，可空）</summary>
    public string? Pd { get; set; }

    /// <summary>瞳高（选填，加工时需要）</summary>
    public string? Height { get; set; }
}

/// <summary>双眼验光数据</summary>
public class CartPrescriptionDto
{
    /// <summary>左眼</summary>
    public CartPrescriptionEyeDto? Left { get; set; }

    /// <summary>右眼</summary>
    public CartPrescriptionEyeDto? Right { get; set; }
}

/// <summary>
/// 加入购物车入参
/// </summary>
public class CartAddDto
{
    /// <summary>规格 SKU ID（prod_sku.id）</summary>
    public long SkuId { get; set; }

    /// <summary>数量（默认 1，最小 1）</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// 是否需要加工：0-不需要加工（只发片） 1-需要加工（邮寄镜架）
    /// 【关键】该字段参与合并判定，0 与 1 必然是两条独立记录。
    /// </summary>
    public int ProcessingType { get; set; }

    /// <summary>验光数据（only 需要加工时必填）</summary>
    public CartPrescriptionDto? Prescription { get; set; }
}

/// <summary>修改数量入参</summary>
public class CartUpdateQtyDto
{
    /// <summary>购物车行ID</summary>
    public long Id { get; set; }

    /// <summary>新的数量（最小 1）</summary>
    public int Quantity { get; set; }
}

/// <summary>按行ID操作入参（删除 / 勾选）</summary>
public class CartIdDto
{
    /// <summary>购物车行ID</summary>
    public long Id { get; set; }
}

/// <summary>勾选状态切换入参</summary>
public class CartToggleDto
{
    /// <summary>购物车行ID</summary>
    public long Id { get; set; }

    /// <summary>目标勾选状态</summary>
    public bool Selected { get; set; }
}

/// <summary>全选 / 全不选入参</summary>
public class CartToggleAllDto
{
    /// <summary>目标勾选状态</summary>
    public bool Selected { get; set; }
}

/// <summary>
/// 购物车行（返回给小程序；字段与小程序 CartItem 保持一致，前端无需再适配）
/// </summary>
public class CartItemDto
{
    /// <summary>购物车行ID</summary>
    public long Id { get; set; }

    /// <summary>前端唯一键（= Id 的字符串形式，保持原 CartItem.key 语义）</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>商品ID</summary>
    public long ProductId { get; set; }

    /// <summary>SKU ID</summary>
    public long SkuId { get; set; }

    /// <summary>商品名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>商品图片</summary>
    public string? Image { get; set; }

    /// <summary>规格摘要（SKU 名称 + 加工类型文案）</summary>
    public string SpecText { get; set; } = string.Empty;

    /// <summary>会员成交单价（服务端按会员身份实时计算）</summary>
    public decimal Price { get; set; }

    /// <summary>标准原价（prod_sku.retail_price）</summary>
    public decimal? OriginPrice { get; set; }

    /// <summary>数量</summary>
    public int Qty { get; set; }

    /// <summary>是否勾选</summary>
    public bool Checked { get; set; }

    /// <summary>是否镜片类（决定列表是否显示加工标签）</summary>
    public bool IsLens { get; set; }

    /// <summary>
    /// 加工状态（购物车阶段只有两种）：
    /// unprocessed-待加工（需要加工）；none-无需加工（只发片）
    /// </summary>
    public string ProcessingStatus { get; set; } = "none";

    /// <summary>是否需要加工：0-不需要 1-需要（前端标签以此为准）</summary>
    public int ProcessingType { get; set; }

    /// <summary>加工类型文案（橙色「待加工」/ 灰色「无需加工」）</summary>
    public string ProcessingTypeText { get; set; } = string.Empty;

    /// <summary>验光数据（原样回传，前端自行格式化展示）</summary>
    public CartPrescriptionDto? Prescription { get; set; }
}

/// <summary>
/// 购物车列表（含统计，前端底部结算栏直接用）
/// </summary>
public class CartListDto
{
    /// <summary>购物车行</summary>
    public List<CartItemDto> Items { get; set; } = new();

    /// <summary>全部商品件数（含未勾选）</summary>
    public int TotalCount { get; set; }

    /// <summary>已勾选件数</summary>
    public int SelectedCount { get; set; }

    /// <summary>已勾选金额合计</summary>
    public decimal SelectedAmount { get; set; }

    /// <summary>是否全选</summary>
    public bool AllChecked { get; set; }

    /// <summary>是否含有待加工商品（结算页需二次核对光度）</summary>
    public bool HasProcessing { get; set; }
}
