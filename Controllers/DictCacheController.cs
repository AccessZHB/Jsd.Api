using Jsd.Api.Attributes;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Dict;
using Jsd.Api.Models.System;
using Jsd.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jsd.Api.Controllers;

/// <summary>
/// 字典缓存接口（字典管理模块 C 组）
///
/// 字典数据变更后服务层已自动失效缓存，这里提供两个手动维护入口：
///   POST /api/dict/cache/clear            清空全部字典缓存
///   POST /api/dict/cache/refresh/{type}   刷新（重建）指定字典类型的缓存
///
/// 【P2-15 修复】原为 GET —— GET 会被浏览器预取/代理缓存/爬虫抓取，
/// 一旦被缓存则"刷新缓存"永远不生效，且存在跨站触发风险。现统一改为 POST。
/// 同时删除历史拼写错误的路由 reflesh（与 refresh 重复，形成双份攻击面）。
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

    /// <summary>清空全部字典缓存（权限：system:dict:cache:clear）</summary>
    /// <remarks>POST /api/dict/cache/clear</remarks>
    [HttpPost("clear")]
    [Permission(SystemPermissions.DictCacheClear)]
    [OperationLog("字典管理", "清空字典缓存")]
    public async Task<ApiResponse<DictCacheResultDto>> ClearAll()
        => await _dictCacheService.ClearAllAsync();

    /// <summary>刷新指定字典类型缓存（重新从数据库加载并回写；权限：system:dict:cache:refresh）</summary>
    /// <remarks>POST /api/dict/cache/refresh/sys_order_status</remarks>
    [HttpPost("refresh/{dictType}")]
    [Permission(SystemPermissions.DictCacheRefresh)]
    [OperationLog("字典管理", "刷新字典缓存")]
    public async Task<ApiResponse<DictCacheResultDto>> Refresh(string dictType)
        => await _dictCacheService.RefreshAsync(dictType);
}
