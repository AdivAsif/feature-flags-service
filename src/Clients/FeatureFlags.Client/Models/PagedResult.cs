namespace FeatureFlags.Client;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public PaginationInfo PageInfo { get; init; } = new();
}

public sealed class PaginationInfo
{
    public bool HasNextPage { get; init; }
    public bool HasPreviousPage { get; init; }
    public string? StartCursor { get; init; }
    public string? EndCursor { get; init; }
    public int TotalCount { get; init; }
}