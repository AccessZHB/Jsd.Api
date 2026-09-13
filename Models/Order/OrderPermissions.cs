namespace Jsd.Api.Models.Order;

/// <summary>
/// 订单管理权限标识常量（与 sys_menu.permission 一一对应）。
/// 本项目后端只挂 [Authorize] 做登录校验，按钮级权限由前端 v-permission 指令消费这些标识。
///
/// 说明：Jsd_order.sql 中已存在 订单管理目录(3)、订单列表(300, order:list)、发货(30000, order:ship)
/// 三个种子数据，本常量与其保持一致，并补齐其余操作权限。
/// </summary>
public static class OrderPermissions
{
    /// <summary>订单列表（分页查询）</summary>
    public const string List = "order:list";

    /// <summary>订单详情</summary>
    public const string Detail = "order:detail";

    /// <summary>修改订单备注</summary>
    public const string Remark = "order:remark";

    /// <summary>手动发货</summary>
    public const string Ship = "order:ship";

    /// <summary>订单导出</summary>
    public const string Export = "order:export";

    /// <summary>创建订单</summary>
    public const string Create = "order:create";

    /// <summary>订单支付（触发扣减库存）</summary>
    public const string Pay = "order:pay";

    /// <summary>确认完成（触发增加销量）</summary>
    public const string Complete = "order:complete";

    /// <summary>关闭订单</summary>
    public const string Cancel = "order:cancel";
}
