namespace Jsd.Api.Models.Logistics;

/// <summary>
/// 物流轨迹单条节点（时间轴的一项）
/// 后端统一数据模型，屏蔽快递100/快递鸟等各家字段差异。
/// </summary>
public class LogisticsTrackNode
{
    /// <summary>轨迹时间（如 2026-09-27 10:00:00）</summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>轨迹描述（快递100 放在 context 里，通常已含地点与状态）</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>地点（快递100 未单独返回，缺省空；保留字段便于其它平台扩展）</summary>
    public string Location { get; set; } = string.Empty;
}

/// <summary>
/// 物流轨迹查询结果（统一数据模型）
/// </summary>
public class LogisticsTrackResult
{
    /// <summary>快递单号</summary>
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>快递公司编码（如 shunfeng）</summary>
    public string CarrierCode { get; set; } = string.Empty;

    /// <summary>快递公司名称</summary>
    public string CarrierName { get; set; } = string.Empty;

    /// <summary>
    /// 物流状态（对齐快递100 实时查询 state）：
    /// 0-在途 1-揽收 2-疑难 3-签收 4-退签 5-派件 6-退回
    /// </summary>
    public int State { get; set; }

    /// <summary>物流状态中文描述</summary>
    public string StateText { get; set; } = string.Empty;

    /// <summary>轨迹节点（已按时间倒序：最新一条在最前）</summary>
    public List<LogisticsTrackNode> Nodes { get; set; } = new();

    /// <summary>预估送达时间（第三方返回则填，否则 null）</summary>
    public string? EstimatedDelivery { get; set; }

    /// <summary>是否为演示/未配置真实密钥时返回的模拟数据（前端用于提示）</summary>
    public bool IsMock { get; set; }

    /// <summary>本次查询时间</summary>
    public string QueryTime { get; set; } = string.Empty;
}

/// <summary>
/// 智能单号识别结果（传入单号自动识别快递公司）
/// </summary>
public class LogisticsIdentifyResult
{
    /// <summary>快递单号</summary>
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>识别出的快递公司编码（识别失败为空）</summary>
    public string CarrierCode { get; set; } = string.Empty;

    /// <summary>识别出的快递公司名称</summary>
    public string CarrierName { get; set; } = string.Empty;

    /// <summary>是否为演示数据</summary>
    public bool IsMock { get; set; }
}

/// <summary>
/// 地图轨迹链接（可选能力）
/// </summary>
public class LogisticsMapResult
{
    /// <summary>快递单号</summary>
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>可在 iframe 中展示的轨迹页 URL；无则返回空字符串</summary>
    public string MapUrl { get; set; } = string.Empty;
}
