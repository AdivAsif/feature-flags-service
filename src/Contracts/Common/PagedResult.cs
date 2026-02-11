namespace Contracts.Common;

public sealed record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public PageInfo PageInfo { get; init; } = new();
}

public sealed record PageInfo
{
    public string? StartCursor { get; init; }
    public string? EndCursor { get; init; }
    public bool HasNextPage { get; init; }
    public bool HasPreviousPage { get; init; }
    public int TotalCount { get; init; }
}