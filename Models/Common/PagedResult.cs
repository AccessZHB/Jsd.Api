namespace Jsd.Api.Models.Common;

/// <summary>
/// 分页查询结果
/// </summary>
/// <typeparam name="T">列表项类型</typeparam>
public class PagedResult<T>
{
    /// <summary>当前页数据列表</summary>
    public List<T> List { get; set; } = new();

    /// <summary>总条数</summary>
    public int Total { get; set; }

    /// <summary>当前页码</summary>
    public int Page { get; set; }

    /// <summary>每页条数</summary>
    public int PageSize { get; set; }

    /// <summary>快速构造分页结果</summary>
    public static PagedResult<T> Create(List<T> list, int total, int page, int pageSize)
        => new() { List = list, Total = total, Page = page, PageSize = pageSize };
}
