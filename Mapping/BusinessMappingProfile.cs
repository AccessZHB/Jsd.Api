using AutoMapper;
using Jsd.Api.Entities;
using Jsd.Api.Models.Product;
using Jsd.Api.Models.Supplier;

namespace Jsd.Api.Mapping;

/// <summary>
/// 基础数据 & 商品管理模块的 AutoMapper 映射配置
/// （AddAutoMapper 会自动扫描程序集中所有 Profile，无需手动注册）
/// </summary>
public class BusinessMappingProfile : Profile
{
    public BusinessMappingProfile()
    {
        // ============================================================
        // 供应商
        // ============================================================
        CreateMap<SupSupplier, SupplierDto>();

        CreateMap<SupplierCreateDto, SupSupplier>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());

        CreateMap<SupplierUpdateDto, SupSupplier>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());

        // ============================================================
        // 商品分类
        // ============================================================
        CreateMap<ProdCategory, CategoryDto>();

        // 树节点：Children 由 Service 递归组装，AutoMapper 不处理
        CreateMap<ProdCategory, CategoryTreeNodeDto>();

        CreateMap<CategoryCreateDto, ProdCategory>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());

        CreateMap<CategoryUpdateDto, ProdCategory>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());

        // ============================================================
        // 商品（SPU）
        // ============================================================
        // CategoryName / SupplierName 来自 Include 的导航属性；
        // MinPrice/MaxPrice/TotalStock 由 Service 聚合 SKU 后填充
        CreateMap<ProdInfo, ProdInfoDto>()
            .ForMember(d => d.CategoryName,
                       o => o.MapFrom(p => p.Category != null ? p.Category.CategoryName : null))
            .ForMember(d => d.SupplierName,
                       o => o.MapFrom(p => p.Supplier != null ? p.Supplier.SupplierName : null))
            .ForMember(d => d.MinPrice, o => o.Ignore())
            .ForMember(d => d.MaxPrice, o => o.Ignore())
            .ForMember(d => d.TotalStock, o => o.Ignore());

        // 详情 DTO：SpecItems / Skus 由 Service 查询后填充
        CreateMap<ProdInfo, ProdInfoDetailDto>()
            .IncludeBase<ProdInfo, ProdInfoDto>()
            .ForMember(d => d.CategoryName,
                       o => o.MapFrom(p => p.Category != null ? p.Category.CategoryName : null))
            .ForMember(d => d.SupplierName,
                       o => o.MapFrom(p => p.Supplier != null ? p.Supplier.SupplierName : null));

        CreateMap<ProdInfoCreateDto, ProdInfo>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.Category, opt => opt.Ignore())
            .ForMember(e => e.Supplier, opt => opt.Ignore())
            .ForMember(e => e.SalesCount, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());
            // 注：dto.SpecItems / dto.Skus 为集合入参，由 Service 手工处理，不走映射

        CreateMap<ProdInfoUpdateDto, ProdInfo>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.Category, opt => opt.Ignore())
            .ForMember(e => e.Supplier, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());

        // ============================================================
        // 规格项 / 规格值
        // ============================================================
        // SpecItemDto.Values 由 Service 填充（查询规格值后挂载）
        CreateMap<ProdSpecItem, SpecItemDto>();

        CreateMap<SpecItemCreateDto, ProdSpecItem>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());

        CreateMap<SpecItemUpdateDto, ProdSpecItem>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.ProdInfoId, opt => opt.Ignore())  // 商品归属不允许修改
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());

        CreateMap<ProdSpecValue, SpecValueDto>();

        CreateMap<SpecValueCreateDto, ProdSpecValue>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());

        CreateMap<SpecValueUpdateDto, ProdSpecValue>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.SpecId, opt => opt.Ignore())      // 规格项归属不允许修改
            .ForMember(e => e.ProdInfoId, opt => opt.Ignore())  // 商品归属不允许修改
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());

        // ============================================================
        // SKU
        // ============================================================
        CreateMap<ProdSku, ProdSkuDto>()
            // SpecValuesText / Specs 由 Service 按ID反查后填充，不走映射
            .ForMember(d => d.SpecValuesText, o => o.Ignore())
            .ForMember(d => d.Specs, o => o.Ignore());

        CreateMap<ProdSkuCreateDto, ProdSku>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.SkuCode, opt => opt.Ignore())     // 后端自动生成，禁止前端传值
            .ForMember(e => e.SpecValues, opt => opt.Ignore())  // 由 Service 提取规格值ID后拼接赋值
            .ForMember(e => e.SalesCount, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());
            // dto.Specs（规格对象数组）为集合入参，由 Service 的 BuildSpecValueIds 处理，不走映射

        CreateMap<ProdSkuUpdateDto, ProdSku>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.ProdInfoId, opt => opt.Ignore())  // 商品归属不允许修改
            .ForMember(e => e.SkuCode, opt => opt.Ignore())     // 编码由后端生成，不允许修改
            .ForMember(e => e.SpecValues, opt => opt.Ignore())  // 由 Service 校验后赋值
            .ForMember(e => e.SalesCount, opt => opt.Ignore())
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());

        // ============================================================
        // 商品图片（prod_image）
        // ============================================================
        CreateMap<ProdImage, ProdImageDto>();

        CreateMap<ProdImageSaveDto, ProdImage>()
            .ForMember(e => e.Id, opt => opt.Ignore())
            .ForMember(e => e.ProdInfoId, opt => opt.Ignore())  // 归属商品由 Service 填充
            .ForMember(e => e.CreateTime, opt => opt.Ignore())
            .ForMember(e => e.UpdateTime, opt => opt.Ignore());
    }
}
