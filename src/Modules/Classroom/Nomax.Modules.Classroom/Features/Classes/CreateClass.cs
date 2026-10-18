using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Features.Common;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.Modules.Content.Contracts;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Classes;

/// <param name="Status"><c>draft</c> or <c>active</c> (default); a draft class gives students no access yet.</param>
public sealed record CreateClassRequest(string Name, Guid LevelId, Guid BookId, ClassStatus? Status = null);

internal sealed class CreateClassValidator : AbstractValidator<CreateClassRequest>
{
    public CreateClassValidator()
    {
        RuleFor(request => request.Name).Must(name => name?.Trim().Length is > 0 and <= StudyClass.NameMaxLength)
            .WithMessage($"Must be 1–{StudyClass.NameMaxLength} characters.");
        RuleFor(request => request.LevelId).NotEmpty();
        RuleFor(request => request.BookId).NotEmpty();
        RuleFor(request => request.Status).Must(status => status is null or ClassStatus.Draft or ClassStatus.Active)
            .WithMessage("Must be draft or active.");
    }
}

/// <summary>POST /api/classroom/classes — staff; the creator becomes the primary teacher (BR-AKSES-03).</summary>
internal sealed class CreateClassHandler(
    ClassroomDbContext db,
    IContentCatalog catalog,
    ClassViews views,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<ClassDto>> HandleAsync(CreateClassRequest request, CancellationToken cancellationToken)
    {
        var book = await catalog.GetBookAsync(request.BookId, cancellationToken);
        if (book is null || book.LevelId != request.LevelId)
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["bookId"] = ["Must be a book of the chosen level."] });
        }

        var item = StudyClass.Create(Uuid7.New(timeProvider), request.LevelId, request.BookId, request.Name, request.Status ?? ClassStatus.Active, currentUser.Id);
        db.Classes.Add(item);
        db.ClassTeachers.Add(ClassTeacher.Create(item.Id, currentUser.Id, isPrimary: true));
        await db.SaveChangesAsync(cancellationToken);
        return await views.BuildAsync(item, currentUser.Id, cancellationToken);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/classes", async (CreateClassRequest request, CreateClassHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken))
                    .ToHttpResult(item => TypedResults.Created($"/api/classroom/classes/{item.Id}", item)))
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<CreateClassRequest>()
            .WithName("CreateClass")
            .WithSummary("Create a class; the creator becomes its primary teacher (staff)")
            .Produces<ClassDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
