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
/// 字典类型服务实现。
///
/// 约定：
///  - 所有字符串入参统一 Trim，避免前后空格落库（防御性编程）
///  - dict_type 编码全局唯一，新增/修改前显式校验（数据库上有 uk_dict_type，
///    但直接抛 Doctrine 式异常对前端不友好，这里先业务校验并给出中文提示）
///  - 删除类型时在同一事务内完成「删类型 + 删数据 + 清缓存」三步
/// </summary>
public class DictTypeService : IDictTypeService
{
    private readonly IDictTypeRepository _typeRepository;
    private readonly IDictDataRepository _dataRepository;
    private readonly IDictCacheService _cacheService;
    private readonly CurrentUserService _currentUser;
    private readonly IMapper _mapper;
    // 删除类型需要多表事务（删类型 + 删其下字典数据），直接注入 DbContext 开事务（项目既有做法）
    private readonly AppDbContext _db;

    public DictTypeService(
        IDictTypeRepository typeRepository,
        IDictDataRepository dataRepository,
        IDictCacheService cacheService,
        CurrentUserService currentUser,
        IMapper mapper,
        AppDbContext db)
    {
        _typeRepository = typeRepository;
        _dataRepository = dataRepository;
        _cacheService = cacheService;
        _currentUser = currentUser;
        _mapper = mapper;
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<PagedResult<DictTypeDto>>> GetPagedAsync(DictTypeQueryDto query)
    {
        // 参数兜底，防止前端传 0 / 负数 / 超大页
        if (query.Page < 1) query.Page = 1;
        if (query.PageSize < 1 || query.PageSize > 200) query.PageSize = 10;

        var (items, total) = await _typeRepository.GetPagedListAsync(query);

        // 列表页要展示"该类型下有多少条数据"，这里批量取一次，避免 N+1 查询
        var countMap = await _typeRepository.GetDataCountMapAsync(items.Select(x => x.Id).ToList());

        var list = _mapper.Map<List<DictTypeDto>>(items);
        foreach (var dto in list)
        {
            dto.StatusText = StatusText(dto.Status);
            dto.DataCount = countMap.TryGetValue(dto.Id, out var c) ? c : 0;
        }

        return ApiResponse<PagedResult<DictTypeDto>>.Success(
            PagedResult<DictTypeDto>.Create(list, total, query.Page, query.PageSize));
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<DictTypeDto?>> GetByIdAsync(long id)
    {
        var entity = await _typeRepository.GetByIdAsync(id);
        if (entity == null)
        {
            return ApiResponse<DictTypeDto?>.Fail("字典类型不存在", 404);
        }

        var dto = _mapper.Map<DictTypeDto>(entity);
        dto.StatusText = StatusText(dto.Status);

        return ApiResponse<DictTypeDto?>.Success(dto);
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<List<DictTypeOptionDto>>> GetOptionsAsync()
    {
        var entities = await _typeRepository.GetListAsync(x => x.Status == 0);

        var list = _mapper.Map<List<DictTypeOptionDto>>(entities);
        return ApiResponse<List<DictTypeOptionDto>>.Success(list);
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<long>> CreateAsync(CreateDictTypeDto dto)
    {
        // FluentValidation：Controller 捕获 ValidationException 后转 ApiResponse.Fail
        new CreateDictTypeValidator().ValidateAndThrow(dto);

        dto.DictName = dto.DictName.Trim();
        dto.DictType = dto.DictType.Trim();

        if (await _typeRepository.ExistsAsync(dto.DictType, 0))
        {
            return ApiResponse<long>.Fail($"字典类型编码 [{dto.DictType}] 已存在");
        }

        var entity = _mapper.Map<SysDictType>(dto);
        entity.Status = dto.Status == 1 ? 1 : 0;   // 默认 0=正常（启用）
        entity.CreateBy = _currentUser.UserName;
        entity.CreateTime = DateTime.Now;

        await _typeRepository.AddAsync(entity);
        await _typeRepository.SaveChangesAsync();

        return ApiResponse<long>.Success(entity.Id, "新增成功");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> UpdateAsync(UpdateDictTypeDto dto)
    {
        new UpdateDictTypeValidator().ValidateAndThrow(dto);

        dto.DictName = dto.DictName.Trim();
        dto.DictType = dto.DictType.Trim();

        var entity = await _typeRepository.GetByIdAsync(dto.Id);
        if (entity == null) return ApiResponse<bool>.Fail("字典类型不存在", 404);

        if (await _typeRepository.ExistsAsync(dto.DictType, dto.Id))
        {
            return ApiResponse<bool>.Fail($"字典类型编码 [{dto.DictType}] 已存在");
        }

        string oldDictType = entity.DictType;

        _mapper.Map(dto, entity);
        entity.UpdateBy = _currentUser.UserName;
        entity.UpdateTime = DateTime.Now;

        await _typeRepository.SaveChangesAsync();

        // 编码被改过就失效新旧两个缓存，避免旧编码还留着脏缓存
        await _cacheService.RemoveAsync(oldDictType);
        await _cacheService.RemoveAsync(dto.DictType);

        return ApiResponse<bool>.Success(true, "修改成功");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> ChangeStatusAsync(long id, DictStatusDto dto)
    {
        var entity = await _typeRepository.GetByIdAsync(id);
        if (entity == null) return ApiResponse<bool>.Fail("字典类型不存在", 404);

        if (dto.Status != 0 && dto.Status != 1)
        {
            return ApiResponse<bool>.Fail("状态值非法（0-正常 1-停用）");
        }

        entity.Status = dto.Status;
        entity.UpdateBy = _currentUser.UserName;
        entity.UpdateTime = DateTime.Now;

        await _typeRepository.SaveChangesAsync();

        // 类型停用后，该编码的缓存内容已变化（数据仍在库里但对外不可见），必须失效
        await _cacheService.RemoveAsync(entity.DictType);

        return ApiResponse<bool>.Success(true, dto.Status == 0 ? "已启用" : "已停用");
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> DeleteAsync(long id)
    {
        var entity = await _typeRepository.GetByIdAsync(id);
        if (entity == null) return ApiResponse<bool>.Fail("字典类型不存在", 404);

        string dictType = entity.DictType;
        int deletedData = 0;

        try
        {
            // 同一事务内完成「删类型 + 删其下字典数据」，任一环节失败整体回滚
            await _db.Database.BeginTransactionAsync();

            deletedData = await _dataRepository.DeleteByDictTypeIdAsync(id);

            _typeRepository.Remove(entity);
            await _typeRepository.SaveChangesAsync();

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

        // 缓存必须在事务提交后再清，否则回滚时清了缓存反而读到旧数据
        await _cacheService.RemoveAsync(dictType);

        return ApiResponse<bool>.Success(true, $"删除成功，同时级联删除其下 {deletedData} 条字典数据");
    }

    /// <summary>状态 → 中文文案（库里 status：0-正常 1-停用）</summary>
    private static string StatusText(int status) => status == 0 ? "正常" : "停用";
}
