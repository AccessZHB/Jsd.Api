namespace Jsd.Api.Models.Dict;

/// <summary>
/// 字典管理模块 —— 按钮级权限标识常量
/// 与 sys_menu.permission、前端 v-permission 三处保持一致，改这里必须同步改另外两处。
/// </summary>
public static class DictPermissions
{
    /// <summary>字典类型：列表查看</summary>
    public const string TypeList = "dict:type:list";

    /// <summary>字典类型：新增</summary>
    public const string TypeCreate = "dict:type:create";

    /// <summary>字典类型：修改</summary>
    public const string TypeUpdate = "dict:type:update";

    /// <summary>字典类型：删除</summary>
    public const string TypeDelete = "dict:type:delete";

    /// <summary>字典数据：列表查看</summary>
    public const string DataList = "dict:data:list";

    /// <summary>字典数据：新增</summary>
    public const string DataCreate = "dict:data:create";

    /// <summary>字典数据：修改</summary>
    public const string DataUpdate = "dict:data:update";

    /// <summary>字典数据：删除</summary>
    public const string DataDelete = "dict:data:delete";

    /// <summary>字典缓存：清空 / 刷新</summary>
    public const string CacheClear = "dict:cache:clear";
}

/// <summary>字典类型 —— 分页查询条件</summary>
public class DictTypeQueryDto
{
    /// <summary>字典名称模糊匹配</summary>
    public string? DictName { get; set; }

    /// <summary>字典类型编码模糊匹配</summary>
    public string? DictType { get; set; }

    /// <summary>状态：0-正常 1-停用（不传=全部）</summary>
    public int? Status { get; set; }

    /// <summary>页码，从 1 开始</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页条数，最大 200</summary>
    public int PageSize { get; set; } = 10;
}

/// <summary>字典类型 —— 列表/详情出参</summary>
public class DictTypeDto
{
    public long Id { get; set; }

    /// <summary>字典名称</summary>
    public string DictName { get; set; } = string.Empty;

    /// <summary>字典类型编码（全局唯一）</summary>
    public string DictType { get; set; } = string.Empty;

    /// <summary>状态：0-正常 1-停用</summary>
    public int Status { get; set; }

    /// <summary>状态中文文案（0=正常 1=停用），前端可直接展示不必再映射</summary>
    public string StatusText { get; set; } = string.Empty;

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建人账号</summary>
    public string CreateBy { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }

    /// <summary>更新人账号</summary>
    public string UpdateBy { get; set; } = string.Empty;

    public DateTime? UpdateTime { get; set; }

    /// <summary>该类型下字典数据条数（列表页展示，不分页统计）</summary>
    public int DataCount { get; set; }
}

/// <summary>字典类型下拉选项（只返回启用状态）</summary>
public class DictTypeOptionDto
{
    /// <summary>下拉值，取 dictType 编码</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>下拉文案，取 dictName</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>数据库主键（前端需要时可用）</summary>
    public long Id { get; set; }
}

/// <summary>新增字典类型</summary>
public class CreateDictTypeDto
{
    /// <summary>字典名称（如：订单状态）</summary>
    public string DictName { get; set; } = string.Empty;

    /// <summary>字典类型编码，全局唯一（如：sys_order_status）</summary>
    public string DictType { get; set; } = string.Empty;

    /// <summary>状态：0-正常 1-停用</summary>
    public int Status { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>修改字典类型（id 由路径或 body 传入，这里放在 body）</summary>
public class UpdateDictTypeDto
{
    public long Id { get; set; }

    public string DictName { get; set; } = string.Empty;

    public string DictType { get; set; } = string.Empty;

    public int Status { get; set; }

    public string? Remark { get; set; }
}

/// <summary>启用/停用（字典类型与字典数据通用）</summary>
public class DictStatusDto
{
    /// <summary>目标状态：0-正常（启用） 1-停用</summary>
    public int Status { get; set; }
}

/// <summary>字典数据 —— 分页查询条件</summary>
public class DictDataQueryDto
{
    /// <summary>字典类型ID（与 dictType 二选一，优先用 ID）</summary>
    public long? DictTypeId { get; set; }

    /// <summary>字典类型编码精确匹配（如 sys_order_status）</summary>
    public string? DictTypeCode { get; set; }

    /// <summary>字典标签模糊匹配</summary>
    public string? DictLabel { get; set; }

    /// <summary>状态：0-正常 1-停用（不传=全部）</summary>
    public int? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}

/// <summary>字典数据 —— 列表/详情出参</summary>
public class DictDataDto
{
    public long Id { get; set; }

    /// <summary>所属字典类型ID</summary>
    public long DictTypeId { get; set; }

    /// <summary>字典标签</summary>
    public string DictLabel { get; set; } = string.Empty;

    /// <summary>字典键值</summary>
    public string DictValue { get; set; } = string.Empty;

    /// <summary>排序权重，越小越靠前</summary>
    public int DictSort { get; set; }

    /// <summary>CSS 样式类</summary>
    public string CssClass { get; set; } = string.Empty;

    /// <summary>是否默认选中</summary>
    public bool IsDefault { get; set; }

    /// <summary>状态：0-正常 1-停用</summary>
    public int Status { get; set; }

    /// <summary>状态中文文案</summary>
    public string StatusText { get; set; } = string.Empty;

    public string? Remark { get; set; }

    public string CreateBy { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }

    public string UpdateBy { get; set; } = string.Empty;

    public DateTime? UpdateTime { get; set; }
}

/// <summary>新增字典数据</summary>
public class CreateDictDataDto
{
    /// <summary>所属字典类型ID</summary>
    public long DictTypeId { get; set; }

    public string DictLabel { get; set; } = string.Empty;

    public string DictValue { get; set; } = string.Empty;

    /// <summary>排序权重，默认 0</summary>
    public int DictSort { get; set; }

    /// <summary>CSS 样式类</summary>
    public string? CssClass { get; set; }

    /// <summary>是否默认选中（同一类型下最多一条为 true）</summary>
    public bool IsDefault { get; set; }

    /// <summary>状态：0-正常 1-停用</summary>
    public int Status { get; set; }

    public string? Remark { get; set; }
}

/// <summary>修改字典数据</summary>
public class UpdateDictDataDto
{
    public long Id { get; set; }

    public long DictTypeId { get; set; }

    public string DictLabel { get; set; } = string.Empty;

    public string DictValue { get; set; } = string.Empty;

    public int DictSort { get; set; }

    public string? CssClass { get; set; }

    public bool IsDefault { get; set; }

    public int Status { get; set; }

    public string? Remark { get; set; }
}

/// <summary>
/// 字典数据项 —— 缓存中存放的最小单元，
/// 同时用于 GET /api/dict/data/type/{dictType} 的核心缓存接口出参。
/// </summary>
public class DictDataItem
{
    /// <summary>字典键值（option.value）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>字典标签（option.label）</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>排序权重</summary>
    public int Sort { get; set; }

    /// <summary>是否默认选中</summary>
    public bool IsDefault { get; set; }

    /// <summary>CSS 样式类</summary>
    public string CssClass { get; set; } = string.Empty;
}

/// <summary>刷新/清空缓存的返回信息</summary>
public class DictCacheResultDto
{
    /// <summary>操作类型：clear=清空全部，refresh=刷新单个</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>涉及的类型编码（清空全部时为空）</summary>
    public string? DictType { get; set; }

    /// <summary>影响的字典数据条数</summary>
    public int Count { get; set; }
}
