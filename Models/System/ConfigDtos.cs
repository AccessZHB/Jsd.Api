namespace Jsd.Api.Models.System;

/// <summary>
/// 系统管理 - 系统配置模块 —— 按钮级权限标识常量
/// 与 sys_menu.permission、前端 v-permission 三处保持一致，改这里必须同步改另外两处。
/// </summary>
public static class SysConfigPermissions
{
    /// <summary>系统配置：列表查看</summary>
    public const string List = "system:config:list";

    /// <summary>系统配置：新增</summary>
    public const string Create = "system:config:create";

    /// <summary>系统配置：修改</summary>
    public const string Update = "system:config:update";

    /// <summary>系统配置：删除</summary>
    public const string Delete = "system:config:delete";

    /// <summary>系统配置：缓存刷新 / 清空</summary>
    public const string Cache = "system:config:cache";
}

/// <summary>系统内置配置标记（config_type）</summary>
public static class ConfigTypeConst
{
    /// <summary>系统内置：不可删除，更新时只允许改配置值</summary>
    public const string BuiltIn = "Y";

    /// <summary>自定义：可增删改</summary>
    public const string Custom = "N";

    /// <summary>把 Y/N 转成中文展示</summary>
    public static string ToText(string? type) => type == BuiltIn ? "系统内置" : "自定义";
}

/// <summary>系统配置分页查询入参（支持按名称 / 键名 / 类型 / 状态筛选）</summary>
public class SysConfigQueryDto : ISystemPagedQuery
{
    /// <summary>页码（从 1 开始）</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>配置名称（模糊）</summary>
    public string? ConfigName { get; set; }

    /// <summary>配置键名（模糊）</summary>
    public string? ConfigKey { get; set; }

    /// <summary>配置类型：Y-系统内置 N-自定义（空=全部）</summary>
    public string? ConfigType { get; set; }

    /// <summary>状态：0-正常 1-停用（空=全部）</summary>
    public int? Status { get; set; }
}

/// <summary>系统配置出参（列表用）</summary>
public class SysConfigDto
{
    /// <summary>配置ID</summary>
    public long Id { get; set; }

    /// <summary>配置名称</summary>
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>配置键名</summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>配置值（统一字符串，调用方自行转换类型）</summary>
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>配置类型：Y-系统内置 N-自定义</summary>
    public string ConfigType { get; set; } = ConfigTypeConst.Custom;

    /// <summary>配置类型中文（系统内置 / 自定义）</summary>
    public string ConfigTypeText { get; set; } = string.Empty;

    /// <summary>排序</summary>
    public int SortOrder { get; set; }

    /// <summary>状态：0-正常 1-停用</summary>
    public int Status { get; set; }

    /// <summary>状态中文（正常 / 停用）</summary>
    public string StatusText { get; set; } = string.Empty;

    /// <summary>是否系统内置（true 时不可删除、只能改值）</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>创建者账号</summary>
    public string CreateBy { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新者账号</summary>
    public string UpdateBy { get; set; } = string.Empty;

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>系统配置详情出参（与列表同构，单独留出便于后续扩展大字段）</summary>
public class SysConfigDetailDto : SysConfigDto
{
}

/// <summary>新增系统配置入参</summary>
public class CreateSysConfigDto
{
    /// <summary>配置名称</summary>
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>配置键名（唯一，命名规范：模块.功能.参数）</summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>配置值</summary>
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>配置类型：Y-系统内置 N-自定义（默认 N）</summary>
    public string ConfigType { get; set; } = ConfigTypeConst.Custom;

    /// <summary>排序</summary>
    public int SortOrder { get; set; }

    /// <summary>状态：0-正常 1-停用</summary>
    public int Status { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 修改系统配置入参
/// ⚠️ 系统内置（config_type='Y'）记录 Service 层只放行 ConfigValue，
///    其余字段即使传了也会被忽略（不报错），详见 SysConfigService.UpdateAsync。
/// </summary>
public class UpdateSysConfigDto
{
    /// <summary>配置ID</summary>
    public long Id { get; set; }

    /// <summary>配置名称</summary>
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>配置键名（系统内置配置不允许修改）</summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>配置值</summary>
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>排序</summary>
    public int SortOrder { get; set; }

    /// <summary>状态：0-正常 1-停用</summary>
    public int Status { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>按 key 取配置值出参</summary>
public class ConfigValueDto
{
    /// <summary>配置键名</summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>配置值（key 不存在时返回空字符串，而不是 null，调用方少一层判空）</summary>
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>状态：0-正常 1-停用（停用仅作提示，不影响取值）</summary>
    public int Status { get; set; }

    /// <summary>是否命中（false 表示库中不存在该 key）</summary>
    public bool Exists { get; set; }
}

/// <summary>批量获取配置值入参</summary>
public class ConfigKeysDto
{
    /// <summary>配置键名列表（空数组返回空列表）</summary>
    public List<string> Keys { get; set; } = new();
}

/// <summary>
/// 配置缓存项（缓存里存的不是裸字符串，而是「值 + 状态」）
/// —— 只存值时缓存命中会丢失 status / 是否存在的信息，取值接口就没法回传字段了。
/// </summary>
public class ConfigCacheItem
{
    /// <summary>配置值（key 不存在时为 null，用来区分「没这条配置」和「配置值是空串」）</summary>
    public string? Value { get; set; }

    /// <summary>状态：0-正常 1-停用</summary>
    public int Status { get; set; }

    /// <summary>是否命中数据库（false = 库中不存在该 key，空值占位）</summary>
    public bool Exists { get; set; }
}

/// <summary>缓存操作结果出参（刷新 / 清空共用）</summary>
public class ConfigCacheResultDto
{
    /// <summary>动作：refresh / clear</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>影响条数（refresh=重新载入缓存的配置数，clear=0）</summary>
    public int Count { get; set; }

    /// <summary>操作时间</summary>
    public DateTime OperateTime { get; set; }
}
