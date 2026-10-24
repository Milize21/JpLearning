using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Features.Questions;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Generator;

/// <param name="ChapterIds">Empty = every chapter of the level that has mapped reference data.</param>
public sealed record GeneratorRunRequest(Guid LevelId, IReadOnlyList<GeneratorTemplate> Templates, IReadOnlyList<Guid>? ChapterIds = null);

/// <param name="Skipped">Template + item pairs that already had a question (BR-SOAL-05).</param>
public sealed record GeneratorRunResponse(int Created, int Skipped, IReadOnlyList<Guid> QuestionIds);

internal sealed class GeneratorRunValidator : AbstractValidator<GeneratorRunRequest>
{
    public GeneratorRunValidator()
    {
        RuleFor(request => request.LevelId).NotEmpty();
        RuleFor(request => request.Templates).NotEmpty().Must(templates => templates.All(Enum.IsDefined))
            .WithMessage("Pick at least one known template.");
        RuleFor(request => request.ChapterIds).Must(ids => ids is null || ids.Count <= 200).WithMessage("At most 200 chapters.");
    }
}

/// <summary>
/// BR-SOAL-05 for one level: builtin questions in <c>review</c> owned by the admin, without duplicates; the source and
/// license of each reference item become the question's source (BR-SOAL-01). Also used by the development seed.
/// </summary>
internal sealed class QuestionGeneratorService(
    PracticeDbContext db, QuestionStore store, IReferenceDataReader reference, IContentCatalog catalog, TimeProvider timeProvider)
{
    public async Task<Result<GeneratorRunResponse>> RunAsync(
        Guid levelId, IReadOnlyCollection<GeneratorTemplate> templates, IReadOnlyCollection<Guid> chapterIds, Guid ownerId,
        QuestionStatus status, CancellationToken cancellationToken)
    {
        if (await catalog.GetLevelAsync(levelId, cancellationToken) is null)
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["levelId"] = ["No such level."] });
        }

        var wanted = templates.ToHashSet();
        bool Any(params GeneratorTemplate[] options) => options.Any(wanted.Contains);
        var input = new GeneratorInput(
            Any(GeneratorTemplate.KanaToRomaji, GeneratorTemplate.RomajiToKana, GeneratorTemplate.KanaTypeRomaji) ? await reference.GetKanaAsync(levelId, cancellationToken) : [],
            Any(GeneratorTemplate.KanjiToReading, GeneratorTemplate.KanjiToMeaning) ? await reference.GetKanjiAsync(levelId, cancellationToken) : [],
            Any(GeneratorTemplate.VocabToMeaning, GeneratorTemplate.MeaningToVocab, GeneratorTemplate.VocabTypeReading) ? await reference.GetVocabAsync(levelId, cancellationToken) : [],
            Any(GeneratorTemplate.SentenceTap) ? await reference.GetSentencesAsync(levelId, cancellationToken) : []);

        var itemChapters = input.Kana.Select(item => item.ChapterId)
            .Concat(input.Kanji.Select(item => item.ChapterId))
            .Concat(input.Vocab.Select(item => item.ChapterId))
            .Concat(input.Sentences.Select(item => item.ChapterId))
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var chapters = await catalog.GetChaptersAsync(itemChapters, cancellationToken);
        var order = chapters.Values.ToDictionary(chapter => chapter.Id, chapter => chapter.SortOrder);

        var generated = QuestionGenerator.Generate(input, wanted, chapterIds.ToHashSet(), order);

        var itemIds = generated.Select(question => question.ItemId).Distinct().ToList();
        var existing = (await db.Questions.AsNoTracking()
                .Where(question => question.ItemId != null && itemIds.Contains(question.ItemId.Value))
                .Select(question => new { question.ItemId, question.Tags })
                .ToListAsync(cancellationToken))
            .SelectMany(question => question.Tags.Where(QuestionRules.IsInternalTag).Select(tag => (question.ItemId!.Value, tag)))
            .ToHashSet();

        var created = new List<Guid>();
        var skipped = 0;
        foreach (var question in generated)
        {
            if (existing.Contains((question.ItemId, QuestionGenerator.TagOf(question.Template))))
            {
                skipped++;
                continue;
            }

            var sourceId = await store.SourceIdAsync(question.Source, question.License, cancellationToken);
            var entity = Question.Create(
                Uuid7.New(timeProvider), question.Kind, question.LevelId, question.ChapterId, question.Category, question.Tags,
                question.Prompt, Payloads.ToJson(question.Payload), null, question.ItemType, question.ItemId,
                status, QuestionScope.Builtin, sourceId, ownerId);
            db.Questions.Add(entity);
            created.Add(entity.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new GeneratorRunResponse(created.Count, skipped, created);
    }
}

/// <summary>POST /api/practice/generator/runs — admin; results go to review before anyone can practise them.</summary>
internal sealed class RunGeneratorHandler(QuestionGeneratorService generator, PracticeAccess access)
{
    public Task<Result<GeneratorRunResponse>> HandleAsync(GeneratorRunRequest request, CancellationToken cancellationToken) =>
        generator.RunAsync(request.LevelId, request.Templates, request.ChapterIds ?? [], access.UserId, QuestionRules.InitialStatusForGenerated, cancellationToken);

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/generator/runs", async (GeneratorRunRequest request, RunGeneratorHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<GeneratorRunRequest>()
            .WithName("RunQuestionGenerator")
            .WithSummary("Generate builtin questions in review from reference data (admin)")
            .Produces<GeneratorRunResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
