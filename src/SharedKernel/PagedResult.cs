namespace SharedKernel;

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = [];
    public PageInfo PageInfo { get; set; } = new();
}

public class PageInfo
{
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
    public string? StartCursor { get; set; }
    public string? EndCursor { get; set; }
    public int TotalCount { get; set; }
}