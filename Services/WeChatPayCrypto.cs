using System.Security.Cryptography;
using System.Text;
using Jsd.Api.Models.Payment;

namespace Jsd.Api.Services;

/// <summary>
/// 微信支付 V3 密码学工具（纯静态，无状态）。
/// 涵盖三件事：
///   1) 请求签名：商户侧用 API 私钥（RSA-SHA256）生成 Authorization 头；
///   2) 回调验签：用微信【平台证书公钥】验签，防伪造回调（替换了原 Finance 模块的占位 VerifySign）；
///   3) 回调解密：用 APIv3 密钥做 AES-256-GCM 解密 resource.ciphertext。
/// 命名与官方一致，方便对照：https://pay.weixin.qq.com/wiki/doc/apiv3_partner/wechatpay/wechatpay3_0.shtml
/// </summary>
public static class WeChatPayCrypto
{
    // ---------- 1. 请求签名：生成 Authorization 头 ----------

    /// <summary>
    /// 构造 V3 请求 Authorization 头。
    /// 签名串 = method\n + url(不含域名，含 query)\n + timestamp\n + nonce\n + body\n
    /// （body 为原始请求体，GET 为空字符串），再用商户 API 私钥做 RSA-SHA256（PKCS1 v1.5）签名，base64 输出。
    /// </summary>
    public static string BuildAuthorization(string method, string url, string body, WeChatPayOptions options)
    {
        var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");
        var message = $"{method}\n{url}\n{timestamp}\n{nonce}\n{body}\n";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(options.MerchantPrivateKey.ToCharArray());
        var signature = Convert.ToBase64String(
            rsa.SignData(Encoding.UTF8.GetBytes(message), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        return $"WECHATPAY2-SHA256-RSA2048 " +
               $"mchid=\"{options.MchId}\"," +
               $"nonce_str=\"{nonce}\"," +
               $"signature=\"{signature}\"," +
               $"timestamp=\"{timestamp}\"," +
               $"serial_no=\"{options.MerchantSerialNo}\"";
    }

    // ---------- 2. 回调验签 ----------

    /// <summary>
    /// 验证微信回调签名。
    /// 验签串 = timestamp\n + nonce\n + body\n （注意：回调验签不含 method/url）。
    /// 用微信【平台证书公钥】验 RSA-SHA256 签名（PKCS1 v1.5）。
    /// 平台证书通过 serial_no 选择（本系统当前只配置一张，多证书轮换时按请求头 Wechatpay-Serial 选）。
    /// </summary>
    public static bool VerifySignature(string timestamp, string nonce, string body, string signature, string platformPublicKey)
    {
        if (string.IsNullOrWhiteSpace(platformPublicKey) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        try
        {
            var message = $"{timestamp}\n{nonce}\n{body}\n";
            var sigBytes = Convert.FromBase64String(signature);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(platformPublicKey.ToCharArray());
            return rsa.VerifyData(Encoding.UTF8.GetBytes(message), sigBytes,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    // ---------- 3. 回调解密（AES-256-GCM） ----------

    /// <summary>
    /// 解密微信回调 resource（AEAD_AES_256_GCM）。
    /// ciphertext 经 base64 解码后，末尾 16 字节是 GCM 认证标签（tag），前面是密文。
    /// 解密需要：32 字节 APIv3 密钥、12 字节 nonce、可选 associated_data、密文、tag。
    /// </summary>
    public static string DecryptResource(string ciphertext, string nonce, string? associatedData, string apiV3Key)
    {
        var key = Encoding.UTF8.GetBytes(apiV3Key);       // APIv3 密钥固定 32 字节（256-bit）
        var nonceBytes = Encoding.UTF8.GetBytes(nonce);   // nonce 通常 12 字节
        var cipherBytes = Convert.FromBase64String(ciphertext);

        // GCM 模式：密文后 16 字节是认证标签
        const int tagLength = 16;
        if (cipherBytes.Length <= tagLength)
        {
            throw new InvalidOperationException("回调解密失败：密文长度异常");
        }

        var cipherText = cipherBytes[..^tagLength];
        var tag = cipherBytes[^tagLength..];
        var plainBytes = new byte[cipherText.Length];

        // .NET 8+ 推荐显式指定 tag 长度（GCM 固定 16 字节），避免使用已过时的单参构造函数
        using var aes = new AesGcm(key, tagLength);
        aes.Decrypt(nonceBytes, cipherText, tag, plainBytes,
            associatedData == null ? null : Encoding.UTF8.GetBytes(associatedData));

        return Encoding.UTF8.GetString(plainBytes);
    }
}
