using AutoMapper;
using FluentValidation;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.System;
using Jsd.Api.Repositories;
using Jsd.Api.Validators;

namespace Jsd.Api.Services;

/// <summary>
/// 系统管理 - 系统配置服务实现。
///
/// 核心设计（与需求文档一致）：
///   1) 缓存优先：Key 为 config:{config_key}，正常值永不过期，靠写操作显式失效；
///      库中不存在的 key 写入 1 分钟空占位，防缓存穿透。
///   2) 系统内置保护：config_type='Y' 禁止删除；修改时只允许改 config_value，
///      其余字段（含 config_key / config_type）忽略传入值，保持原样。
///   3) 状态语义：status 0-正常 1-停用，只用于列表筛选与展示，
///      【不参与取值判断】——否则停用 security.login 会让验证码/锁定阈值静默失效，
///      造成安全策略降级，风险远大于收益。取值接口会同时回传 status，由调用方自行决定。
///   4) 刷新缓存用 SemaphoreSlim 串行化，避免并发刷新重复装载。
/// </summary>
public class SysConfigService : ISysConfigService
{
    private readonly ISysConfigRepository _repository;
    private readonly SysConfigCacheHelper _cache;
    private readonly CurrentUserService _currentUser;
    private readonly IMapper _mapper;

