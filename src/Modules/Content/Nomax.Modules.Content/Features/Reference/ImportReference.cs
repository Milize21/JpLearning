using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Content.Domain;
using Nomax.Modules.Content.Features.Common;
using Nomax.Modules.Content.Infrastructure;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Serialization;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Content.Features.Reference;

/// <summary>
/// Multipart form of the import. <see cref="Source"/> and <see cref="License"/> are mandatory for every row
/// (AGENTS.md §12). <see cref="ChapterId"/> maps all rows to one chapter; a <c>chapter</c> column (chapter numbers of
/// <see cref="BookId"/>) maps rows individually.
/// </summary>
public sealed class ImportReferenceForm
{
    public IFormFile? File { get; set; }

    public string? Source { get; set; }

    public string? License { get; set; }

    public Guid? LevelId { get; set; }

    public Guid? BookId { get; set; }

    public Guid? ChapterId { get; set; }
}

/// <param name="Errors">Cell errors; when present nothing was created. Codes are in <see cref="ReferenceImport.Codes"/>.</param>
/// <param name="Skipped">Rows that already exist (re-importing a file is safe).</param>
public sealed record ImportReferenceResponse(bool DryRun, int TotalRows, IReadOnlyList<ImportCellError> Errors, int ToCreate, int Skipped, int Created);

