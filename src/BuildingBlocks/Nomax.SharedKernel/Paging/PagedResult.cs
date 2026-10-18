namespace Nomax.SharedKernel.Paging;

/// <summary>
/// One page of a list response: <c>{ items, page, pageSize, totalCount }</c> (docs/05 §1).
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
