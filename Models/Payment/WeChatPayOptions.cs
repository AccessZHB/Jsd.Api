namespace Jsd.Api.Models.Payment;

/// <summary>
/// 微信支付 V3 配置（对应 appsettings.json 的 WeChatPay 节）。
/// 配置缺失时 PayService 直接 Fail，不抛未捕获异常。
///
/// 获取路径（微信支付商户平台 → API 安全）：
///   · MchId / AppId：账户中心；
///   · ApiV3Key：设置 APIv3 密钥（32 字节）；
///   · MerchantPrivateKey / MerchantSerialNo：API 证书（apiclient_key.pem / apiclient_cert.pem）；
///   · PlatformPublicKey / PlatformCertSerialNo：平台证书（用于回调验签，可在商户平台下载或由接口自动获取）。
/// </summary>
public class WeChatPayOptions
{
    /// <summary>商户号</summary>
    public string MchId { get; set; } = string.Empty;

    /// <summary>AppID（公众号/小程序/移动应用）</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>APIv3 密钥（32 字节）</summary>
    public string ApiV3Key { get; set; } = string.Empty;

    /// <summary>商户 API 私钥（apiclient_key.pem 内容，含 -----BEGIN PRIVATE KEY-----）</summary>
    public string MerchantPrivateKey { get; set; } = string.Empty;

    /// <summary>商户证书序列号（apiclient_cert.pem 的序列号）</summary>
    public string MerchantSerialNo { get; set; } = string.Empty;

    /// <summary>微信平台证书公钥（平台证书公钥内容，含 -----BEGIN PUBLIC KEY-----，用于回调验签）</summary>
    public string PlatformPublicKey { get; set; } = string.Empty;

    /// <summary>微信平台证书序列号（多证书轮换时按请求头 Wechatpay-Serial 选择；当前单证书可留空）</summary>
    public string PlatformCertSerialNo { get; set; } = string.Empty;

    /// <summary>微信支付 API 基地址（默认生产环境）</summary>
    public string BaseUrl { get; set; } = "https://api.mch.weixin.qq.com";

    /// <summary>支付结果通知地址（需公网可访问，HTTPS）</summary>
    public string NotifyUrl { get; set; } = string.Empty;
}
