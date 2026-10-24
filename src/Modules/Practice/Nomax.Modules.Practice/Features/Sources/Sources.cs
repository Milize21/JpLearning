using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Sources;

public sealed record CreateSourceRequest(string Name, string License, string? Url = null);

/// <param name="MarkReviewed">Records the caller as reviewer of the source's license.</param>
public sealed record UpdateSourceRequest(string? Name = null, string? License = null, string? Url = null, bool MarkReviewed = false);

internal sealed class CreateSourceValidator : AbstractValidator<CreateSourceRequest>
{
    public CreateSourceValidator()
    {
        RuleFor(request => request.Name).Must(name => name?.Trim().Length is > 0 and <= QuestionSource.NameMaxLength)
            .WithMessage($"Must be 1–{QuestionSource.NameMaxLength} characters.");
        RuleFor(request => request.License).Must(license => license?.Trim().Length is > 0 and <= QuestionSource.LicenseMaxLength)
            .WithMessage($"Must be 1–{QuestionSource.LicenseMaxLength} characters.");
        RuleFor(request => request.Url).HttpsUrl(QuestionSource.UrlMaxLength).When(request => !string.IsNullOrEmpty(request.Url));
    }
}

internal sealed class UpdateSourceValidator : AbstractValidator<UpdateSourceRequest>
{
    public UpdateSourceValidator()
    {
        RuleFor(request => request.Name).Must(name => name!.Trim().Length is > 0 and <= QuestionSource.NameMaxLength)
            .When(request => request.Name is not null)
            .WithMessage($"Must be 1–{QuestionSource.NameMaxLength} characters.");
        RuleFor(request => request.License).Must(license => license!.Trim().Length is > 0 and <= QuestionSource.LicenseMaxLength)
            .When(request => request.License is not null)
            .WithMessage($"Must be 1–{QuestionSource.LicenseMaxLength} characters.");
        RuleFor(request => request.Url).HttpsUrl(QuestionSource.UrlMaxLength).When(request => !string.IsNullOrEmpty(request.Url));
    }
}

/// <summary>GET · POST /api/practice/sources and PATCH /sources/{id} — admin (BR-SOAL-01: every question has a source and license).</summary>
internal sealed class SourcesHandler(PracticeDbContext db, PracticeAccess access, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<QuestionSourceDto>> ListAsync(CancellationToken cancellationToken) =>
        [.. (await db.QuestionSources.AsNoTracking().OrderBy(source => source.Name).ToListAsync(cancellationToken)).Select(QuestionSourceDto.From)];

    public async Task<QuestionSourceDto> CreateAsync(CreateSourceRequest request, CancellationToken cancellationToken)
    {
        var source = QuestionSource.Create(Uuid7.New(timeProvider), request.Name, string.IsNullOrEmpty(request.Url) ? null : request.Url, request.License);
        db.QuestionSources.Add(source);
        await db.SaveChangesAsync(cancellationToken);
        return QuestionSourceDto.From(source);
    }

    public async Task<Result<QuestionSourceDto>> UpdateAsync(Guid id, UpdateSourceRequest request, CancellationToken cancellationToken)
    {
        var source = await db.QuestionSources.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (source is null)
        {
            return AppError.NotFound();
        }

        source.Update(request.Name, string.IsNullOrEmpty(request.Url) ? null : request.Url, request.License);
        if (request.MarkReviewed)
        {
            source.MarkReviewed(access.UserId, timeProvider.GetUtcNow());
        }

        await db.SaveChangesAsync(cancellationToken);
        return QuestionSourceDto.From(source);
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/sources", async (SourcesHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(cancellationToken)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("ListQuestionSources")
            .WithSummary("Question sources with their licenses (admin)")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/sources", async (CreateSourceRequest request, SourcesHandler handler, CancellationToken cancellationToken) =>
            {
                var created = await handler.CreateAsync(request, cancellationToken);
                return TypedResults.Created($"/api/practice/sources/{created.Id}", created);
            })
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<CreateSourceRequest>()
            .WithName("CreateQuestionSource")
            .WithSummary("Add a question source (admin)")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPatch("/sources/{id:guid}", async (Guid id, UpdateSourceRequest request, SourcesHandler handler, CancellationToken cancellationToken) =>
                (await handler.UpdateAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<UpdateSourceRequest>()
            .WithName("UpdateQuestionSource")
            .WithSummary("Change a question source or mark its license as reviewed (admin)")
            .Produces<QuestionSourceDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
