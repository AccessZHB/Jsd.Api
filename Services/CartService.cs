using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jsd.Api.Entities;
using Jsd.Api.Models.Cart;
using Jsd.Api.Models.Common;
using Jsd.Api.Models.Price;
using Jsd.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jsd.Api.Services;

/// <summary>
/// 购物车服务实现（mem_cart）
///
/// 【修复的核心 Bug】历史上前端本地购物车的唯一键只有「商品ID + SKU + 球镜柱镜」，
/// 导致「需要加工，邮寄镜架」与「不需要加工，只发片」被合并成一条（数量×2），加工属性丢失。
/// 现在唯一键 = memberId + prodInfoId + skuId + processingType + prescriptionHash，
/// 并且数据库有 UNIQUE(cart_key) 兜底，从根上杜绝错误合并。
/// </summary>
public class CartService : ICartService
{
    private readonly ICartRepository _cartRepo;
    private readonly AppDbContext _db;
    private readonly CurrentUserService _currentUser;
    private readonly IPriceService _priceService;

    /// <summary>反序列化验光 JSON 用（兼容 camelCase / PascalCase）</summary>
    private static readonly JsonSerializerOptions RxJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CartService(
        ICartRepository cartRepo,
        AppDbContext db,
        CurrentUserService currentUser,
        IPriceService priceService)
    {
        _cartRepo = cartRepo;
        _db = db;
        _currentUser = currentUser;
        _priceService = priceService;
    }

    // ============================================================
    // 1. 购物车列表
    // ============================================================