/// <summary>POST /api/content/reference/{type}/import?dryRun=true|false — admin (docs/05 §5).</summary>
internal sealed class ImportReferenceHandler(ContentDbContext db, TimeProvider timeProvider)
{
    public async Task<Result<ImportReferenceResponse>> HandleAsync(
        ReferenceType type,
        ImportReferenceForm form,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var context = await ValidateFormAsync(form, cancellationToken);
        if (!context.IsSuccess)
        {
            return context.Error;
        }

        string text;
        using (var reader = new StreamReader(form.File!.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync(cancellationToken);
        }

        var errors = new List<ImportCellError>();
        var rows = ReferenceImport.Read(text, type, errors);
        var plan = type switch
        {
            ReferenceType.Kana => await PlanKanaAsync(ReferenceImport.ParseKana(rows, errors), context.Value, errors, cancellationToken),
            ReferenceType.Kanji => await PlanKanjiAsync(ReferenceImport.ParseKanji(rows, errors), context.Value, errors, cancellationToken),
            ReferenceType.Vocab => await PlanVocabAsync(ReferenceImport.ParseVocab(rows, errors), context.Value, errors, cancellationToken),
            ReferenceType.Grammar => await PlanGrammarAsync(ReferenceImport.ParseGrammar(rows, errors), context.Value, errors, cancellationToken),
            _ => await PlanSentencesAsync(ReferenceImport.ParseSentences(rows, errors), context.Value, errors, cancellationToken),
        };

        var sortedErrors = errors.OrderBy(error => error.Row).ThenBy(error => error.Column, StringComparer.Ordinal).ToList();
        if (sortedErrors.Count > 0 || dryRun)
        {
            return new ImportReferenceResponse(dryRun, rows.Count, sortedErrors, sortedErrors.Count > 0 ? 0 : plan.ToCreate.Count, plan.Skipped, 0);
        }

        db.AddRange(plan.ToCreate);
        await db.SaveChangesAsync(cancellationToken);
        return new ImportReferenceResponse(false, rows.Count, [], plan.ToCreate.Count, plan.Skipped, plan.ToCreate.Count);
    }

    private sealed record ImportContext(Guid LevelId, string Source, string License, Guid? DefaultChapterId, IReadOnlyDictionary<int, Guid> ChaptersByNumber);

    private sealed record ImportPlan(IReadOnlyList<ReferenceItem> ToCreate, int Skipped);

    private async Task<Result<ImportContext>> ValidateFormAsync(ImportReferenceForm form, CancellationToken cancellationToken)
    {
        var fieldErrors = new Dictionary<string, string[]>();
        if (form.File is null || form.File.Length == 0)
        {
            fieldErrors["file"] = ["A CSV or JSON file is required."];
        }

        var source = form.Source?.Trim() ?? string.Empty;
        var license = form.License?.Trim() ?? string.Empty;
        if (source.Length is 0 or > ReferenceItem.SourceMaxLength)
        {
            fieldErrors["source"] = [$"Is required (at most {ReferenceItem.SourceMaxLength} characters)."];
        }

        if (license.Length is 0 or > ReferenceItem.LicenseMaxLength)
        {
            fieldErrors["license"] = [$"Is required (at most {ReferenceItem.LicenseMaxLength} characters)."];
        }

        if (form.LevelId is not { } levelId || !await db.Levels.AnyAsync(level => level.Id == levelId, cancellationToken))
        {
            fieldErrors["levelId"] = ["Does not exist."];
            return AppError.Validation(fieldErrors);
        }

        var chaptersByNumber = new Dictionary<int, Guid>();
        if (form.BookId is { } bookId)
        {
            if (!await db.Books.AnyAsync(book => book.Id == bookId && book.LevelId == levelId, cancellationToken))
            {
                fieldErrors["bookId"] = ["Must be a book of the level."];
            }
            else
            {
                chaptersByNumber = await db.Chapters.AsNoTracking()
                    .Where(chapter => chapter.BookId == bookId)
                    .ToDictionaryAsync(chapter => chapter.Number, chapter => chapter.Id, cancellationToken);
            }
        }

        if (form.ChapterId is { } chapterId)
        {
            var inLevel = await db.Chapters.AsNoTracking()
                .Where(chapter => chapter.Id == chapterId)
                .Join(db.Books, chapter => chapter.BookId, book => book.Id, (chapter, book) => book.LevelId)
                .AnyAsync(level => level == levelId, cancellationToken);
            if (!inLevel)
            {
                fieldErrors["chapterId"] = ["Must be a chapter of the level."];
            }
        }

        return fieldErrors.Count > 0
            ? AppError.Validation(fieldErrors)
            : new ImportContext(levelId, source, license, form.ChapterId, chaptersByNumber);
    }

    private static Guid? ResolveChapter(int row, int? number, ImportContext context, List<ImportCellError> errors)
    {
        if (number is null)
        {
            return context.DefaultChapterId;
        }

        if (context.ChaptersByNumber.TryGetValue(number.Value, out var chapterId))
        {
            return chapterId;
        }

        errors.Add(new ImportCellError(row, ReferenceImport.ChapterColumn, ReferenceImport.Codes.UnknownChapter));
        return null;
    }

    private async Task<ImportPlan> PlanKanaAsync(
        IReadOnlyList<KanaImportRow> rows, ImportContext context, List<ImportCellError> errors, CancellationToken cancellationToken)
    {
        var chars = rows.Select(row => row.Character).ToList();
        var existing = (await db.KanaChars.AsNoTracking()
                .Where(item => chars.Contains(item.Character))
                .Select(item => new { item.Script, item.Character })
                .ToListAsync(cancellationToken))
            .Select(item => $"{item.Script}:{item.Character}")
            .ToHashSet(StringComparer.Ordinal);

        var create = new List<ReferenceItem>();
        var skipped = 0;
        foreach (var row in rows)
        {
            var chapterId = ResolveChapter(row.Row, row.Chapter, context, errors);
            if (existing.Contains($"{row.Script}:{row.Character}"))
            {
                skipped++;
                continue;
            }

            create.Add(KanaChar.Create(
                Uuid7.New(timeProvider), context.LevelId, chapterId, context.Source, context.License,
                row.Character, row.Script, row.Romaji, row.RowKey, row.Variant, row.SortOrder));
        }

        return new ImportPlan(create, skipped);
    }

    private async Task<ImportPlan> PlanKanjiAsync(
        IReadOnlyList<KanjiImportRow> rows, ImportContext context, List<ImportCellError> errors, CancellationToken cancellationToken)
    {
        var characters = rows.Select(row => row.Character).ToList();
        var existing = (await db.Kanji.AsNoTracking()
                .Where(item => characters.Contains(item.Character))
                .Select(item => item.Character)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var create = new List<ReferenceItem>();
        var skipped = 0;
        foreach (var row in rows)
        {
            var chapterId = ResolveChapter(row.Row, row.Chapter, context, errors);
            if (existing.Contains(row.Character))
            {
                skipped++;
                continue;
            }

            create.Add(Kanji.Create(
                Uuid7.New(timeProvider), context.LevelId, chapterId, context.Source, context.License,
                row.Character, row.Onyomi, row.Kunyomi, row.Meanings, row.StrokeCount, row.KanjivgCode));
        }

        return new ImportPlan(create, skipped);
    }

    private async Task<ImportPlan> PlanVocabAsync(
        IReadOnlyList<VocabImportRow> rows, ImportContext context, List<ImportCellError> errors, CancellationToken cancellationToken)
    {
        var words = rows.Select(row => row.Word).ToList();
        var existing = (await db.Vocab.AsNoTracking()
                .Where(item => item.LevelId == context.LevelId && words.Contains(item.Word))
                .Select(item => new { item.Word, item.Reading })
                .ToListAsync(cancellationToken))
            .Select(item => $"{item.Word}\u0000{item.Reading}")
            .ToHashSet(StringComparer.Ordinal);

        var create = new List<ReferenceItem>();
        var skipped = 0;
        foreach (var row in rows)
        {
            var chapterId = ResolveChapter(row.Row, row.Chapter, context, errors);
            if (existing.Contains($"{row.Word}\u0000{row.Reading}"))
            {
                skipped++;
                continue;
            }

            create.Add(Vocab.Create(
                Uuid7.New(timeProvider), context.LevelId, chapterId, context.Source, context.License,
                row.Word, row.Reading, row.Meanings, row.Pos, row.Tags, row.JmdictSeq));
        }

        return new ImportPlan(create, skipped);
    }

    private async Task<ImportPlan> PlanGrammarAsync(
        IReadOnlyList<GrammarImportRow> rows, ImportContext context, List<ImportCellError> errors, CancellationToken cancellationToken)
    {
        var patterns = rows.Select(row => row.Pattern).ToList();
        var existing = (await db.GrammarPoints.AsNoTracking()
                .Where(item => item.LevelId == context.LevelId && patterns.Contains(item.Pattern))
                .Select(item => item.Pattern)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var create = new List<ReferenceItem>();
        var skipped = 0;
        foreach (var row in rows)
        {
            var chapterId = ResolveChapter(row.Row, row.Chapter, context, errors);
            if (existing.Contains(row.Pattern))
            {
                skipped++;
                continue;
            }

            create.Add(GrammarPoint.Create(
                Uuid7.New(timeProvider), context.LevelId, chapterId, context.Source, context.License,
                row.Pattern, row.Title, row.Explanation, row.Tags));
        }

        return new ImportPlan(create, skipped);
    }

    private async Task<ImportPlan> PlanSentencesAsync(
        IReadOnlyList<SentenceImportRow> rows, ImportContext context, List<ImportCellError> errors, CancellationToken cancellationToken)
    {
        var grammar = await db.GrammarPoints.AsNoTracking()
            .Where(item => item.LevelId == context.LevelId)
            .Select(item => new { item.Pattern, item.Id })
            .ToListAsync(cancellationToken);
        var grammarByPattern = grammar.GroupBy(item => item.Pattern, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);

        // Existing sentences of the level, compared by their displayed Japanese text.
        var existing = (await db.Sentences.AsNoTracking()
                .Where(item => item.LevelId == context.LevelId)
                .Select(item => item.Text)
                .ToListAsync(cancellationToken))
            .Select(text => FuriganaMarkup.PlainText(text.Japanese ?? []))
            .ToHashSet(StringComparer.Ordinal);

        var create = new List<ReferenceItem>();
        var skipped = 0;
        foreach (var row in rows)
        {
            var chapterId = ResolveChapter(row.Row, row.Chapter, context, errors);
            Guid? grammarPointId = null;
            if (row.GrammarPattern is { } pattern)
            {
                if (grammarByPattern.TryGetValue(pattern, out var id))
                {
                    grammarPointId = id;
                }
                else
                {
                    errors.Add(new ImportCellError(row.Row, "grammar_pattern", ReferenceImport.Codes.UnknownGrammar));
                }
            }

            if (existing.Contains(FuriganaMarkup.PlainText(row.Text.Japanese ?? [])))
            {
                skipped++;
                continue;
            }

            create.Add(Sentence.Create(
                Uuid7.New(timeProvider), context.LevelId, chapterId, context.Source, context.License,
                row.Text, row.Tags, grammarPointId, row.TatoebaId));
        }

        return new ImportPlan(create, skipped);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/reference/{type}/import", async (
                string type,
                [FromForm] ImportReferenceForm form,
                bool? dryRun,
                ImportReferenceHandler handler,
                CancellationToken cancellationToken) =>
            {
                if (!EnumSnakeCase.TryParse<ReferenceType>(type, out var referenceType))
                {
                    return AppError.NotFound("Unknown reference data type.").ToProblem();
                }

                return (await handler.HandleAsync(referenceType, form, dryRun ?? true, cancellationToken)).ToOkResult();
            })
            .RequireAuthorization(Policies.AdminOnly)
            .DisableAntiforgery()
            .WithName("ImportReference")
            .WithSummary("Validate (dryRun=true) or import reference data from CSV/JSON with source and license (admin)")
            .Produces<ImportReferenceResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
