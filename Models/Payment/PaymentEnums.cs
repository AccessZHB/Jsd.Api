namespace Jsd.Api.Models.Payment;

/// <summary>支付单状态（对应 payment_order.status）</summary>
public enum PayOrderStatus : int
{
    /// <summary>INIT - 初始/待支付（下单后未发起或刚发起）</summary>
    Init = 0,

    /// <summary>PAYING - 支付中（已发起支付，等待回调/查单确认）</summary>
    Paying = 1,

    /// <summary>SUCCESS - 支付成功</summary>
    Success = 2,

    /// <summary>FAIL - 支付失败</summary>
    Fail = 3,

    /// <summary>CLOSED - 已关闭/已关单（超时未支付或用户取消）</summary>
    Closed = 4
}

/// <summary>支付单状态 → 中文名</summary>
public static class PayOrderStatusNames
{
    public static string GetName(int status) => status switch
    {
        (int)PayOrderStatus.Init => "待支付",
        (int)PayOrderStatus.Paying => "支付中",
        (int)PayOrderStatus.Success => "支付成功",
        (int)PayOrderStatus.Fail => "支付失败",
        (int)PayOrderStatus.Closed => "已关闭",
        _ => "未知"
    };
}

/// <summary>支付渠道（对应 payment_order.pay_channel）</summary>
public enum PayChannel : int
{
    /// <summary>未知/未指定</summary>
    Unknown = 0,

    /// <summary>微信 JSAPI（公众号/小程序）</summary>
    WechatJsApi = 1,

    /// <summary>微信 APP 支付</summary>
    WechatApp = 2,

    /// <summary>微信 Native 扫码支付</summary>
    WechatNative = 3,

    /// <summary>微信 H5 支付</summary>
    WechatH5 = 4
}

/// <summary>回调处理状态（对应 pay_callback_log.handle_status）</summary>
public enum CallbackHandleStatus : int
{
    /// <summary>待处理</summary>
    Pending = 0,

    /// <summary>处理成功</summary>
    Success = 1,

    /// <summary>处理失败（验签/解密/业务异常）</summary>
    Failed = 2,

    /// <summary>已忽略（重复回调被幂等拦截）</summary>
    Ignored = 3
}
