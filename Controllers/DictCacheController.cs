using Jsd.Api.Attributes;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Dict;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 字典缓存接口（字典管理模块 C 组，共 2 个）
///
/// 字典数据变更后服务层已自动失效缓存，这里提供两个手动维护入口：
///   GET /api/dict/cache/clear            清空全部字典缓存
///   GET /api/dict/cache/reflesh/{type}   刷新（重建）指定字典类型的缓存
///
/// 文档里 reflesh 是拼写笔误，这里同时支持正确拼写的 refresh/{dictType}，两个都能用。
/// </summary>
[ApiController]
[Route("api/dict/cache")]
[Authorize]
public class DictCacheController : ControllerBase
{
    private readonly IDictCacheService _dictCacheService;

    public DictCacheController(IDictCacheService dictCacheService)
    {
        _dictCacheService = dictCacheService;
    }

    /// <summary>清空全部字典缓存</summary>
    /// <remarks>GET /api/dict/cache/clear</remarks>
    [HttpGet("clear")]
    [OperationLog("字典管理", "清空字典缓存")]
    public async Task<ApiResponse<DictCacheResultDto>> ClearAll()
        => await _dictCacheService.ClearAllAsync();

    /// <summary>刷新指定字典类型缓存（重新从数据库加载并回写）</summary>
    /// <remarks>GET /api/dict/cache/reflesh/sys_order_status</remarks>
    [HttpGet("reflesh/{dictType}")]
    [HttpGet("refresh/{dictType}")]
    [OperationLog("字典管理", "刷新字典缓存")]
    public async Task<ApiResponse<DictCacheResultDto>> Refresh(string dictType)
        => await _dictCacheService.RefreshAsync(dictType);
}
