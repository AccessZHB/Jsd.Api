namespace Jsd.Api.Models.Price;

/// <summary>
/// 价格策略与会员余额模块 —— 状态/类型枚举与展示映射（单一数据源，Service/Controller/前端文案共用）
/// </summary>

/// <summary>
/// 策略类型（price_strategy.strategy_type）
/// </summary>
public enum StrategyType
{
    /// <summary>1-客户等级价（按 mem_member.customer_level_id 匹配）</summary>
    MemberLevel = 1,
    /// <summary>2-客户专属价（按 price_rule.mem_member_id 匹配）</summary>
    MemberExclusive = 2,
    /// <summary>3-批量阶梯价（按 price_rule_item 数量区间匹配）</summary>
    Tiered = 3,
    /// <summary>4-促销价（限时活动价）</summary>
    Promotion = 4
}

/// <summary>
/// 策略状态（price_strategy.status）
/// </summary>
public enum StrategyStatus
{
    /// <summary>0-草稿（仅草稿可改基本信息）</summary>
    Draft = 0,
    /// <summary>1-启用（参与取价）</summary>
    Enabled = 1,
    /// <summary>2-停用（不参与取价）</summary>
    Disabled = 2
}

/// <summary>
/// 规则适用范围（price_rule.apply_scope）
/// </summary>
public enum ApplyScope
{
    /// <summary>1-全部商品</summary>
    All = 1,
    /// <summary>2-指定分类</summary>
    Category = 2,
    /// <summary>3-指定商品</summary>
    Material = 3
}

/// <summary>
/// 价格计算方式（price_rule.calc_type）
/// </summary>
public enum CalcType
{
    /// <summary>1-固定价（直接取 calc_value 作为单价）</summary>
    Fixed = 1,
    /// <summary>2-折扣率（标准售价 × calc_value）</summary>
    Discount = 2,
    /// <summary>3-减免金额（标准售价 - calc_value）</summary>
    Reduce = 3
}

/// <summary>
/// 价格变更操作类型（price_change_log.operation_type）
/// </summary>
public enum PriceOperationType
{
    /// <summary>1-新增</summary>
    Create = 1,
    /// <summary>2-修改</summary>
    Update = 2,
    /// <summary>3-停用</summary>
    Disable = 3,
    /// <summary>4-删除</summary>
    Delete = 4
}

/// <summary>
/// 充值单状态（mem_recharge 的 pay_status 列）
///
/// ⚠️ 取值语义以 Jsd_order.sql 建表注释为准，与需求 Word 文档不同：
///    文档写 0待支付/1已支付/2已退款/3失败，数据库是 0待支付/1已到账/2已关闭/3已退款。
///    数据库 2=已关闭（支付失败或超时关闭，不入账），3=已退款。
/// </summary>
public enum RechargeStatus
{
    /// <summary>0-待支付</summary>
    Pending = 0,
    /// <summary>1-已到账（已入账）</summary>
    Paid = 1,
    /// <summary>2-已关闭（支付失败 / 超时关闭，未入账）</summary>
    Closed = 2,
    /// <summary>3-已退款（余额已扣回）</summary>
    Refunded = 3
}

/// <summary>充值支付方式（mem_recharge 的 pay_type 列）</summary>
public enum PayType
{
    /// <summary>1-微信支付</summary>
    Wechat = 1,
    /// <summary>2-线下转账（后台代客充值）</summary>
    Offline = 2
}

/// <summary>充值渠道（mem_recharge 的 recharge_channel 列）：区分谁发起的充值</summary>
public enum RechargeChannel
{
    /// <summary>1-小程序 / 客户自助</summary>
    Self = 1,
    /// <summary>2-后台代充</summary>
    Admin = 2
}

/// <summary>余额流水关联单据类型（mkt_balance_log 的 related_type 列，TINYINT）</summary>
public enum BalanceRelatedType
{
    /// <summary>0-无</summary>
    None = 0,
    /// <summary>1-订单</summary>
    Order = 1,
    /// <summary>2-充值订单</summary>
    Recharge = 2,
    /// <summary>3-退款单</summary>
    Refund = 3
}

