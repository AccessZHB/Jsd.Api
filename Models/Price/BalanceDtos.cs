namespace Jsd.Api.Models.Price;

// ============================================================
// 一、充值 DTO
// ============================================================

/// <summary>创建充值订单入参（POST /api/recharge/create）</summary>
public class RechargeCreateDto
{
    /// <summary>会员ID（mem_member.id）</summary>
    public long MemMemberId { get; set; }

    /// <summary>充值本金（元，必须 &gt; 0）</summary>
    public decimal RechargeAmount { get; set; }

    /// <summary>赠送金额（元，可为空/0）</summary>
    public decimal GiftAmount { get; set; }

    /// <summary>支付渠道：WECHAT / ALIPAY / BANK / OFFLINE（默认 WECHAT）</summary>
    public string? PayChannel { get; set; }
}

/// <summary>
/// 充值支付参数出参（POST /api/recharge/create）
/// 说明：本项目未接入微信支付 SDK，此处返回"待支付"的下单要素，
///       接入真实 SDK 后只需替换 PayParams 内容，接口契约不变。
/// </summary>
public class RechargePayParamsDto
{
    /// <summary>充值单ID</summary>
    public long RechargeId { get; set; }

    /// <summary>充值单号（商户订单号，回调时回填）</summary>
    public string RechargeNo { get; set; } = string.Empty;

    /// <summary>应付金额（本金；赠送不参与支付）</summary>
    public decimal PayAmount { get; set; }

    /// <summary>赠送金额</summary>
    public decimal GiftAmount { get; set; }

    /// <summary>支付渠道</summary>
    public string PayChannel { get; set; } = "WECHAT";

    /// <summary>二维码链接（真实接入后由微信返回 code_url）</summary>
    public string? CodeUrl { get; set; }

    /// <summary>预支付交易会话标识（真实接入后由微信返回 prepay_id）</summary>
    public string? PrepayId { get; set; }

    /// <summary>下单时间戳（秒）</summary>
    public long Timestamp { get; set; }
}

/// <summary>
/// 微信支付回调入参（POST /api/recharge/wechat-notify）
/// 说明：微信官方回调为 XML；若直连微信，在 Controller 内把 XML 解析成本 DTO 后调用 Service 即可。
/// </summary>
public class WechatNotifyDto
{
    /// <summary>商户订单号（本系统 recharge_no）</summary>
    public string? OutTradeNo { get; set; }

    /// <summary>微信支付订单号（幂等键）</summary>
    public string? TransactionId { get; set; }

    /// <summary>业务结果：SUCCESS / FAIL</summary>
    public string? ResultCode { get; set; }

    /// <summary>支付金额（元）</summary>
    public decimal? Amount { get; set; }

    /// <summary>签名（生产环境必须验签）</summary>
    public string? Sign { get; set; }

    /// <summary>原始报文（落库/排障用）</summary>
    public string? RawData { get; set; }
}

/// <summary>微信支付回调处理出参</summary>
public class WechatNotifyResultDto
{
    /// <summary>是否处理成功</summary>
    public bool Success { get; set; }

    /// <summary>是否为重复回调（幂等拦截，未做任何写入）</summary>
    public bool Duplicated { get; set; }

    /// <summary>充值单号</summary>
    public string? RechargeNo { get; set; }

    /// <summary>本次入账后会员可用余额</summary>
    public decimal Balance { get; set; }

    /// <summary>结果描述</summary>
    public string Message { get; set; } = string.Empty;
}

// ============================================================
// 二、余额流水 DTO
// ============================================================

/// <summary>余额流水分页查询入参（GET /api/balance/logs）</summary>
public class BalanceLogQueryDto : IPagedQuery
{
    /// <summary>会员ID</summary>
    public long? MemMemberId { get; set; }

    /// <summary>会员名称/编号模糊查询</summary>
    public string? Keyword { get; set; }

    /// <summary>变动类型：1-充值 2-下单扣减 3-退款退回 5-后台调整 7-下单冻结 8-解冻</summary>
    public int? ChangeType { get; set; }

