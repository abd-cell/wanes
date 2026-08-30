namespace Wanes.Shareds.Models;

/// <summary>Standard paged-list request. Paging is 1-based.</summary>
public class PageInput
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;
    private int _pageNumber = 1;

    /// <summary>1-based page number.</summary>
    public int PageNumber
    {
        get => _pageNumber < 1 ? 1 : _pageNumber;
        set => _pageNumber = value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > MaxPageSize ? MaxPageSize : value;
    }

    /// <summary>Optional free-text search term applied to [Searchable] fields.</summary>
    public string? Search { get; set; }

    public int Skip => (PageNumber - 1) * PageSize;
}

/// <summary>Standard paged-list response (matches the CMS AdminPage&lt;T&gt; shape).</summary>
public class PageOutput<T>
{
    public IReadOnlyList<T> Data { get; set; } = [];
    public int TotalRows { get; set; }
}
