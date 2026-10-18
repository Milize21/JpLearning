using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Paging;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Content.Features.Reference;

public sealed record ListReferenceQuery(
    [property: FromQuery(Name = "levelId")] Guid? LevelId,
    [property: FromQuery(Name = "chapterId")] Guid? ChapterId,
    [property: FromQuery(Name = "page")] int? Page,
    [property: FromQuery(Name = "pageSize")] int? PageSize);

/// <summary>
/// GET /api/content/reference/{type} — all roles. Students only see items of chapters unlocked for them; items without
/// a chapter are staff-only (docs/05 §5). Filtered and paged in SQL.
/// </summary>
internal sealed class ListReferenceHandler(ContentDbContext db, ChapterGate gate)
{
    public Task<Result<PagedResult<KanaDto>>> KanaAsync(ListReferenceQuery query, CancellationToken cancellationToken) =>
        ListAsync(db.KanaChars, query, items => items.OrderBy(item => item.Script).ThenBy(item => item.SortOrder), KanaDto.From, cancellationToken);

    public Task<Result<PagedResult<KanjiDto>>> KanjiAsync(ListReferenceQuery query, CancellationToken cancellationToken) =>
        ListAsync(db.Kanji, query, items => items.OrderBy(item => item.Character), KanjiDto.From, cancellationToken);

    public Task<Result<PagedResult<VocabDto>>> VocabAsync(ListReferenceQuery query, CancellationToken cancellationToken) =>
        ListAsync(db.Vocab, query, items => items.OrderBy(item => item.Reading), VocabDto.From, cancellationToken);

    public Task<Result<PagedResult<GrammarPointDto>>> GrammarAsync(ListReferenceQuery query, CancellationToken cancellationToken) =>
        ListAsync(db.GrammarPoints, query, items => items.OrderBy(item => item.Pattern), GrammarPointDto.From, cancellationToken);

    public Task<Result<PagedResult<SentenceDto>>> SentencesAsync(ListReferenceQuery query, CancellationToken cancellationToken) =>
        ListAsync(db.Sentences, query, items => items.OrderBy(item => item.CreatedAt), SentenceDto.From, cancellationToken);

    private async Task<Result<PagedResult<TDto>>> ListAsync<TItem, TDto>(
        IQueryable<TItem> source,
        ListReferenceQuery query,
        Func<IQueryable<TItem>, IOrderedQueryable<TItem>> order,
        Func<TItem, TDto> map,
        CancellationToken cancellationToken)
        where TItem : ReferenceItem
    {
        var items = source.AsNoTracking();
        if (query.LevelId is { } levelId)
        {
            items = items.Where(item => item.LevelId == levelId);
        }

        if (query.ChapterId is { } chapterId)
        {
            if (await gate.CheckAsync(chapterId, cancellationToken) is { } locked)
            {
                return locked;
            }

            items = items.Where(item => item.ChapterId == chapterId);
        }

        if (await gate.AccessibleChaptersAsync(cancellationToken) is { } accessible)
        {
            var chapterIds = accessible.ToList();
            items = items.Where(item => item.ChapterId != null && chapterIds.Contains(item.ChapterId.Value));
        }

        var page = PageRequest.Create(query.Page, query.PageSize);
        var total = await items.CountAsync(cancellationToken);
        var rows = await order(items).ThenBy(item => item.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<TDto>([.. rows.Select(map)], page.Page, page.PageSize, total);
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        Configure(group.MapGet("/reference/kana", async ([AsParameters] ListReferenceQuery query, ListReferenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.KanaAsync(query, cancellationToken)).ToOkResult()), "ListKana")
            .Produces<PagedResult<KanaDto>>();
        Configure(group.MapGet("/reference/kanji", async ([AsParameters] ListReferenceQuery query, ListReferenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.KanjiAsync(query, cancellationToken)).ToOkResult()), "ListKanji")
            .Produces<PagedResult<KanjiDto>>();
        Configure(group.MapGet("/reference/vocab", async ([AsParameters] ListReferenceQuery query, ListReferenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.VocabAsync(query, cancellationToken)).ToOkResult()), "ListVocab")
            .Produces<PagedResult<VocabDto>>();
        Configure(group.MapGet("/reference/grammar", async ([AsParameters] ListReferenceQuery query, ListReferenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.GrammarAsync(query, cancellationToken)).ToOkResult()), "ListGrammarPoints")
            .Produces<PagedResult<GrammarPointDto>>();
        Configure(group.MapGet("/reference/sentences", async ([AsParameters] ListReferenceQuery query, ListReferenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.SentencesAsync(query, cancellationToken)).ToOkResult()), "ListSentences")
            .Produces<PagedResult<SentenceDto>>();
    }

    private static RouteHandlerBuilder Configure(RouteHandlerBuilder builder, string name) =>
        builder
            .WithName(name)
            .WithSummary("Reference data, filtered by level and chapter; students only see unlocked chapters")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
