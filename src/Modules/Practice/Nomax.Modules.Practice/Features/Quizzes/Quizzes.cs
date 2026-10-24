using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Persistence;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Content.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Features.Quizzes;

public sealed record SaveQuizRequest(
    TextBlock Title,
    QuizKind Kind = QuizKind.Quiz,
    Guid? ChapterId = null,
    int? TimeLimitMin = null,
    int MaxAttempts = 1,
    DateTimeOffset? OpenAt = null,
    DateTimeOffset? CloseAt = null,
    bool Shuffle = true,
    bool ShowReview = false,
    uint? Version = null);

public sealed record QuizQuestionItem(Guid QuestionId, decimal Points = 1m);

public sealed record SetQuizQuestionsRequest(uint Version, IReadOnlyList<QuizQuestionItem> Questions);

internal sealed class SaveQuizValidator : AbstractValidator<SaveQuizRequest>
{
    public SaveQuizValidator()
    {
        RuleFor(request => request.Title).ValidTextBlock(TextBlockUsage.Title);
        RuleFor(request => request.Kind).IsInEnum();
        RuleFor(request => request.TimeLimitMin).InclusiveBetween(1, 1440).When(request => request.TimeLimitMin is not null);
        RuleFor(request => request.MaxAttempts).InclusiveBetween(1, 100);
        RuleFor(request => request.CloseAt).GreaterThan(request => request.OpenAt)
            .When(request => request.OpenAt is not null && request.CloseAt is not null)
            .WithMessage("Must be after openAt.");
    }
}

internal sealed class SetQuizQuestionsValidator : AbstractValidator<SetQuizQuestionsRequest>
{
    public SetQuizQuestionsValidator()
    {
        RuleFor(request => request.Questions).NotNull().Must(items => items.Count <= 200 && items.Select(item => item.QuestionId).Distinct().Count() == items.Count)
            .WithMessage("At most 200 distinct questions.");
        RuleForEach(request => request.Questions).ChildRules(item =>
            item.RuleFor(entry => entry.Points).GreaterThan(0).LessThanOrEqualTo(9999));
    }
}

