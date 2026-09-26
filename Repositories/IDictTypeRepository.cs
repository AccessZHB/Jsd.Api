using Jsd.Api.Entities;
using Jsd.Api.Models.Dict;

namespace Jsd.Api.Repositories;

/// <summary>
/// 字典类型仓储接口
/// （分页统一在数据库层用 Skip/Take 完成，禁止在内存中分页）
/// </summary>
public interface IDictTypeRepository : IRepository<SysDictType>
{
    /// <summary>分页查询字典类型（支持名称/编码模糊、状态筛选）</summary>
    Task<(List<SysDictType> Items, int Total)> GetPagedListAsync(DictTypeQueryDto query);

    /// <summary>统计指定几个字典类型下的字典数据条数，返回 类型ID → 条数</summary>
    Task<Dictionary<long, int>> GetDataCountMapAsync(List<long> dictTypeIds);

    /// <summary>判断某个类型编码已存在（排除自身 id，用于修改时排除自己）</summary>
    Task<bool> ExistsAsync(string dictType, long excludeId);
}
