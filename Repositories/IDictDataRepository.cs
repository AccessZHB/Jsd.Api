using Jsd.Api.Entities;
using Jsd.Api.Models.Dict;

namespace Jsd.Api.Repositories;

/// <summary>字典数据仓储接口</summary>
public interface IDictDataRepository : IRepository<SysDictData>
{
    /// <summary>分页查询字典数据（支持类型ID / 类型编码 / 标签模糊 / 状态筛选）</summary>
    Task<(List<SysDictData> Items, int Total)> GetPagedListAsync(DictDataQueryDto query);

    /// <summary>
    /// 按字典类型编码取「启用」的数据列表（只读缓存的接口走这里）
    /// 返回顺序：dict_sort 升序 → id 升序，保证前端下拉顺序稳定。
    /// </summary>
    Task<List<SysDictData>> GetEnabledListByDictTypeAsync(string dictType);

    /// <summary>获取所有启用中字典类型的编码集合（应用启动预热缓存用）</summary>
    Task<List<string>> GetEnabledTypeCodesAsync();

    /// <summary>按字典类型ID批量删除（删字典类型时级联用，返回删除条数）</summary>
    Task<int> DeleteByDictTypeIdAsync(long dictTypeId);

    /// <summary>按主键批量删除，返回删除条数</summary>
    Task<int> DeleteByIdsAsync(List<long> ids);
}
