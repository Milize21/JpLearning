using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Features.Reference;

// PATCH /api/content/reference/{type}/{id} — admin. Only the fields that are present change. Mapping an item to a
// chapter (ChapterId) makes it available to the question generator (BR-KONTEN-04); UnmapChapter removes the mapping.

public sealed record UpdateKanaRequest(
    Guid? ChapterId = null, bool UnmapChapter = false, string? Source = null, string? License = null,
    string? Romaji = null, string? RowKey = null, KanaVariant? Variant = null, int? SortOrder = null);

public sealed record UpdateKanjiRequest(
    Guid? ChapterId = null, bool UnmapChapter = false, string? Source = null, string? License = null,
    IReadOnlyList<string>? Onyomi = null, IReadOnlyList<string>? Kunyomi = null, Meanings? Meanings = null, short? StrokeCount = null);

public sealed record UpdateVocabRequest(
    Guid? ChapterId = null, bool UnmapChapter = false, string? Source = null, string? License = null,
    Meanings? Meanings = null, string? Pos = null, IReadOnlyList<string>? Tags = null);

public sealed record UpdateGrammarPointRequest(
    Guid? ChapterId = null, bool UnmapChapter = false, string? Source = null, string? License = null,
    TextBlock? Title = null, RichText? Explanation = null, IReadOnlyList<string>? Tags = null);

public sealed record UpdateSentenceRequest(
    Guid? ChapterId = null, bool UnmapChapter = false, string? Source = null, string? License = null,
    TextBlock? Text = null, IReadOnlyList<string>? Tags = null);

internal static class ReferenceUpdateRules
{
    public static void Common<T>(AbstractValidator<T> validator, Func<T, string?> source, Func<T, string?> license)
    {
        validator.RuleFor(request => source(request))
            .Must(value => value is null || value.Trim().Length is > 0 and <= ReferenceItem.SourceMaxLength)
            .OverridePropertyName("source")
            .WithMessage($"Must be 1–{ReferenceItem.SourceMaxLength} characters.");
        validator.RuleFor(request => license(request))
            .Must(value => value is null || value.Trim().Length is > 0 and <= ReferenceItem.LicenseMaxLength)
            .OverridePropertyName("license")
            .WithMessage($"Must be 1–{ReferenceItem.LicenseMaxLength} characters.");
    }

    public static bool ValidList(IReadOnlyList<string>? items) => items is null || items.All(item => item.Trim().Length is > 0 and <= 100);

    public static bool ValidMeanings(Meanings? meanings) =>
        meanings is null || (meanings.Indonesian.Count > 0 && ValidList(meanings.Indonesian) && ValidList(meanings.English));
}

internal sealed class UpdateKanaValidator : AbstractValidator<UpdateKanaRequest>
{
    public UpdateKanaValidator()
    {
        ReferenceUpdateRules.Common(this, request => request.Source, request => request.License);
        RuleFor(request => request.Romaji).Must(value => value is null || (value.Length is > 0 and <= 8 && value.All(char.IsAsciiLetterLower)));
        RuleFor(request => request.RowKey).Must(value => value is null || value.Length is > 0 and <= 8);
        RuleFor(request => request.Variant).IsInEnum();
        RuleFor(request => request.SortOrder).GreaterThanOrEqualTo(0).When(request => request.SortOrder is not null);
    }
}

internal sealed class UpdateKanjiValidator : AbstractValidator<UpdateKanjiRequest>
{
    public UpdateKanjiValidator()
    {
        ReferenceUpdateRules.Common(this, request => request.Source, request => request.License);
        RuleFor(request => request.Onyomi).Must(items => items is null || items.All(JapaneseScript.IsReading)).WithMessage("Katakana readings only.");
        RuleFor(request => request.Kunyomi)
            .Must(items => items is null || items.All(item => JapaneseScript.IsReading(item.Replace(".", string.Empty, StringComparison.Ordinal))))
            .WithMessage("Hiragana readings only; okurigana after a dot.");
        RuleFor(request => request.Meanings).Must(ReferenceUpdateRules.ValidMeanings).WithMessage("At least one Indonesian meaning; each at most 100 characters.");
        RuleFor(request => request.StrokeCount).InclusiveBetween((short)1, (short)100).When(request => request.StrokeCount is not null);
    }
}

internal sealed class UpdateVocabValidator : AbstractValidator<UpdateVocabRequest>
{
    public UpdateVocabValidator()
    {
        ReferenceUpdateRules.Common(this, request => request.Source, request => request.License);
        RuleFor(request => request.Meanings).Must(ReferenceUpdateRules.ValidMeanings).WithMessage("At least one Indonesian meaning; each at most 100 characters.");
        RuleFor(request => request.Pos).MaximumLength(32);
        RuleFor(request => request.Tags).Must(ReferenceUpdateRules.ValidList).WithMessage("Tags are 1–100 characters.");
    }
}

internal sealed class UpdateGrammarPointValidator : AbstractValidator<UpdateGrammarPointRequest>
{
    public UpdateGrammarPointValidator()
    {
        ReferenceUpdateRules.Common(this, request => request.Source, request => request.License);
        RuleFor(request => request.Title).ValidTextBlock(TextBlockUsage.Title).When(request => request.Title is not null);
        RuleFor(request => request.Explanation).ValidRichText().When(request => request.Explanation is not null);
        RuleFor(request => request.Tags).Must(ReferenceUpdateRules.ValidList).WithMessage("Tags are 1–100 characters.");
    }
}

