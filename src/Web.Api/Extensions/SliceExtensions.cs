using Application.Common;
using Contracts.Common;

namespace Web.Api.Extensions;

public static class SliceExtensions
{
    public static PagedResult<T> ToPagedResult<T>(this Slice<T> slice)
    {
        return new PagedResult<T>
        {
            Items = slice.Items,
            PageInfo = new PageInfo
            {
                StartCursor = slice.StartCursor,
                EndCursor = slice.EndCursor,
                HasNextPage = slice.HasNextPage,
                HasPreviousPage = slice.HasPreviousPage,
                TotalCount = slice.TotalCount
            }
        };
    }
}