/// <summary>
/// Quizzes and exams of a class (docs/05 §7): GET · POST /classes/{id}/quizzes, GET · PATCH /quizzes/{id},
/// GET · PUT /quizzes/{id}/questions, POST /quizzes/{id}/publish · /archive. Teachers of the class and admins manage
/// them (BR-AKSES-03); students list the published ones of their classes.
/// </summary>
internal sealed class QuizzesHandler(
    PracticeDbContext db, PracticeAccess access, ClassScope scope, IContentCatalog catalog, TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<QuizDto>>> ListAsync(Guid classId, CancellationToken cancellationToken)
    {
        var found = await scope.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var quizzes = await db.Quizzes.AsNoTracking().Where(quiz => quiz.ClassId == classId)
            .OrderByDescending(quiz => quiz.CreatedAt).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<QuizDto>>(await DtosAsync(quizzes, cancellationToken));
    }

    public async Task<Result<QuizDto>> CreateAsync(Guid classId, SaveQuizRequest request, CancellationToken cancellationToken)
    {
        var found = await scope.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        if (ClassScope.EnsureWritable(found.Value) is { } readOnly)
        {
            return readOnly;
        }

        if (await CheckChapterAsync(found.Value.BookId, request.ChapterId, cancellationToken) is { } chapterError)
        {
            return chapterError;
        }

        var quiz = Quiz.Create(
            Uuid7.New(timeProvider), classId, request.ChapterId, request.Kind, request.Title, request.TimeLimitMin, request.MaxAttempts,
            request.OpenAt?.ToUniversalTime(), request.CloseAt?.ToUniversalTime(), request.Shuffle, request.ShowReview, access.UserId);
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync(cancellationToken);
        return QuizDto.From(quiz, 0, 0m, hasAttempts: false);
    }

    public async Task<Result<Quiz>> ManagedAsync(Guid id, CancellationToken cancellationToken)
    {
        var quiz = await db.Quizzes.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (quiz is null)
        {
            return AppError.NotFound();
        }

        var found = await scope.ForTeacherAsync(quiz.ClassId, cancellationToken);
        return found.IsSuccess ? quiz : found.Error;
    }

    public async Task<Result<QuizDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await ManagedAsync(id, cancellationToken);
        return found.IsSuccess ? (await DtosAsync([found.Value], cancellationToken))[0] : found.Error;
    }

    public async Task<Result<QuizDto>> UpdateAsync(Guid id, SaveQuizRequest request, CancellationToken cancellationToken)
    {
        var found = await ManagedAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var quiz = found.Value;
        if (db.ExpectVersion(quiz, request.Version ?? 0) is { } stale)
        {
            return stale;
        }

        var info = await scope.ForTeacherAsync(quiz.ClassId, cancellationToken);
        if (ClassScope.EnsureWritable(info.Value) is { } readOnly)
        {
            return readOnly;
        }

        if (await CheckChapterAsync(info.Value.BookId, request.ChapterId, cancellationToken) is { } chapterError)
        {
            return chapterError;
        }

        var openAt = request.OpenAt?.ToUniversalTime();
        var closeAt = request.CloseAt?.ToUniversalTime();
        if (await db.QuizAttempts.AnyAsync(attempt => attempt.QuizId == id, cancellationToken)
            && QuizRules.LockedChange(quiz, request.Kind, request.ChapterId, request.TimeLimitMin, request.MaxAttempts, openAt, closeAt, request.Shuffle) is { } field)
        {
            return AppError.InvalidState($"Students already started this quiz; {field} can no longer change (BR-KUIS-10).");
        }

        quiz.Update(request.Title, request.ChapterId, request.Kind, request.TimeLimitMin, request.MaxAttempts, openAt, closeAt, request.Shuffle, request.ShowReview);
        if (await db.SaveChangesOrConflictAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return (await DtosAsync([quiz], cancellationToken))[0];
    }

    public async Task<Result<IReadOnlyList<QuizQuestionDto>>> QuestionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await ManagedAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        return Result.Success<IReadOnlyList<QuizQuestionDto>>(
        [
            .. await db.QuizQuestions.AsNoTracking()
                .Where(item => item.QuizId == id)
                .Join(db.Questions.AsNoTracking(), item => item.QuestionId, question => question.Id, (item, question) => new { item, question })
                .OrderBy(row => row.item.SortOrder)
                .Select(row => new QuizQuestionDto(row.item.QuestionId, row.item.Points, row.item.SortOrder, row.question.Kind, row.question.Category, row.question.Prompt, row.question.Status))
                .ToListAsync(cancellationToken),
        ]);
    }

    /// <summary>
    /// The list replaces the quiz's questions in order. Questions must be published, visible to the caller (builtin or
    /// own, BR-SOAL-03) and allowed in quizzes (BR-KUIS-07); locked once attempted (BR-KUIS-10).
    /// </summary>
    public async Task<Result<IReadOnlyList<QuizQuestionDto>>> SetQuestionsAsync(Guid id, SetQuizQuestionsRequest request, CancellationToken cancellationToken)
    {
        var found = await ManagedAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var quiz = found.Value;
        if (db.ExpectVersion(quiz, request.Version) is { } stale)
        {
            return stale;
        }

        if (quiz.Status == PublishStatus.Archived || await db.QuizAttempts.AnyAsync(attempt => attempt.QuizId == id, cancellationToken))
        {
            return AppError.InvalidState("The questions of an attempted or archived quiz cannot change (BR-KUIS-10).");
        }

        var ids = request.Questions.Select(item => item.QuestionId).ToList();
        var me = access.UserId;
        var usable = await db.Questions.AsNoTracking()
            .Where(question => ids.Contains(question.Id) && question.Status == QuestionStatus.Published)
            .Where(question => access.IsAdmin || question.Scope == QuestionScope.Builtin || question.OwnerId == me)
            .Select(question => new { question.Id, question.Kind })
            .ToListAsync(cancellationToken);
        var allowed = usable.Where(question => QuizRules.IsAllowedInQuiz(question.Kind)).Select(question => question.Id).ToHashSet();
        var invalid = ids.Where(questionId => !allowed.Contains(questionId)).ToList();
        if (invalid.Count > 0)
        {
            return AppError.Validation(new Dictionary<string, string[]>
            {
                ["questions"] = [$"Not usable in a quiz (unpublished, not visible, or a self-check kind): {string.Join(", ", invalid)}"],
            });
        }

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.QuizQuestions.Where(item => item.QuizId == id).ExecuteDeleteAsync(cancellationToken);
            db.QuizQuestions.AddRange(request.Questions.Select((item, index) => QuizQuestion.Create(id, item.QuestionId, Math.Round(item.Points, 2), index)));
            // Touch the quiz so its version changes with its questions.
            db.Entry(quiz).Property(item => item.UpdatedAt).IsModified = true;
            if (await db.SaveChangesOrConflictAsync(cancellationToken) is { } conflict)
            {
                return conflict;
            }

            await transaction.CommitAsync(cancellationToken);
        }

        return await QuestionsAsync(id, cancellationToken);
    }

    public async Task<Result<QuizDto>> PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await ManagedAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var quiz = found.Value;
        if (quiz.Status != PublishStatus.Draft)
        {
            return AppError.InvalidState("Only draft quizzes can be published.");
        }

        if (!await db.QuizQuestions.AnyAsync(item => item.QuizId == id, cancellationToken))
        {
            return AppError.InvalidState("Add questions before publishing.");
        }

        quiz.ChangeStatus(PublishStatus.Published);
        await db.SaveChangesAsync(cancellationToken);
        return (await DtosAsync([quiz], cancellationToken))[0];
    }

    public async Task<Result<QuizDto>> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await ManagedAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        found.Value.ChangeStatus(PublishStatus.Archived);
        await db.SaveChangesAsync(cancellationToken);
        return (await DtosAsync([found.Value], cancellationToken))[0];
    }

    private async Task<AppError?> CheckChapterAsync(Guid bookId, Guid? chapterId, CancellationToken cancellationToken)
    {
        if (chapterId is not { } id)
        {
            return null;
        }

        return (await catalog.GetChapterAsync(id, cancellationToken))?.BookId == bookId
            ? null
            : AppError.Validation(new Dictionary<string, string[]> { ["chapterId"] = ["Must be a chapter of the class's book."] });
    }

    private async Task<IReadOnlyList<QuizDto>> DtosAsync(IReadOnlyList<Quiz> quizzes, CancellationToken cancellationToken)
    {
        var ids = quizzes.Select(quiz => quiz.Id).ToList();
        var counts = await db.QuizQuestions.AsNoTracking().Where(item => ids.Contains(item.QuizId))
            .GroupBy(item => item.QuizId)
            .Select(group => new { QuizId = group.Key, Count = group.Count(), Points = group.Sum(item => item.Points) })
            .ToDictionaryAsync(row => row.QuizId, cancellationToken);
        var attempted = (await db.QuizAttempts.AsNoTracking().Where(attempt => ids.Contains(attempt.QuizId))
            .Select(attempt => attempt.QuizId).Distinct().ToListAsync(cancellationToken)).ToHashSet();
        return
        [
            .. quizzes.Select(quiz => QuizDto.From(
                quiz,
                counts.TryGetValue(quiz.Id, out var count) ? count.Count : 0,
                counts.TryGetValue(quiz.Id, out var points) ? points.Points : 0m,
                attempted.Contains(quiz.Id))),
        ];
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/classes/{id:guid}/quizzes", async (Guid id, QuizzesHandler handler, CancellationToken cancellationToken) =>
                (await handler.ListAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("ListClassQuizzes")
            .WithSummary("Quizzes and exams of a class (teachers, admin); students use /me/quizzes")
            .Produces<IReadOnlyList<QuizDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/classes/{id:guid}/quizzes", async (Guid id, SaveQuizRequest request, QuizzesHandler handler, CancellationToken cancellationToken) =>
                (await handler.CreateAsync(id, request, cancellationToken)).ToHttpResult(quiz => TypedResults.Created($"/api/practice/quizzes/{quiz.Id}", quiz)))
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<SaveQuizRequest>()
            .WithName("CreateQuiz")
            .WithSummary("Create a draft quiz or exam (teachers, admin)")
            .Produces<QuizDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/quizzes/{id:guid}", async (Guid id, QuizzesHandler handler, CancellationToken cancellationToken) =>
                (await handler.GetAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("GetQuiz")
            .WithSummary("One quiz (teachers, admin)")
            .Produces<QuizDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/quizzes/{id:guid}", async (Guid id, SaveQuizRequest request, QuizzesHandler handler, CancellationToken cancellationToken) =>
                (await handler.UpdateAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<SaveQuizRequest>()
            .WithName("UpdateQuiz")
            .WithSummary("Change a quiz; after the first attempt only BR-KUIS-10 fields (teachers, admin)")
            .Produces<QuizDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/quizzes/{id:guid}/questions", async (Guid id, QuizzesHandler handler, CancellationToken cancellationToken) =>
                (await handler.QuestionsAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("GetQuizQuestions")
            .WithSummary("Questions of a quiz with points and order (teachers, admin)")
            .Produces<IReadOnlyList<QuizQuestionDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/quizzes/{id:guid}/questions", async (Guid id, SetQuizQuestionsRequest request, QuizzesHandler handler, CancellationToken cancellationToken) =>
                (await handler.SetQuestionsAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<SetQuizQuestionsRequest>()
            .WithName("SetQuizQuestions")
            .WithSummary("Replace the questions, points and order (teachers, admin)")
            .Produces<IReadOnlyList<QuizQuestionDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/quizzes/{id:guid}/publish", async (Guid id, QuizzesHandler handler, CancellationToken cancellationToken) =>
                (await handler.PublishAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("PublishQuiz")
            .WithSummary("Publish a quiz to the class (teachers, admin)")
            .Produces<QuizDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/quizzes/{id:guid}/archive", async (Guid id, QuizzesHandler handler, CancellationToken cancellationToken) =>
                (await handler.ArchiveAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("ArchiveQuiz")
            .WithSummary("Archive a quiz (teachers, admin)")
            .Produces<QuizDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