internal sealed class UpdateSentenceValidator : AbstractValidator<UpdateSentenceRequest>
{
    public UpdateSentenceValidator()
    {
        ReferenceUpdateRules.Common(this, request => request.Source, request => request.License);
        RuleFor(request => request.Text).ValidTextBlock(TextBlockUsage.Sentence).When(request => request.Text is not null);
        RuleFor(request => request.Tags).Must(ReferenceUpdateRules.ValidList).WithMessage("Tags are 1–100 characters.");
    }
}

internal sealed class UpdateReferenceHandler(ContentDbContext db)
{
    public Task<Result<KanaDto>> KanaAsync(Guid id, UpdateKanaRequest request, CancellationToken cancellationToken) =>
        UpdateAsync(db.KanaChars, id, request.ChapterId, request.UnmapChapter, request.Source, request.License,
            item => item.Update(request.Romaji, request.RowKey, request.Variant, request.SortOrder), KanaDto.From, cancellationToken);

    public Task<Result<KanjiDto>> KanjiAsync(Guid id, UpdateKanjiRequest request, CancellationToken cancellationToken) =>
        UpdateAsync(db.Kanji, id, request.ChapterId, request.UnmapChapter, request.Source, request.License,
            item => item.Update(request.Onyomi, request.Kunyomi, request.Meanings, request.StrokeCount), KanjiDto.From, cancellationToken);

    public Task<Result<VocabDto>> VocabAsync(Guid id, UpdateVocabRequest request, CancellationToken cancellationToken) =>
        UpdateAsync(db.Vocab, id, request.ChapterId, request.UnmapChapter, request.Source, request.License,
            item => item.Update(request.Meanings, request.Pos, request.Tags), VocabDto.From, cancellationToken);

    public Task<Result<GrammarPointDto>> GrammarAsync(Guid id, UpdateGrammarPointRequest request, CancellationToken cancellationToken) =>
        UpdateAsync(db.GrammarPoints, id, request.ChapterId, request.UnmapChapter, request.Source, request.License,
            item => item.Update(request.Title, request.Explanation, request.Tags), GrammarPointDto.From, cancellationToken);

    public Task<Result<SentenceDto>> SentenceAsync(Guid id, UpdateSentenceRequest request, CancellationToken cancellationToken) =>
        UpdateAsync(db.Sentences, id, request.ChapterId, request.UnmapChapter, request.Source, request.License,
            item => item.Update(request.Text, request.Tags), SentenceDto.From, cancellationToken);

    private async Task<Result<TDto>> UpdateAsync<TItem, TDto>(
        DbSet<TItem> items,
        Guid id,
        Guid? chapterId,
        bool unmapChapter,
        string? source,
        string? license,
        Action<TItem> apply,
        Func<TItem, TDto> map,
        CancellationToken cancellationToken)
        where TItem : ReferenceItem
    {
        var item = await items.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (item is null)
        {
            return AppError.NotFound();
        }

        if (unmapChapter)
        {
            item.MapToChapter(null);
        }
        else if (chapterId is { } newChapter)
        {
            // The chapter must belong to a book of the item's level.
            var sameLevel = await db.Chapters.AsNoTracking()
                .Where(chapter => chapter.Id == newChapter)
                .Join(db.Books, chapter => chapter.BookId, book => book.Id, (_, book) => book.LevelId)
                .AnyAsync(levelId => levelId == item.LevelId, cancellationToken);
            if (!sameLevel)
            {
                return ContentErrors.UnknownReference("chapterId");
            }

            item.MapToChapter(newChapter);
        }

        if (source is not null || license is not null)
        {
            item.ChangeSource(source?.Trim() ?? item.Source, license?.Trim() ?? item.License);
        }

        apply(item);
        await db.SaveChangesAsync(cancellationToken);
        return map(item);
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        Configure(group.MapPatch("/reference/kana/{id:guid}", async (Guid id, UpdateKanaRequest request, UpdateReferenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.KanaAsync(id, request, cancellationToken)).ToOkResult()), "UpdateKana")
            .WithValidation<UpdateKanaRequest>().Produces<KanaDto>();
        Configure(group.MapPatch("/reference/kanji/{id:guid}", async (Guid id, UpdateKanjiRequest request, UpdateReferenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.KanjiAsync(id, request, cancellationToken)).ToOkResult()), "UpdateKanji")
            .WithValidation<UpdateKanjiRequest>().Produces<KanjiDto>();
        Configure(group.MapPatch("/reference/vocab/{id:guid}", async (Guid id, UpdateVocabRequest request, UpdateReferenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.VocabAsync(id, request, cancellationToken)).ToOkResult()), "UpdateVocab")
            .WithValidation<UpdateVocabRequest>().Produces<VocabDto>();
        Configure(group.MapPatch("/reference/grammar/{id:guid}", async (Guid id, UpdateGrammarPointRequest request, UpdateReferenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.GrammarAsync(id, request, cancellationToken)).ToOkResult()), "UpdateGrammarPoint")
            .WithValidation<UpdateGrammarPointRequest>().Produces<GrammarPointDto>();
        Configure(group.MapPatch("/reference/sentences/{id:guid}", async (Guid id, UpdateSentenceRequest request, UpdateReferenceHandler handler, CancellationToken cancellationToken) =>
                (await handler.SentenceAsync(id, request, cancellationToken)).ToOkResult()), "UpdateSentence")
            .WithValidation<UpdateSentenceRequest>().Produces<SentenceDto>();
    }

    private static RouteHandlerBuilder Configure(RouteHandlerBuilder builder, string name) =>
        builder
            .RequireAuthorization(Policies.AdminOnly)
            .WithName(name)
            .WithSummary("Change a reference data item or its chapter mapping (admin)")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