/// <summary>
/// 余额变动类型（mkt_balance_log.change_type）
/// change_amount 一律记正数，方向由本枚举语义决定。
///
/// ⚠️ 1~6 沿用 Jsd_order.sql 建表注释（4-提现 5-后台调整 6-赠送），
///    "先充值后下单"新增的【冻结 / 解冻】没有现成取值，扩展为 7 / 8，避免与既有语义冲突。
/// </summary>
public enum BalanceChangeType
{
    /// <summary>1-充值（balance 增加）</summary>
    Recharge = 1,
    /// <summary>2-下单扣减（frozen_balance 减少，资金真正划走）</summary>
    OrderDeduct = 2,
    /// <summary>3-退款退回（balance 增加）</summary>
    RefundBack = 3,
    /// <summary>4-提现（本系统暂未启用）</summary>
    Withdraw = 4,
    /// <summary>5-后台调整（人工增减可用余额）</summary>
    AdminAdjust = 5,
    /// <summary>6-赠送（本系统暂未启用）</summary>
    Gift = 6,
    /// <summary>7-下单冻结（balance 减少，frozen_balance 增加）</summary>
    Freeze = 7,
    /// <summary>8-解冻（frozen_balance 减少，balance 增加）</summary>
    Unfreeze = 8
}

/// <summary>支付方式 → 中文名 / 渠道码（保持前端 DTO 的 payChannel 字符串契约不变）</summary>
public static class PayTypeHelper
{
    public static string GetName(int payType) => payType switch
    {
        (int)PayType.Wechat => "微信支付",
        (int)PayType.Offline => "线下转账",
        _ => "未知"
    };

    /// <summary>数值 → 对外渠道码（WECHAT / OFFLINE）</summary>
    public static string GetCode(int payType) => payType switch
    {
        (int)PayType.Wechat => "WECHAT",
        (int)PayType.Offline => "OFFLINE",
        _ => "OTHER"
    };

    /// <summary>渠道码 → 数值（未知统一按线下转账处理）</summary>
    public static int FromCode(string? code) =>
        string.Equals(code, "WECHAT", StringComparison.OrdinalIgnoreCase)
            ? (int)PayType.Wechat
            : (int)PayType.Offline;
}

/// <summary>
/// 余额操作对象：决定冻结/解冻/扣减作用于「可用余额」还是「冻结余额」
/// </summary>
public enum BalanceField
{
    /// <summary>可用余额（mem_member.balance）</summary>
    Available = 0,
    /// <summary>冻结余额（mem_member.frozen_balance）</summary>
    Frozen = 1
}

/// <summary>策略类型 → 中文名</summary>
public static class StrategyTypeHelper
{
    public static string GetName(int type) => type switch
    {
        (int)StrategyType.MemberLevel => "客户等级价",
        (int)StrategyType.MemberExclusive => "客户专属价",
        (int)StrategyType.Tiered => "批量阶梯价",
        (int)StrategyType.Promotion => "促销价",
        _ => "未知"
    };
}

/// <summary>策略状态 → 中文名</summary>
public static class StrategyStatusHelper
{
    public static string GetName(int status) => status switch
    {
        (int)StrategyStatus.Draft => "草稿",
        (int)StrategyStatus.Enabled => "启用",
        (int)StrategyStatus.Disabled => "停用",
        _ => "未知"
    };
}

/// <summary>适用范围 → 中文名</summary>
public static class ApplyScopeHelper
{
    public static string GetName(int scope) => scope switch
    {
        (int)ApplyScope.All => "全部商品",
        (int)ApplyScope.Category => "指定分类",
        (int)ApplyScope.Material => "指定商品",
        _ => "未知"
    };
}

/// <summary>计算方式 → 中文名</summary>
public static class CalcTypeHelper
{
    public static string GetName(int calcType) => calcType switch
    {
        (int)CalcType.Fixed => "固定价",
        (int)CalcType.Discount => "折扣率",
        (int)CalcType.Reduce => "减免金额",
        _ => "未知"
    };
}

/// <summary>价格变更操作类型 → 中文名</summary>
public static class PriceOperationTypeHelper
{
    public static string GetName(int type) => type switch
    {
        (int)PriceOperationType.Create => "新增",
        (int)PriceOperationType.Update => "修改",
        (int)PriceOperationType.Disable => "停用",
        (int)PriceOperationType.Delete => "删除",
        _ => "未知"
    };
}

