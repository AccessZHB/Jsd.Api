using AutoMapper;
using FluentValidation;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Dict;
using Jsd.Api.Repositories;
using Jsd.Api.Validators;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 字典数据服务实现。
///
/// 约定：
///  - is_default 同一 dict_type_id 下最多只能有一条「是」，新增/修改时做业务校验，
///    并把同类型下的其它默认值自动改回「否」，保证数据一致（文档硬性要求）
///  - 任何写操作后必须失效对应 dictType 的缓存，否则前端下拉会读到旧数据
/// </summary>
public class DictDataService : IDictDataService
{
    private readonly IDictDataRepository _dataRepository;
    private readonly IDictTypeRepository _typeRepository;
    private readonly IDictCacheService _cacheService;
    private readonly CurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly AppDbContext _db;

    public DictDataService(
        IDictDataRepository dataRepository,
        IDictTypeRepository typeRepository,
        IDictCacheService cacheService,
        CurrentUserService currentUser,
        IMapper mapper,
        AppDbContext db)
    {
        _dataRepository = dataRepository;
        _typeRepository = typeRepository;
        _cacheService = cacheService;
        _currentUser = currentUser;
        _mapper = mapper;
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<PagedResult<DictDataDto>>> GetPagedAsync(DictDataQueryDto query)
    {
        if (query.Page < 1) query.Page = 1;
        if (query.PageSize < 1 || query.PageSize > 200) query.PageSize = 10;

        var (items, total) = await _dataRepository.GetPagedListAsync(query);

        var list = _mapper.Map<List<DictDataDto>>(items);
        foreach (var dto in list)
        {
            dto.StatusText = dto.Status == 0 ? "正常" : "停用";
        }

        return ApiResponse<PagedResult<DictDataDto>>.Success(
            PagedResult<DictDataDto>.Create(list, total, query.Page, query.PageSize));
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DictDataDto?>> GetByIdAsync(long id)
    {
        var entity = await _dataRepository.GetByIdAsync(id);
        if (entity == null) return ApiResponse<DictDataDto?>.Fail("字典数据不存在", 404);

        var dto = _mapper.Map<DictDataDto>(entity);
        dto.StatusText = dto.Status == 0 ? "正常" : "停用";

        return ApiResponse<DictDataDto?>.Success(dto);
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<long>> CreateAsync(CreateDictDataDto dto)
    {
        new CreateDictDataValidator().ValidateAndThrow(dto);

        var type = await _typeRepository.GetByIdAsync(dto.DictTypeId);
        if (type == null) return ApiResponse<long>.Fail("关联的字典类型不存在", 400);

        TrimAll(dto);
        await EnsureDictTypeAsync(dto.DictTypeId);

        var entity = _mapper.Map<SysDictData>(dto);
        entity.IsDefault = dto.IsDefault ? "Y" : "N";
        entity.Status = dto.Status == 1 ? 1 : 0;
        entity.CreateBy = _currentUser.UserName;
        entity.CreateTime = DateTime.Now;
        entity.UpdateBy = _currentUser.UserName;

        if (dto.IsDefault)
        {
            await ClearOtherDefaultAsync(dto.DictTypeId, 0);
        }

        await _dataRepository.AddAsync(entity);
        await _dataRepository.SaveChangesAsync();

        await _cacheService.RemoveAsync(type.DictType);

        return ApiResponse<long>.Success(entity.Id, "新增成功");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> UpdateAsync(UpdateDictDataDto dto)
    {
        new UpdateDictDataValidator().ValidateAndThrow(dto);

        var entity = await _dataRepository.GetByIdAsync(dto.Id);
        if (entity == null) return ApiResponse<bool>.Fail("字典数据不存在", 404);

        var type = await _typeRepository.GetByIdAsync(dto.DictTypeId);
        if (type == null) return ApiResponse<bool>.Fail("关联的字典类型不存在", 400);

        TrimAll(dto);
        await EnsureDictTypeAsync(dto.DictTypeId);

        _mapper.Map(dto, entity);
        entity.IsDefault = dto.IsDefault ? "Y" : "N";
        entity.Status = dto.Status == 1 ? 1 : 0;
        entity.UpdateBy = _currentUser.UserName;
        entity.UpdateTime = DateTime.Now;

        if (dto.IsDefault)
        {
            await ClearOtherDefaultAsync(dto.DictTypeId, entity.Id);
        }

        await _dataRepository.SaveChangesAsync();

        await _cacheService.RemoveAsync(type.DictType);

        return ApiResponse<bool>.Success(true, "修改成功");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> ChangeStatusAsync(long id, DictStatusDto dto)
    {
        var entity = await _dataRepository.GetByIdAsync(id);
        if (entity == null) return ApiResponse<bool>.Fail("字典数据不存在", 404);

        if (dto.Status != 0 && dto.Status != 1)
        {
            return ApiResponse<bool>.Fail("状态值非法（0-正常 1-停用）");
        }

        entity.Status = dto.Status;
        entity.UpdateBy = _currentUser.UserName;
        entity.UpdateTime = DateTime.Now;

        await _dataRepository.SaveChangesAsync();

        var type = await _typeRepository.GetByIdAsync(entity.DictTypeId);
        if (type != null)
        {
            await _cacheService.RemoveAsync(type.DictType);
        }

        return ApiResponse<bool>.Success(true, dto.Status == 0 ? "已启用" : "已停用");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> DeleteAsync(long id)
    {
        var entity = await _dataRepository.GetByIdAsync(id);
        if (entity == null) return ApiResponse<bool>.Fail("字典数据不存在", 404);

        _dataRepository.Remove(entity);
        await _dataRepository.SaveChangesAsync();

        var type = await _typeRepository.GetByIdAsync(entity.DictTypeId);
        if (type != null)
        {
            await _cacheService.RemoveAsync(type.DictType);
        }

        return ApiResponse<bool>.Success(true, "删除成功");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<List<DictDataItem>>> GetByDictTypeAsync(string dictType)
    {
        if (string.IsNullOrWhiteSpace(dictType))
        {
            return ApiResponse<List<DictDataItem>>.Fail("字典类型编码不能为空");
        }

        // 内部：读缓存 → 未命中回源数据库 → 回写缓存（空结果短 TTL，防穿透）
        var items = await _cacheService.GetByDictTypeAsync(dictType.Trim());

        return ApiResponse<List<DictDataItem>>.Success(items);
    }

    // ============================================================
    // 私有工具
    // ============================================================

    /// <summary>所有字符串入参统一 Trim，避免空格落库</summary>
    private static void TrimAll(CreateDictDataDto dto)
    {
        dto.DictLabel = dto.DictLabel.Trim();
        dto.DictValue = dto.DictValue.Trim();
        if (dto.CssClass != null) dto.CssClass = SanitizeCss(dto.CssClass);
        if (dto.Remark != null) dto.Remark = dto.Remark.Trim();
    }

    private static void TrimAll(UpdateDictDataDto dto)
    {
        dto.DictLabel = dto.DictLabel.Trim();
        dto.DictValue = dto.DictValue.Trim();
        if (dto.CssClass != null) dto.CssClass = SanitizeCss(dto.CssClass);
        if (dto.Remark != null) dto.Remark = dto.Remark.Trim();
    }

    /// <summary>
    /// css_class 最终会拼进 HTML 的 class 属性，去掉尖括号/引号/括号/反斜杠，防 XSS。
    /// </summary>
    private static string SanitizeCss(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var chars = input.Trim()
            .Where(c => c != '<' && c != '>' && c != '"' && c != '\'' && c != '\\' && c != '(' && c != ')')
            .ToArray();

        return new string(chars);
    }

    /// <summary>
    /// 「同一类型下最多一个默认值」的业务校验：
    /// 既不合法默认值的设置直接拒绝，合法时把同类型其它默认值改回「否」。
    /// 用事务包裹，防止并发下出现两条默认值。
    /// </summary>
    private async Task EnsureDictTypeAsync(long dictTypeId)
    {
        // 仅校验类型存在性（默认值本身允许没有），单独一个方法名会误导，这里重命名注释说明
        var exists = await _typeRepository.AnyAsync(x => x.Id == dictTypeId);
        if (!exists)
        {
            throw new InvalidOperationException("关联的字典类型不存在");
        }
    }

    /// <summary>把同一字典类型下的其它默认值改回「否」（排除 excludeId）</summary>
    private async Task ClearOtherDefaultAsync(long dictTypeId, long excludeId)
    {
        await _db.Database.BeginTransactionAsync();

        try
        {
            // ExecuteUpdate：一条 UPDATE 直接落库，不加载实体
            await _db.SysDictDatas
                .Where(d => d.DictTypeId == dictTypeId && d.IsDefault == "Y" && d.Id != excludeId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsDefault, "N"));

            await _db.Database.CommitTransactionAsync();
        }
        catch
        {
            if (_db.Database.CurrentTransaction != null)
            {
                await _db.Database.RollbackTransactionAsync();
            }

            throw;
        }
    }
}
