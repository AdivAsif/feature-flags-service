namespace Application.DTOs;

public class PagedFeatureFlagDTO
{
    public IEnumerable<FeatureFlagDTO> Items { get; set; } = [];
    public PaginationInfo PageInfo { get; set; } = new();
}

public class PaginationInfo
{
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
    public string? StartCursor { get; set; }
    public string? EndCursor { get; set; }
    public int TotalCount { get; set; }
}