/// <summary>充值单状态 → 中文名</summary>
public static class RechargeStatusHelper
{
    public static string GetName(int status) => status switch
    {
        (int)RechargeStatus.Pending => "待支付",
        (int)RechargeStatus.Paid => "已到账",
        (int)RechargeStatus.Closed => "已关闭",
        (int)RechargeStatus.Refunded => "已退款",
        _ => "未知"
    };
}

/// <summary>余额变动类型 → 中文名</summary>
public static class BalanceChangeTypeHelper
{
    public static string GetName(int type) => type switch
    {
        (int)BalanceChangeType.Recharge => "充值",
        (int)BalanceChangeType.OrderDeduct => "下单扣减",
        (int)BalanceChangeType.RefundBack => "退款退回",
        (int)BalanceChangeType.Withdraw => "提现",
        (int)BalanceChangeType.AdminAdjust => "后台调整",
        (int)BalanceChangeType.Gift => "赠送",
        (int)BalanceChangeType.Freeze => "下单冻结",
        (int)BalanceChangeType.Unfreeze => "解冻",
        _ => "未知"
    };
}

/// <summary>
/// 取价优先级（业务强制顺序，不可跳级）：
///   客户专属价(2) &gt; 促销价(4) &gt; 客户等级价(1) &gt; 批量阶梯价(3) &gt; 商品标准售价(0)
/// </summary>
public static class StrategyPriority
{
    /// <summary>按取价优先级从高到低返回策略类型序列</summary>
    public static readonly int[] Order =
    [
        (int)StrategyType.MemberExclusive,
        (int)StrategyType.Promotion,
        (int)StrategyType.MemberLevel,
        (int)StrategyType.Tiered
    ];

    /// <summary>未命中任何策略时的兜底类型值（写快照用）</summary>
    public const int None = 0;
}

/// <summary>
/// 价格策略与会员余额模块权限标识
///
/// ⚠️ 与《价格策略与会员余额模块前端Vue开发AI提示词》第四章严格对齐：
///   前端按钮用 v-permission 绑定这些常量，后端菜单种子 sys_menu.permission 也用同一套字符串，
///   三者必须完全一致，否则按钮会被权限指令隐藏。
/// </summary>
public static class PricePermissions
{
    // ---------- 客户等级 ----------
    public const string MemberLevelList = "price:level:list";
    public const string MemberLevelAdd = "price:level:add";
    public const string MemberLevelEdit = "price:level:edit";
    public const string MemberLevelStatus = "price:level:status";

    // ---------- 价格策略 ----------
    public const string StrategyList = "price:strategy:list";
    public const string StrategyAdd = "price:strategy:add";
    public const string StrategyEdit = "price:strategy:edit";
    public const string StrategySubmit = "price:strategy:submit";
    public const string StrategyDisable = "price:strategy:disable";
    public const string StrategyDelete = "price:strategy:delete";
    public const string RuleBatchSave = "price:strategy:rule:save";

    // ---------- 取价 / 变更日志 / 快照 ----------
    public const string Quotation = "price:quotation";
    public const string ChangeLogList = "price:change-log:list";
    public const string SnapshotList = "price:snapshot:list";
    public const string SnapshotWrite = "price:snapshot:write";

    // ---------- 会员余额 ----------
    public const string RechargeList = "member:recharge:list";
    public const string RechargeAudit = "member:recharge:audit";     // 后台代客充值
    public const string RechargeRefund = "member:recharge:refund";   // 充值退款
    public const string RechargeNotify = "member:recharge:notify";   // 支付回调（内部）
    public const string RechargeCreate = "member:recharge:create";   // 创建充值单

    public const string BalanceLogList = "member:balance-log:list";
    public const string BalanceAdjust = "member:balance-log:adjust"; // 后台调整余额
    public const string BalanceFreeze = "member:balance:freeze";
    public const string BalanceUnfreeze = "member:balance:unfreeze";
    public const string BalanceDeduct = "member:balance:deduct";
}
