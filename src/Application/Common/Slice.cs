namespace Application.Common;

public sealed record Slice<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public string? StartCursor { get; init; }
    public string? EndCursor { get; init; }
    public bool HasNextPage { get; init; }
    public bool HasPreviousPage { get; init; }
    public int TotalCount { get; init; }
}