namespace Nomax.SharedKernel.Paging;

/// <summary>
/// Page parameters for list endpoints: <c>?page=1&amp;pageSize=20</c>, at most 100 items per page (docs/05 §1).
/// Out-of-range values are clamped instead of rejected so that a stale link still returns a page.
/// </summary>
public sealed record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    // Keeps Skip within int range.
    private const int MaxPage = int.MaxValue / MaxPageSize;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;

    public static PageRequest Create(int? page, int? pageSize) =>
        new(Math.Clamp(page ?? 1, 1, MaxPage), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}