    /// <inheritdoc />
    public async Task<ApiResponse<CartListDto>> GetListAsync()
    {
        var memberId = _currentUser.MemberId;
        if (memberId <= 0)
        {
            return ApiResponse<CartListDto>.Fail("未获取到登录会员信息", 401);
        }

        var rows = await _cartRepo.GetListByMemberAsync(memberId);
        if (rows.Count == 0)
        {
            return ApiResponse<CartListDto>.Success(new CartListDto());
        }

        var skuIds = rows.Select(r => r.SkuId).Distinct().ToList();
        var prodIds = rows.Select(r => r.ProdInfoId).Distinct().ToList();

        var skus = await _db.ProdSkus.AsNoTracking()
            .Where(s => skuIds.Contains(s.Id))
            .Select(s => new { s.Id, s.SkuName, s.SpecValues, s.RetailPrice, s.SalePrice, s.Image })
            .ToListAsync();
        var skuMap = skus.ToDictionary(s => s.Id);

        var prods = await _db.ProdInfos.AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ProdInfoName, p.MainImage, p.IsPrescription })
            .ToListAsync();
        var prodMap = prods.ToDictionary(p => p.Id);

        // 会员成交价：按 SKU 汇总数量后统一取价（阶梯价按该 SKU 在购物车的总数量命中）
        var priceBySku = await ResolvePricesAsync(memberId, rows);

        var list = new List<CartItemDto>(rows.Count);
        foreach (var row in rows)
        {
            skuMap.TryGetValue(row.SkuId, out var sku);
            prodMap.TryGetValue(row.ProdInfoId, out var prod);

            // 非镜片商品强制视为「不需要加工」，避免脏数据展示成待加工
            var isLens = prod?.IsPrescription == 1;
            var processingType = isLens && row.ProcessingType == 1 ? 1 : 0;

            var price = priceBySku.TryGetValue(row.SkuId, out var p)
                ? p
                : (sku?.SalePrice ?? sku?.RetailPrice ?? 0m);

            var skuLabel = !string.IsNullOrWhiteSpace(sku?.SkuName)
                ? sku!.SkuName!
                : (!string.IsNullOrWhiteSpace(sku?.SpecValues) ? sku!.SpecValues! : "默认规格");

            list.Add(new CartItemDto
            {
                Id = row.Id,
                Key = row.Id.ToString(),
                ProductId = row.ProdInfoId,
                SkuId = row.SkuId,
                Name = prod?.ProdInfoName ?? "商品已下架",
                Image = !string.IsNullOrWhiteSpace(sku?.Image) ? sku!.Image : prod?.MainImage,
                SpecText = skuLabel + "·" + (processingType == 1 ? "需要加工，邮寄镜架" : "不需要加工，只发片"),
                Price = price,
                OriginPrice = sku?.RetailPrice,
                Qty = row.Quantity,
                Checked = row.IsSelected == 1,
                IsLens = isLens,
                ProcessingType = processingType,
                ProcessingStatus = processingType == 1 ? "unprocessed" : "none",
                ProcessingTypeText = processingType == 1 ? "需要加工" : "不需要加工",
                Prescription = ParsePrescription(row.PrescriptionData)
            });
        }

        var selected = list.Where(i => i.Checked).ToList();
        var result = new CartListDto
        {
            Items = list,
            TotalCount = list.Sum(i => i.Qty),
            SelectedCount = selected.Sum(i => i.Qty),
            SelectedAmount = Math.Round(selected.Sum(i => i.Price * i.Qty), 2),
            AllChecked = list.Count > 0 && list.All(i => i.Checked),
            HasProcessing = list.Any(i => i.ProcessingType == 1)
        };

        return ApiResponse<CartListDto>.Success(result);
    }

    // ============================================================
    // 2. 加入购物车（核心：合并判定）
    // ============================================================

    /// <inheritdoc />
    public async Task<ApiResponse<long>> AddAsync(CartAddDto dto)
    {
        var memberId = _currentUser.MemberId;
        if (memberId <= 0)
        {
            return ApiResponse<long>.Fail("请先登录后再加入购物车", 401);
        }
        if (dto.SkuId <= 0)
        {
            return ApiResponse<long>.Fail("请选择商品规格");
        }

        var qty = dto.Quantity <= 0 ? 1 : dto.Quantity;

        var sku = await _db.ProdSkus.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.SkuId);
        if (sku == null)
        {
            return ApiResponse<long>.Fail("商品规格不存在");
        }
        if (sku.Status != 1)
        {
            return ApiResponse<long>.Fail("该规格已下架");
        }

        var prod = await _db.ProdInfos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == sku.ProdInfoId);
        if (prod == null)
        {
            return ApiResponse<long>.Fail("商品不存在");
        }
        if (prod.Status != 1)
        {
            return ApiResponse<long>.Fail("商品已下架");
        }

        // 只有镜片类商品才允许「需要加工」；其它商品一律按 0 处理
        var isLens = prod.IsPrescription == 1;
        var processingType = isLens && dto.ProcessingType == 1 ? 1 : 0;

        if (processingType == 1)
        {
            if (dto.Prescription == null)
            {
                return ApiResponse<long>.Fail("需要加工的商品请先填写验光数据");
            }
            if (!HasAnyPrescription(dto.Prescription))
            {
                return ApiResponse<long>.Fail("验光数据不完整，请填写球镜 / 柱镜 / 轴位");
            }
        }

        // 规范化验光 → JSON + 哈希（哈希用于唯一键，避免逐列比较与 JSON 顺序差异）
        var (rxJson, rxHash) = Canonicalize(dto.Prescription, processingType);
        var cartKey = BuildCartKey(memberId, prod.Id, sku.Id, processingType, rxHash);

        // ===== 合并判定：只有 cartKey 完全一致才累加数量 =====
        var exist = await _cartRepo.FindByCartKeyAsync(cartKey);
        if (exist != null)
        {
            exist.Quantity += qty;
            exist.IsSelected = 1;
            _cartRepo.Update(exist);
            await _cartRepo.SaveChangesAsync();
            return ApiResponse<long>.Success(exist.Id, "已更新购物车数量");
        }

        // ===== 不存在（加工类型 / 验光任一不同）→ 必须插入新行 =====
        var entity = new MemCart
        {
            MemberId = memberId,
            ProdInfoId = prod.Id,
            SkuId = sku.Id,
            Quantity = qty,
            ProcessingType = processingType,
            PrescriptionData = string.IsNullOrEmpty(rxJson) ? null : rxJson,
            PrescriptionHash = rxHash,
            CartKey = cartKey,
            IsSelected = 1,
            AddTime = DateTime.Now
        };

        try
        {
            await _cartRepo.AddAsync(entity);
            await _cartRepo.SaveChangesAsync();
            return ApiResponse<long>.Success(entity.Id, "已加入购物车");
        }
        catch (DbUpdateException)
        {
            // 并发下唯一键冲突（两个请求同时插入同一 cartKey）：退化为累加已有行
            _db.Entry(entity).State = EntityState.Detached;
            var again = await _cartRepo.FindByCartKeyAsync(cartKey);
            if (again == null)
            {
                throw;
            }
            again.Quantity += qty;
            again.IsSelected = 1;
            _cartRepo.Update(again);
            await _cartRepo.SaveChangesAsync();
            return ApiResponse<long>.Success(again.Id, "已更新购物车数量");
        }
    }

    // ============================================================
    // 3. 修改数量
    // ============================================================

    /// <inheritdoc />
    public async Task<ApiResponse<bool>> UpdateQtyAsync(CartUpdateQtyDto dto)
    {
        var memberId = _currentUser.MemberId;
        if (memberId <= 0)
        {
            return ApiResponse<bool>.Fail("未获取到登录会员信息", 401);
        }

        var entity = await _cartRepo.GetByIdAsync(dto.Id, memberId);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("购物车商品不存在", 404);
        }

        entity.Quantity = dto.Quantity < 1 ? 1 : dto.Quantity;
        _cartRepo.Update(entity);
        await _cartRepo.SaveChangesAsync();
        return ApiResponse<bool>.Success(true, "数量已更新");
    }

    // ============================================================
    // 4. 删除单行 / 清空
    // ============================================================

    /// <inheritdoc />
    public async Task<ApiResponse<bool>> DeleteAsync(long id)
    {
        var memberId = _currentUser.MemberId;
        if (memberId <= 0)
        {
            return ApiResponse<bool>.Fail("未获取到登录会员信息", 401);
        }

        var entity = await _cartRepo.GetByIdAsync(id, memberId);
        if (entity == null)
        {
            return ApiResponse<bool>.Fail("购物车商品不存在", 404);
        }

        _cartRepo.Remove(entity);
        await _cartRepo.SaveChangesAsync();
        return ApiResponse<bool>.Success(true, "已删除");
    }

    /// <inheritdoc />
    public async Task<ApiResponse<bool>> ClearAsync()
    {
        var memberId = _currentUser.MemberId;
        if (memberId <= 0)
        {
            return ApiResponse<bool>.Fail("未获取到登录会员信息", 401);
        }

        var count = await _cartRepo.DeleteByMemberAsync(memberId);
        return ApiResponse<bool>.Success(true, $"已清空购物车（{count} 项）");
    }

    // ============================================================
    // 5. 勾选状态
    // ============================================================

    /// <inheritdoc />
    public async Task<ApiResponse<bool>> ToggleSelectAsync(CartToggleDto dto)
    {
        var memberId = _currentUser.MemberId;
        if (memberId <= 0)
        {
            return ApiResponse<bool>.Fail("未获取到登录会员信息", 401);
        }

        var affected = await _cartRepo.SetSelectedAsync(memberId, dto.Selected, dto.Id);
        if (affected == 0)
        {
            return ApiResponse<bool>.Fail("购物车商品不存在", 404);
        }
        return ApiResponse<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<ApiResponse<bool>> ToggleAllAsync(bool selected)
    {
        var memberId = _currentUser.MemberId;
        if (memberId <= 0)
        {
            return ApiResponse<bool>.Fail("未获取到登录会员信息", 401);
        }

        await _cartRepo.SetSelectedAsync(memberId, selected, null);
        return ApiResponse<bool>.Success(true);
    }

    // ============================================================
    // 私有工具
    // ============================================================

    /// <summary>
    /// 构造购物车唯一键：会员 + 商品 + SKU + 是否需要加工 + 验光哈希。
    /// 任意维度不同都会得到不同的 key。
    /// </summary>
    private static string BuildCartKey(long memberId, long prodInfoId, long skuId, int processingType, string rxHash)
        => $"{memberId}_{prodInfoId}_{skuId}_{processingType}_{rxHash}";

    /// <summary>规范化验光数据：输出可读 JSON（存库）+ SHA256 哈希（唯一键）</summary>
    private static (string Json, string Hash) Canonicalize(CartPrescriptionDto? rx, int processingType)
    {
        // 不需要加工：不存验光、哈希固定 NORX（保证多次「只发片」能正确合并）
        if (processingType != 1 || rx == null)
        {
            return (string.Empty, "NORX");
        }

        static string Norm(string? v) => string.IsNullOrWhiteSpace(v) ? "-" : v.Trim();

        static string Eye(CartPrescriptionEyeDto? e) => e == null
            ? "-/-/-/-/-"
            : $"{Norm(e.Sphere)}/{Norm(e.Cylinder)}/{Norm(e.Axis)}/{Norm(e.Pd)}/{Norm(e.Height)}";

        var canonical = $"L:{Eye(rx.Left)}|R:{Eye(rx.Right)}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        var json = JsonSerializer.Serialize(rx, RxJsonOptions);
        return (json, hash);
    }

    /// <summary>验光数据是否至少有一项有效内容</summary>
    private static bool HasAnyPrescription(CartPrescriptionDto rx)
    {
        static bool Has(CartPrescriptionEyeDto? e)
            => e != null && (!string.IsNullOrWhiteSpace(e.Sphere)
                             || !string.IsNullOrWhiteSpace(e.Cylinder)
                             || !string.IsNullOrWhiteSpace(e.Axis));

        return Has(rx.Left) || Has(rx.Right);
    }

    /// <summary>解析库存 JSON 为验光对象（损坏数据返回 null，不影响列表展示）</summary>
    private static CartPrescriptionDto? ParsePrescription(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<CartPrescriptionDto>(json, RxJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>按会员身份解析各 SKU 成交单价（失败时回退标准价，不阻塞购物车）</summary>
    private async Task<Dictionary<long, decimal>> ResolvePricesAsync(long memberId, List<MemCart> rows)
    {
        var map = new Dictionary<long, decimal>();
        try
        {
            var request = new QuotationRequestDto
            {
                MemMemberId = memberId,
                Items = rows
                    .GroupBy(r => r.SkuId)
                    .Select(g => new QuotationItemInputDto { MaterialId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                    .ToList()
            };

            var resp = await _priceService.QuotationAsync(request);
            if (resp.Code == 200 && resp.Data != null)
            {
                foreach (var q in resp.Data)
                {
                    map[q.MaterialId] = q.FinalPrice;
                }
            }
        }
        catch
        {
            // 取价异常不影响购物车列表（前端会回退到 SKU 标准价）
        }
        return map;
    }
}
