using System.Text.Json.Serialization;

namespace ProCargo.Application.Common;

/// <summary>Common query-string parameters for every paged list endpoint.</summary>
public class PagedRequest
{
    public const int MaxPageSize = 100;

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }

    /// <summary>Clamps paging values and whitelists sort fields so the stored procedure only ever sees safe values.</summary>
    public (int PageNumber, int PageSize, string? Search, string SortBy, string SortDirection) Normalize(
        string defaultSort, params string[] allowedSorts)
    {
        var page = PageNumber < 1 ? 1 : PageNumber;
        var size = PageSize switch { < 1 => 20, > MaxPageSize => MaxPageSize, _ => PageSize };
        var sort = SortBy is not null && allowedSorts.Contains(SortBy, StringComparer.OrdinalIgnoreCase)
            ? allowedSorts.First(s => string.Equals(s, SortBy, StringComparison.OrdinalIgnoreCase))
            : defaultSort;
        var direction = string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        var search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim()[..Math.Min(Search.Trim().Length, 100)];
        return (page, size, search, sort, direction);
    }
}

/// <summary>Standard paged response: { items, pageNumber, pageSize, totalRecords, totalPages }.</summary>
public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalRecords)
    {
        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalRecords = totalRecords;
    }

    public IReadOnlyList<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalRecords { get; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalRecords / (double)PageSize);

    public static PagedResult<T> Empty(int pageNumber, int pageSize) => new([], pageNumber, pageSize, 0);
}

/// <summary>
/// Base for rows returned by paged stored procedures. The procedure adds COUNT(*) OVER () as TotalRecords
/// to every row so one round trip returns both the page and the total.
/// </summary>
public abstract class PagedRow
{
    [JsonIgnore]
    public int TotalRecords { get; init; }
}

public static class PagedResultExtensions
{
    public static PagedResult<T> ToPagedResult<T>(this IReadOnlyList<T> rows, int pageNumber, int pageSize)
        where T : PagedRow =>
        new(rows, pageNumber, pageSize, rows.Count == 0 ? 0 : rows[0].TotalRecords);
}
