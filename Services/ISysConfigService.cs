using Jsd.Api.Models.Common;
using Jsd.Api.Models.System;

namespace Jsd.Api.Services;

/// <summary>
/// 系统管理 - 系统配置服务接口（9 个接口的业务实现）
/// 配置统一存 sys_config，读取优先走缓存（Key 格式 config:{config_key}）。
/// </summary>
public interface ISysConfigService
{
    /// <summary>分页查询配置（名称 / 键名 / 类型 / 状态筛选）</summary>
    Task<ApiResponse<PagedResult<SysConfigDto>>> GetPagedListAsync(SysConfigQueryDto query);

    /// <summary>按 ID 获取配置详情</summary>
    Task<ApiResponse<SysConfigDetailDto?>> GetByIdAsync(long id);

    /// <summary>新增配置（校验 config_key 唯一）</summary>
    Task<ApiResponse<long>> CreateAsync(CreateSysConfigDto dto);

    /// <summary>修改配置（系统内置仅允许改配置值），成功后失效缓存</summary>
    Task<ApiResponse<bool>> UpdateAsync(UpdateSysConfigDto dto);

    /// <summary>批量删除配置（系统内置 config_type='Y' 不可删除）</summary>
    Task<ApiResponse<int>> DeleteAsync(List<long> ids);

    /// <summary>按 key 取配置值（优先读缓存）</summary>
    Task<ApiResponse<ConfigValueDto>> GetValueAsync(string configKey);

    /// <summary>按 key 列表批量取配置值（一次回源，不循环打库）</summary>
    Task<ApiResponse<List<ConfigValueDto>>> GetValuesAsync(List<string> configKeys);

    /// <summary>刷新缓存：清空全部并重新从数据库装载</summary>
    Task<ApiResponse<ConfigCacheResultDto>> RefreshCacheAsync();

    /// <summary>清空缓存</summary>
    Task<ApiResponse<ConfigCacheResultDto>> ClearCacheAsync();

    /// <summary>启动预热：一次性把全部配置装载进缓存，返回装载条数</summary>
    Task<int> PreloadAllAsync();

    /// <summary>
    /// 按 key 写入配置值（不存在则新增，存在则更新），并立即失效该 key 缓存。
    /// 供其它模块（如登录安全策略 SecurityService）复用，避免绕过缓存造成读到旧值。
    /// </summary>
    /// <param name="configKey">配置键</param>
    /// <param name="value">配置值</param>
    /// <param name="builtIn">新增时是否标记为系统内置（Y，不可删除）；已存在的记录不改动其类型</param>
    Task SetValueAsync(string configKey, string value, bool builtIn = false);
}
