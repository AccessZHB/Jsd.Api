using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jsd.Api.Entities;

/// <summary>
/// 客户开票信息表 customer_invoice_info（发票与税务管理模块）
///
/// 一个客户（mem_member）可保存多套开票抬头（个人/企业），其中一套可设为默认。
/// ⚠️ 抬头在【开票时会被快照进 order_invoice】：客户事后修改抬头，历史发票的开票信息不变 ——
///    这是税务合规的基本要求（已开具的发票抬头必须与当时申请一致）。
/// </summary>
[Table("customer_invoice_info")]
public class CustomerInvoiceInfo
{
    /// <summary>ID（主键）</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>关联客户ID（mem_member.id）</summary>
    [Column("customer_id")]
    public long CustomerId { get; set; }

    /// <summary>抬头类型（见 InvoiceTitleType：1-个人 2-企业）</summary>
    [Column("title_type")]
    public int TitleType { get; set; }

    /// <summary>抬头名称（个人=姓名，企业=营业执照全称）</summary>
    [Required]
    [StringLength(100)]
    [Column("title_name")]
    public string TitleName { get; set; } = string.Empty;

    /// <summary>纳税人识别号 / 统一社会信用代码（企业必填，15/17/18/20 位）</summary>
    [StringLength(32)]
    [Column("tax_no")]
    public string? TaxNo { get; set; }

    /// <summary>开户银行（开专票时必填）</summary>
    [StringLength(100)]
    [Column("bank_name")]
    public string? BankName { get; set; }

    /// <summary>银行账号（开专票时必填）</summary>
    [StringLength(64)]
    [Column("bank_account")]
    public string? BankAccount { get; set; }

    /// <summary>注册地址（开专票时必填）</summary>
    [StringLength(200)]
    [Column("address")]
    public string? Address { get; set; }

    /// <summary>注册电话 / 联系电话（开专票时必填）</summary>
    [StringLength(32)]
    [Column("phone")]
    public string? Phone { get; set; }

    /// <summary>是否默认开票信息：0-否 1-是（同一客户至多一条为 1，由 Service 层维护）</summary>
    [Column("is_default")]
    public int IsDefault { get; set; }

    /// <summary>状态：0-正常 1-停用</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>创建人</summary>
    [StringLength(64)]
    [Column("create_by")]
    public string? CreateBy { get; set; }

    /// <summary>创建时间</summary>
    [Column("create_time")]
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间（数据库 ON UPDATE CURRENT_TIMESTAMP）</summary>
    [Column("update_time")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; }
}
