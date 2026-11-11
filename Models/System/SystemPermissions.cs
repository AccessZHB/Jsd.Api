namespace Jsd.Api.Models.System;

/// <summary>
/// 系统管理模块权限标识（安全策略 / 系统配置 / 定时任务 / 操作日志 / 登录日志 / 字典缓存）。
///
/// ⚠️ 权限码必须与数据库 sys_menu.permission 完全一致，否则非超管角色一律 403。
///    本文件中的常量已按现网 sys_menu 的实际取值对齐（见 sql/scripts/system_security_permission_sync.sql）。
/// </summary>
public static class SystemPermissions
{
    // ---------- 登录安全策略（菜单 804：system:security:list）----------

    /// <summary>登录安全策略 - 读取完整配置（原先匿名，现需权限）</summary>
    public const string SecurityList = "system:security:list";

    /// <summary>登录安全策略 - 修改（高危，仅超管）</summary>
    public const string SecurityUpdate = "system:security:update";

    // ---------- 操作日志（菜单 802）----------

    public const string OperationLogList = "system:operation:list";
    public const string OperationLogExport = "system:operation:export";
    public const string OperationLogRemove = "system:operation:remove";
    public const string OperationLogClean = "system:operation:clean";

    // ---------- 登录日志（菜单 803）与账号锁定（菜单 805）----------

    public const string LoginLogList = "system:loginlog:list";
    public const string LoginLogExport = "system:loginlog:export";
    public const string LoginLogRemove = "system:loginlog:remove";
    public const string LoginLogClean = "system:loginlog:clean";

    /// <summary>已锁定账号列表</summary>
    public const string LockList = "system:lock:list";

    /// <summary>手动解锁账号（可绕过失败锁定策略，高危）</summary>
    public const string AccountUnlock = "system:lock:unlock";

    // ---------- 字典缓存（菜单 903：dict:cache:clear）----------

    public const string DictCacheClear = "dict:cache:clear";
    public const string DictCacheRefresh = "dict:cache:refresh";

    // ---------- 系统配置（菜单 811）----------

    public const string ConfigList = "system:config:list";
    public const string ConfigAdd = "system:config:create";
    public const string ConfigEdit = "system:config:update";
    public const string ConfigRemove = "system:config:delete";
    public const string ConfigCache = "system:config:cache";

    // ---------- 定时任务 ----------

    public const string JobList = "system:job:list";
    public const string JobAdd = "system:job:add";
    public const string JobEdit = "system:job:edit";
    public const string JobRemove = "system:job:remove";
    public const string JobRun = "system:job:run";
}
