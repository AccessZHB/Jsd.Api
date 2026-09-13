using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Supplier;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 供应商服务实现
/// </summary>
public class SupSupplierService : ISupSupplierService
{
    private readonly ISupSupplierRepository _supplierRepository;
    private readonly IRepository<ProdInfo> _productRepository;
    private readonly IMapper _mapper;

    public SupSupplierService(
        ISupSupplierRepository supplierRepository,
        IRepository<ProdInfo> productRepository,
        IMapper mapper)
    {
        _supplierRepository = supplierRepository;
        _productRepository = productRepository;
        _mapper = mapper;
    }

    /// <summary>
    /// 分页查询供应商列表
    /// </summary>
    public async Task<ApiResponse<PagedResult<SupplierDto>>> GetPagedListAsync(string? keyword, int? status, int page, int pageSize)
    {
        // 参数兜底，防止前端传非法值
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (items, total) = await _supplierRepository.GetPagedListAsync(keyword, status, page, pageSize);
        var list = _mapper.Map<List<SupplierDto>>(items);

        return ApiResponse<PagedResult<SupplierDto>>.Success(
            PagedResult<SupplierDto>.Create(list, total, page, pageSize));
    }

    /// <summary>
    /// 根据 ID 获取供应商详情
    /// </summary>
    public async Task<ApiResponse<SupplierDto>> GetByIdAsync(long id)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id);
        if (supplier == null)
        {
            return ApiResponse<SupplierDto>.Fail("供应商不存在", 404);
        }

        return ApiResponse<SupplierDto>.Success(_mapper.Map<SupplierDto>(supplier));
    }

    /// <summary>
    /// 新增供应商
    /// </summary>
    public async Task<ApiResponse<object>> CreateAsync(SupplierCreateDto dto)
    {
        // 1. 名称重复校验（同名的供应商容易造成采购混淆）
        if (await _supplierRepository.AnyAsync(s => s.SupplierName == dto.SupplierName))
        {
            return ApiResponse<object>.Fail("供应商名称已存在");
        }

        // 2. DTO → Entity 并保存
        var supplier = _mapper.Map<SupSupplier>(dto);
        await _supplierRepository.AddAsync(supplier);
        await _supplierRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = supplier.Id }, "供应商创建成功");
    }

    /// <summary>
    /// 修改供应商信息
    /// </summary>
    public async Task<ApiResponse<object>> UpdateAsync(SupplierUpdateDto dto)
    {
        // 1. 供应商必须存在
        var supplier = await _supplierRepository.GetByIdAsync(dto.Id);
        if (supplier == null)
        {
            return ApiResponse<object>.Fail("供应商不存在", 404);
        }

        // 2. 名称重复校验（排除自己）
        if (await _supplierRepository.AnyAsync(s => s.SupplierName == dto.SupplierName && s.Id != dto.Id))
        {
            return ApiResponse<object>.Fail("供应商名称已存在");
        }

        // 3. DTO 增量覆盖到已跟踪实体并保存
        _mapper.Map(dto, supplier);
        await _supplierRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = supplier.Id }, "供应商修改成功");
    }

    /// <summary>
    /// 删除供应商。
    /// 保护规则：存在关联商品时拒绝物理删除（避免商品悬挂），提示改为"禁用"。
    /// </summary>
    public async Task<ApiResponse<object>> DeleteAsync(long id)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id);
        if (supplier == null)
        {
            return ApiResponse<object>.Fail("供应商不存在", 404);
        }

        // 有关联商品时拒绝删除
        if (await _productRepository.AnyAsync(p => p.SupplierId == id))
        {
            return ApiResponse<object>.Fail("该供应商下存在关联商品，无法删除；如需停用请将合作状态改为禁用");
        }

        _supplierRepository.Remove(supplier);
        await _supplierRepository.SaveChangesAsync();

        return ApiResponse<object>.Success(new { id = id }, "删除成功");
    }

    /// <summary>
    /// 获取所有启用状态的供应商（商品录入下拉框用，不分页）
    /// </summary>
    public async Task<ApiResponse<List<SupplierDto>>> GetAllEnabledAsync()
    {
        var suppliers = await _supplierRepository.Query()
            .AsNoTracking()
            .Where(s => s.Status == 1)
            .OrderBy(s => s.Id)
            .ToListAsync();

        return ApiResponse<List<SupplierDto>>.Success(_mapper.Map<List<SupplierDto>>(suppliers));
    }
}