    /// <summary>刷新缓存的并发锁（同一时刻只允许一个刷新在跑）</summary>
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    public SysConfigService(
        ISysConfigRepository repository,
        SysConfigCacheHelper cache,
        CurrentUserService currentUser,
        IMapper mapper)
    {
        _repository = repository;
        _cache = cache;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<PagedResult<SysConfigDto>>> GetPagedListAsync(SysConfigQueryDto query)
    {
        var (items, total) = await _repository.GetPagedListAsync(query);
        var list = items.Select(ToDto).ToList();

        return ApiResponse<PagedResult<SysConfigDto>>.Success(
            PagedResult<SysConfigDto>.Create(list, total, query.Page < 1 ? 1 : query.Page, query.PageSize < 1 ? 10 : query.PageSize));
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<SysConfigDetailDto?>> GetByIdAsync(long id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null) return ApiResponse<SysConfigDetailDto?>.Fail("配置不存在", 404);

        return ApiResponse<SysConfigDetailDto?>.Success(_mapper.Map<SysConfigDetailDto>(entity));
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<long>> CreateAsync(CreateSysConfigDto dto)
    {
        // FluentValidation：Controller 捕获 ValidationException 后转 ApiResponse.Fail
        new CreateSysConfigValidator().ValidateAndThrow(dto);

        dto.ConfigName = dto.ConfigName.Trim();
        dto.ConfigKey = dto.ConfigKey.Trim();

        if (await _repository.ExistsAsync(dto.ConfigKey, 0))
        {
            return ApiResponse<long>.Fail($"配置键名 [{dto.ConfigKey}] 已存在");
        }

        var entity = _mapper.Map<SysConfig>(dto);
        entity.ConfigType = NormalizeType(dto.ConfigType);
        entity.Status = dto.Status == 1 ? 1 : 0;
        entity.CreateBy = _currentUser.UserName;
        entity.CreateTime = DateTime.Now;

        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();

        // 新增即写入缓存，避免新配置第一次读取还要回源
        await _cache.SetAsync(entity.ConfigKey, new ConfigCacheItem
        {
            Value = entity.ConfigValue,
            Status = entity.Status,
            Exists = true
        });

        return ApiResponse<long>.Success(entity.Id, "新增成功");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> UpdateAsync(UpdateSysConfigDto dto)
    {
        new UpdateSysConfigValidator().ValidateAndThrow(dto);

        dto.ConfigName = dto.ConfigName.Trim();
        dto.ConfigKey = dto.ConfigKey.Trim();

        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null) return ApiResponse<bool>.Fail("配置不存在", 404);

        var oldKey = entity.ConfigKey;
        var isBuiltIn = entity.ConfigType == ConfigTypeConst.BuiltIn;

        if (isBuiltIn)
        {
            // 系统内置：只允许改配置值，其余字段一律保持原样（传入值静默忽略）
            entity.ConfigValue = dto.ConfigValue;
        }
        else
        {
            // 自定义：键名被改过要先校验唯一性，避免撞到别人的 key
            if (!string.Equals(oldKey, dto.ConfigKey, StringComparison.Ordinal) &&
                await _repository.ExistsAsync(dto.ConfigKey, dto.Id))
            {
                return ApiResponse<bool>.Fail($"配置键名 [{dto.ConfigKey}] 已存在");
            }

            entity.ConfigName = dto.ConfigName;
            entity.ConfigKey = dto.ConfigKey;
            entity.ConfigValue = dto.ConfigValue;
            entity.SortOrder = dto.SortOrder;
            entity.Status = dto.Status == 1 ? 1 : 0;
            entity.Remark = dto.Remark;
        }

        entity.UpdateBy = _currentUser.UserName;
        entity.UpdateTime = DateTime.Now;

        await _repository.SaveChangesAsync();

        // 先落库再失效缓存（文档避坑点：顺序反了会出现缓存与库不一致）
        await _cache.RemoveAsync(oldKey);
        if (!string.Equals(oldKey, entity.ConfigKey, StringComparison.Ordinal))
        {
            await _cache.RemoveAsync(entity.ConfigKey);
        }

        return ApiResponse<bool>.Success(true, "修改成功");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<int>> DeleteAsync(List<long> ids)
    {
        if (ids == null || ids.Count == 0) return ApiResponse<int>.Fail("请选择要删除的配置");

        var targets = new List<SysConfig>();
        foreach (var id in ids.Distinct())
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null) continue;

            // 系统内置保护：只要命中一条内置就整批拒绝，避免"删了一半"的半成品状态
            if (entity.ConfigType == ConfigTypeConst.BuiltIn)
            {
                return ApiResponse<int>.Fail($"配置 [{entity.ConfigKey}] 为系统内置配置，不可删除");
            }

            targets.Add(entity);
        }

        if (targets.Count == 0) return ApiResponse<int>.Fail("配置不存在", 404);

        var keys = targets.Select(t => t.ConfigKey).ToList();

        foreach (var entity in targets)
        {
            _repository.Remove(entity);
        }

        await _repository.SaveChangesAsync();

        // 提交成功后再清缓存
        foreach (var key in keys)
        {
            await _cache.RemoveAsync(key);
        }

        return ApiResponse<int>.Success(targets.Count, $"已删除 {targets.Count} 条配置");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<ConfigValueDto>> GetValueAsync(string configKey)
    {
        var item = await _cache.GetAsync(configKey);
        if (item != null)
        {
            return ApiResponse<ConfigValueDto>.Success(new ConfigValueDto
            {
                ConfigKey = configKey,
                ConfigValue = item.Value ?? string.Empty,
                Status = item.Status,
                Exists = item.Exists
            });
        }

        // 缓存未命中：回源数据库并回填（不存在的 key 也回填空占位，防穿透）
        var entity = await _repository.GetByKeyAsync(configKey);
        await _cache.SetAsync(configKey, new ConfigCacheItem
        {
            Value = entity?.ConfigValue,
            Status = entity?.Status ?? 0,
            Exists = entity != null
        });

        return ApiResponse<ConfigValueDto>.Success(new ConfigValueDto
        {
            ConfigKey = configKey,
            ConfigValue = entity?.ConfigValue ?? string.Empty,
            Status = entity?.Status ?? 0,
            Exists = entity != null
        });
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<List<ConfigValueDto>>> GetValuesAsync(List<string> configKeys)
    {
        var keys = (configKeys ?? new List<string>())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct()
            .ToList();

        if (keys.Count == 0) return ApiResponse<List<ConfigValueDto>>.Success(new List<ConfigValueDto>());

        var result = new List<ConfigValueDto>();
        var missKeys = new List<string>();

        // 1) 先批量命中缓存
        foreach (var key in keys)
        {
            var item = await _cache.GetAsync(key);
            if (item != null)
            {
                result.Add(new ConfigValueDto
                {
                    ConfigKey = key,
                    ConfigValue = item.Value ?? string.Empty,
                    Status = item.Status,
                    Exists = item.Exists
                });
            }
            else
            {
                missKeys.Add(key);
            }
        }

        // 2) 未命中的一次性回源（批量优化：不循环单条查库）
        if (missKeys.Count > 0)
        {
            var rows = await _repository.GetByKeysAsync(missKeys);
            var map = rows.ToDictionary(r => r.ConfigKey);

            foreach (var key in missKeys)
            {
                map.TryGetValue(key, out var entity);
                await _cache.SetAsync(key, new ConfigCacheItem
                {
                    Value = entity?.ConfigValue,
                    Status = entity?.Status ?? 0,
                    Exists = entity != null
                });

                result.Add(new ConfigValueDto
                {
                    ConfigKey = key,
                    ConfigValue = entity?.ConfigValue ?? string.Empty,
                    Status = entity?.Status ?? 0,
                    Exists = entity != null
                });
            }
        }

        // 保持与入参一致的顺序，方便前端按下标取值
        var ordered = keys
            .Select(k => result.First(r => string.Equals(r.ConfigKey, k, StringComparison.Ordinal)))
            .ToList();

        return ApiResponse<List<ConfigValueDto>>.Success(ordered);
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<ConfigCacheResultDto>> RefreshCacheAsync()
    {
        // 并发刷新加锁：同一时刻只有一个线程真正装载，其余直接复用结果
        await RefreshLock.WaitAsync();
        try
        {
            await _cache.RemoveAllAsync();
            var count = await PreloadAllAsync();

            return ApiResponse<ConfigCacheResultDto>.Success(new ConfigCacheResultDto
            {
                Action = "refresh",
                Count = count,
                OperateTime = DateTime.Now
            }, $"配置缓存已刷新，共载入 {count} 条");
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<ConfigCacheResultDto>> ClearCacheAsync()
    {
        await _cache.RemoveAllAsync();

        return ApiResponse<ConfigCacheResultDto>.Success(new ConfigCacheResultDto
        {
            Action = "clear",
            Count = 0,
            OperateTime = DateTime.Now
        }, "配置缓存已清空");
    }

    /// <inheritdoc/>
    public async Task<int> PreloadAllAsync()
    {
        var rows = await _repository.GetAllAsync();

        var items = rows.ToDictionary(
            r => SysConfigCacheHelper.BuildKey(r.ConfigKey),
            r => new ConfigCacheItem { Value = r.ConfigValue, Status = r.Status, Exists = true });

        await _cache.SetManyAsync(items);
        return rows.Count;
    }

    /// <inheritdoc/>
    public async Task SetValueAsync(string configKey, string value, bool builtIn = false)
    {
        var entity = await _repository.GetByKeyAsync(configKey);
        if (entity == null)
        {
            // 注意：走仓储 Add 的是非跟踪实体，这里构造后直接新增即可
            var now = DateTime.Now;
            await _repository.AddAsync(new SysConfig
            {
                ConfigName = configKey,
                ConfigKey = configKey,
                ConfigValue = value,
                ConfigType = builtIn ? ConfigTypeConst.BuiltIn : ConfigTypeConst.Custom,
                Status = 0,
                CreateBy = _currentUser.UserName,
                CreateTime = now,
                UpdateBy = _currentUser.UserName,
                UpdateTime = now
            });
        }
        else
        {
            var tracked = await _repository.GetByIdAsync(entity.Id);
            if (tracked != null)
            {
                tracked.ConfigValue = value;
                tracked.UpdateBy = _currentUser.UserName;
                tracked.UpdateTime = DateTime.Now;
            }
        }

        await _repository.SaveChangesAsync();

        // 写库成功后立即失效缓存，保证下一次读取拿到新值
        await _cache.RemoveAsync(configKey);
    }

    // ============================================================
    // 内部工具
    // ============================================================

    /// <summary>Entity → 列表 DTO（顺带补齐中文展示字段）</summary>
    private SysConfigDto ToDto(SysConfig entity)
    {
        var dto = _mapper.Map<SysConfigDto>(entity);
        dto.ConfigTypeText = ConfigTypeConst.ToText(entity.ConfigType);
        dto.StatusText = entity.Status == 0 ? "正常" : "停用";
        dto.IsBuiltIn = entity.ConfigType == ConfigTypeConst.BuiltIn;
        return dto;
    }

    /// <summary>配置类型归一化：只认 Y / N，其它一律按自定义处理</summary>
    private static string NormalizeType(string? type)
        => string.Equals(type, ConfigTypeConst.BuiltIn, StringComparison.OrdinalIgnoreCase)
            ? ConfigTypeConst.BuiltIn
            : ConfigTypeConst.Custom;
}
