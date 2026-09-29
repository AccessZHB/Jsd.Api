using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 微信对账单主表 wechat_daily_bill（T+1 对账模块）
///
/// 「一天 + 一种账单类型」一条记录（uk_bill_date_type 保证重复拉取走 UPDATE 而不是堆数据）。
/// 生命周期（recon_status）：
///   0 待下载 → 1 处理中（已申请到 download_url / 正在入库明细）→ 2 对账完成 / 3 异常。
/// ⚠️ 微信当日账单次日才提供下载，T+1 由任务 DailyReconciliationJob（每天 03:00 拉昨日）驱动。
/// </summary>
[Table("wechat_daily_bill")]
public class WechatDailyBill
{
    /// <summary>ID（主键）</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>账单日期（如 2026-09-27）</summary>
    [Column("bill_date", TypeName = "date")]
    public DateTime BillDate { get; set; }

    /// <summary>账单类型：ALL / SUCCESS / REFUND</summary>
    [StringLength(16)]
    [Column("bill_type")]
    public string BillType { get; set; } = "ALL";

    /// <summary>文件下载地址（微信返回，有效期很短，仅留痕便于人工复盘）</summary>
    [StringLength(1024)]
    [Column("download_url")]
    public string? DownloadUrl { get; set; }

    /// <summary>账单摘要算法（SHA1）</summary>
    [StringLength(16)]
    [Column("hash_type")]
    public string? HashType { get; set; }

    /// <summary>账单原始摘要值</summary>
    [StringLength(255)]
    [Column("hash_value")]
    public string? HashValue { get; set; }

    /// <summary>总交易笔数（明细行统计）</summary>
    [Column("total_count")]
    public int TotalCount { get; set; }

    /// <summary>总金额（单位：元）</summary>
    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    /// <summary>退款金额合计（单位：元）</summary>
    [Column("refund_amount")]
    public decimal RefundAmount { get; set; }

    /// <summary>手续费合计（单位：元）</summary>
    [Column("total_fee")]
    public decimal TotalFee { get; set; }

    /// <summary>对账状态（见 BilReconStatus：0待下载 1处理中 2对账完成 3异常）</summary>
    [Column("recon_status")]
    public int ReconStatus { get; set; }

    /// <summary>对账结论摘要</summary>
    [StringLength(1000)]
    [Column("recon_result")]
    public string? ReconResult { get; set; }

    /// <summary>异常信息</summary>
    [Column("error_msg")]
    public string? ErrorMsg { get; set; }

    /// <summary>创建时间</summary>
    [Column("create_time")]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE CURRENT_TIMESTAMP）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