    /// <summary>关联单据类型：0-无 1-订单 2-充值订单 3-退款单（数据库为 TINYINT）</summary>
    public int? RelatedType { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>余额流水出参（mkt_balance_log）</summary>
public class BalanceLogDto
{
    public long Id { get; set; }
    public long MemMemberId { get; set; }

    /// <summary>会员名称（批量补全）</summary>
    public string? MemMemberName { get; set; }

    /// <summary>会员编号（批量补全）</summary>
    public string? MemberNo { get; set; }

    /// <summary>变动类型：1-充值 2-下单扣减 3-退款退回 5-后台调整 7-下单冻结 8-解冻</summary>
    public int ChangeType { get; set; }

    /// <summary>变动类型中文名</summary>
    public string ChangeTypeText { get; set; } = string.Empty;

    /// <summary>变动金额（一律正数，方向由变动类型决定）</summary>
    public decimal ChangeAmount { get; set; }

    public decimal BeforeBalance { get; set; }
    public decimal AfterBalance { get; set; }

    /// <summary>关联单据类型：0-无 1-订单 2-充值订单 3-退款单</summary>
    public int? RelatedType { get; set; }

    public long? RelatedId { get; set; }
    public string? Remark { get; set; }
    public long? OperatorId { get; set; }

    /// <summary>操作人账号（批量补全）</summary>
    public string? OperatorName { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 充值单列表/详情出参（mem_recharge）
/// 【支付状态唯一事实源】前端一律以 status 字段为准展示，不允许按"点过支付按钮"臆测成功。
/// </summary>
public class RechargeDto
{
    public long Id { get; set; }
    public string RechargeNo { get; set; } = string.Empty;
    public long MemMemberId { get; set; }

    /// <summary>会员名称（批量补全）</summary>
    public string? MemMemberName { get; set; }

    /// <summary>会员编号（批量补全）</summary>
    public string? MemberNo { get; set; }

    /// <summary>充值本金</summary>
    public decimal RechargeAmount { get; set; }

    /// <summary>赠送金额</summary>
    public decimal GiftAmount { get; set; }

    /// <summary>到账金额 = 本金 + 赠送</summary>
    public decimal CreditAmount { get; set; }

    /// <summary>
    /// 支付渠道码（WECHAT / OFFLINE）。
    /// 数据库落库用的是 pay_type 数值（1-微信支付 2-线下转账），这里对外转回渠道码，保持前端契约不变。
    /// </summary>
    public string? PayChannel { get; set; }

    /// <summary>充值渠道：1-客户自助 2-后台代充（mem_recharge.recharge_channel）</summary>
    public int RechargeChannel { get; set; }

    /// <summary>微信交易号（回调写入，幂等键）</summary>
    public string? TransactionId { get; set; }

    /// <summary>状态：0-待支付 1-已支付 2-已退款 3-失败</summary>
    public int Status { get; set; }

    /// <summary>状态中文名</summary>
    public string StatusText { get; set; } = string.Empty;

    public DateTime? PayTime { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }

    /// <summary>该会员当前可用余额（详情接口返回，便于核对到账情况）</summary>
    public decimal? CurrentBalance { get; set; }
}

/// <summary>充值分页查询入参（GET /api/recharge/list）</summary>
public class RechargeQueryDto : IPagedQuery
{
    /// <summary>充值单号（模糊）</summary>
    public string? RechargeNo { get; set; }

    /// <summary>客户名称/编号（模糊，EXISTS 子查询）</summary>
    public string? Keyword { get; set; }

    /// <summary>支付渠道</summary>
    public string? PayChannel { get; set; }

    /// <summary>状态：0-待支付 1-已支付 2-已退款 3-失败</summary>
    public int? Status { get; set; }

    /// <summary>充值时间范围（按 create_time）</summary>
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// 后台代客充值入参（POST /api/recharge/admin-create）
/// 场景：客户线下转账/现金，运营在后台确认收款后代为充值，直接入账（无需支付回调）。
/// </summary>
public class AdminRechargeDto
{
    public long MemMemberId { get; set; }

    /// <summary>充值本金（&gt; 0）</summary>
    public decimal RechargeAmount { get; set; }

    /// <summary>赠送金额（≥ 0）</summary>
    public decimal GiftAmount { get; set; }

    /// <summary>线下流水号/凭证号（选填，写入 transaction_id 便于对账）</summary>
    public string? TransactionId { get; set; }

    /// <summary>备注（资金操作必须可审计）</summary>
    public string? Remark { get; set; }
}

/// <summary>充值退款入参（POST /api/recharge/{id}/refund，仅已支付可退）</summary>
public class RechargeRefundDto
{
    /// <summary>退款原因（必填，前端二次确认时收集）</summary>
    public string Reason { get; set; } = string.Empty;
}

// ============================================================
// 三、余额操作 DTO
// ============================================================

/// <summary>会员余额概况出参（GET /api/balance/{memberId}）</summary>
public class MemberBalanceDto
{
    public long MemMemberId { get; set; }
    public string? MemMemberName { get; set; }
    public string? MemberNo { get; set; }

    /// <summary>可用余额</summary>
    public decimal Balance { get; set; }

    /// <summary>冻结余额</summary>
    public decimal FrozenBalance { get; set; }

    /// <summary>累计充值总额（本金）</summary>
    public decimal TotalRecharge { get; set; }

    /// <summary>等级ID</summary>
    public long? CustomerLevelId { get; set; }

    /// <summary>等级名称</summary>
    public string? CustomerLevelName { get; set; }
}

/// <summary>后台手工调整余额入参（POST /api/balance/admin-adjust）</summary>
public class AdminAdjustBalanceDto
{
    /// <summary>会员ID</summary>
    public long MemMemberId { get; set; }

    /// <summary>
    /// 调整金额（可正可负）：正数=增加可用余额，负数=扣减可用余额；不允许为 0。
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>调整原因（必填，写入流水备注与审计）</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 下单冻结入参（POST /api/balance/freeze）
/// 事务内：balance -= 金额，frozen_balance += 金额，写冻结流水。
/// </summary>
public class FreezeBalanceDto
{
    public long MemMemberId { get; set; }

    /// <summary>冻结金额（必须 &gt; 0）</summary>
    public decimal Amount { get; set; }

    /// <summary>关联单据类型：0-无 1-订单 2-充值订单 3-退款单（数值枚举 BalanceRelatedType）</summary>
    public int? RelatedType { get; set; }

    /// <summary>关联业务ID（订单ID）</summary>
    public long? RelatedId { get; set; }

    public string? Remark { get; set; }
}

/// <summary>
/// 取消/退款解冻入参（POST /api/balance/unfreeze）
/// 事务内：frozen_balance -= 金额，balance += 金额（退回可用余额），写解冻流水。
/// </summary>
public class UnfreezeBalanceDto
{
    public long MemMemberId { get; set; }

    /// <summary>解冻金额（必须 &gt; 0）</summary>
    public decimal Amount { get; set; }

    /// <summary>关联单据类型：0-无 1-订单 2-充值订单 3-退款单（数值枚举 BalanceRelatedType）</summary>
    public int? RelatedType { get; set; }

    /// <summary>关联业务ID（订单ID / 退款单ID）</summary>
    public long? RelatedId { get; set; }

    public string? Remark { get; set; }
}

/// <summary>
/// 下单扣减入参（POST /api/balance/deduct）
/// 事务内：frozen_balance -= 金额（资金真正划走），写扣减流水。
/// 说明：下单时已冻结，支付成功时只需把冻结额销账，不再动可用余额。
/// </summary>
public class DeductBalanceDto
{
    public long MemMemberId { get; set; }

    /// <summary>扣减金额（必须 &gt; 0）</summary>
    public decimal Amount { get; set; }

    /// <summary>关联单据类型：0-无 1-订单 2-充值订单 3-退款单（数值枚举 BalanceRelatedType）</summary>
    public int? RelatedType { get; set; }

    /// <summary>关联业务ID（订单ID）</summary>
    public long? RelatedId { get; set; }

    public string? Remark { get; set; }
}
