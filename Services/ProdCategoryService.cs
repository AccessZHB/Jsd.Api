using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Product;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 商品分类服务实现
/// </summary>
public class ProdCategoryService : IProdCategoryService
{
    private readonly IRepository<ProdCategory> _categoryRepository;
    private readonly IRepository<ProdInfo> _productRepository;
    private readonly IMapper _mapper;

    public ProdCategoryService(
        IRepository<ProdCategory> categoryRepository,
        IRepository<ProdInfo> productRepository,
        IMapper mapper)
    {
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _mapper = mapper;
    }

    /// <summary>
    /// 获取分类树（管理页需要看到禁用的分类，所以不过滤状态）
    /// </summary>
    public async Task<ApiResponse<List<CategoryTreeNodeDto>>> GetTreeAsync()
    {
        // 一次性取出全部分类（分类数据量小，内存组树比逐级查库高效）
        var all = await _categoryRepository.Query()
            .AsNoTracking()
            .OrderBy(c => c.Sort)
            .ThenBy(c => c.Id)
            .ToListAsync();

        var tree = BuildTree(all, rootParentId: 0);
        return ApiResponse<List<CategoryTreeNodeDto>>.Success(tree);
    }

    /// <summary>
    /// 分页查询分类列表（扁平结构，按名称搜索）
    /// </summary>
    public async Task<ApiResponse<PagedResult<CategoryDto>>> GetPagedListAsync(string? keyword, int page, int pageSize)
    {
        // 参数兜底
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var query = _categoryRepository.Query().AsNoTracking().AsQueryable();

        // 名称模糊搜索
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(c => c.CategoryName.Contains(keyword));
        }

        var total = await query.CountAsync();

        // 排序：同级按 Sort，再按 Id
        var items = await query
            .OrderBy(c => c.Sort)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var list = _mapper.Map<List<CategoryDto>>(items);
        return ApiResponse<PagedResult<CategoryDto>>.Success(
            PagedResult<CategoryDto>.Create(list, total, page, pageSize));
    }

    /// <summary>
    /// 根据 ID 获取分类详情
    /// </summary>
    public async Task<ApiResponse<CategoryDto>> GetByIdAsync(long id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category == null)
        {
            return ApiResponse<CategoryDto>.Fail("分类不存在", 404);
        }

        return ApiResponse<CategoryDto>.Success(_mapper.Map<CategoryDto>(category));
    }

    /// <summary>
    /// 新增分类
    /// </summary>
    public async Task<ApiResponse<object>> CreateAsync(CategoryCreateDto dto)
    {
        // 指定了父分类时，校验父分类存在
        if (dto.ParentId != 0 && !await _categoryRepository.AnyAsync(c => c.Id == dto.ParentId))
        {
            return ApiResponse<object>.Fail("所选父分类不存在");
        }

        var category = _mapper.Map<ProdCategory>(dto);
        await _categoryRepository.AddAsync(category);
        await _categoryRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = category.Id }, "分类创建成功");
    }

    /// <summary>
    /// 修改分类
    /// </summary>
    public async Task<ApiResponse<object>> UpdateAsync(CategoryUpdateDto dto)
    {
        // 1. 分类必须存在
        var category = await _categoryRepository.GetByIdAsync(dto.Id);
        if (category == null)
        {
            return ApiResponse<object>.Fail("分类不存在", 404);
        }

        // 2. 父级不能是自己（否则形成死循环树）
        if (dto.ParentId == dto.Id)
        {
            return ApiResponse<object>.Fail("父分类不能是当前分类自己");
        }

        // 3. 指定了父分类时，校验父分类存在
        if (dto.ParentId != 0 && !await _categoryRepository.AnyAsync(c => c.Id == dto.ParentId))
        {
            return ApiResponse<object>.Fail("所选父分类不存在");
        }

        // 4. DTO 增量覆盖并保存
        _mapper.Map(dto, category);
        await _categoryRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = category.Id }, "分类修改成功");
    }

    /// <summary>
    /// 删除分类。
    /// 保护规则：存在子分类或关联商品时拒绝删除（规范 5.7）。
    /// </summary>
    public async Task<ApiResponse<object>> DeleteAsync(long id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category == null)
        {
            return ApiResponse<object>.Fail("分类不存在", 404);
        }

        // 有子分类时拒绝删除
        if (await _categoryRepository.AnyAsync(c => c.ParentId == id))
        {
            return ApiResponse<object>.Fail("该分类下存在子分类，请先删除子分类");
        }

        // 有关联商品时拒绝删除
        if (await _productRepository.AnyAsync(p => p.ProdCategoryId == id))
        {
            return ApiResponse<object>.Fail("该分类下存在关联商品，无法删除");
        }

        _categoryRepository.Remove(category);
        await _categoryRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = id }, "删除成功");
    }

    /// <summary>
    /// 递归把扁平分类列表组装成树形结构（与菜单树同一套路）
    /// </summary>
    private List<CategoryTreeNodeDto> BuildTree(List<ProdCategory> categories, long rootParentId)
    {
        // 按 ParentId 分组，方便取某节点的直接子级
        var lookup = categories.ToLookup(c => c.ParentId);

        List<CategoryTreeNodeDto> BuildNodes(long parentId)
        {
            return lookup[parentId]
                .Select(c => new CategoryTreeNodeDto
                {
                    Id = c.Id,
                    ParentId = c.ParentId,
                    CategoryName = c.CategoryName,
                    Sort = c.Sort,
                    Status = c.Status,
                    Children = BuildNodes(c.Id)   // 递归挂子分类
                })
                .ToList();
        }

        return BuildNodes(rootParentId);
    }
}
