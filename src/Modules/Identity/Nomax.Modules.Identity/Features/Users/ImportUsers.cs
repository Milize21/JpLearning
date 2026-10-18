using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Identity.Domain;
using Nomax.Modules.Identity.Features.Common;
using Nomax.Modules.Identity.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Identity.Features.Users;

/// <param name="Code">An error code from the catalog in docs/05 §2, e.g. <c>keycloak_unavailable</c>.</param>
public sealed record ImportFailure(int Row, string Username, string Code);

/// <param name="Errors">Validation errors per cell; when present nothing was created.</param>
/// <param name="ToCreate">Usernames that will be (dry run) or were meant to be created.</param>
/// <param name="Skipped">Usernames that already exist and were left alone.</param>
/// <param name="Created">Usernames created in this run.</param>
/// <param name="Failure">Where a real run stopped because Keycloak failed; earlier rows stay created.</param>
/// <param name="PasswordsCsv"><c>username,temporary_password</c> for the created accounts. Returned once, never stored.</param>
public sealed record ImportUsersResponse(
    bool DryRun,
    int TotalRows,
    IReadOnlyList<CsvImportError> Errors,
    IReadOnlyList<string> ToCreate,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Created,
    ImportFailure? Failure,
    string? PasswordsCsv);

/// <summary>
/// POST /api/identity/users/import?dryRun=true|false — BR-AKUN-03, admin only. Multipart field <c>file</c> (UTF-8 CSV).
/// </summary>
internal sealed class ImportUsersHandler(IdentityDbContext db, AccountProvisioner provisioner, ICurrentUser currentUser)
{
    public async Task<Result<ImportUsersResponse>> HandleAsync(IFormFile? file, bool dryRun, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["file"] = ["A CSV file is required."] });
        }

        string csv;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            csv = await reader.ReadToEndAsync(cancellationToken);
        }

        var existingUsernames = (await db.UserProfiles.Select(profile => profile.Username).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var existingEmails = (await db.UserProfiles
                .Where(profile => profile.Email != null)
                .Select(profile => new { profile.Email, profile.Username })
                .ToListAsync(cancellationToken))
            .ToDictionary(profile => profile.Email!.ToLowerInvariant(), profile => profile.Username, StringComparer.Ordinal);

        var plan = UserCsvImport.Plan(csv, existingUsernames, existingEmails);
        var toCreate = plan.ToCreate.Select(row => row.Username).ToList();
        var skipped = plan.Skipped.Select(row => row.Username).ToList();

        if (dryRun || !plan.IsValid)
        {
            return new ImportUsersResponse(dryRun, plan.TotalRows, plan.Errors, toCreate, skipped, [], null, null);
        }

        var created = new List<(string Username, string TemporaryPassword)>();
        ImportFailure? failure = null;
        foreach (var row in plan.ToCreate)
        {
            var result = await provisioner.CreateAsync(
                new NewAccount(row.Username, row.Email, row.DisplayName, row.Role, row.Timezone, row.UiLocale),
                currentUser.Id,
                cancellationToken);

            if (!result.IsSuccess)
            {
                // Stop at the first failure; importing the same file again skips the accounts already created.
                failure = new ImportFailure(row.Row, row.Username, result.Error.Code);
                break;
            }

            created.Add((row.Username, result.Value.TemporaryPassword));
        }

        return new ImportUsersResponse(
            DryRun: false,
            plan.TotalRows,
            plan.Errors,
            toCreate,
            skipped,
            [.. created.Select(account => account.Username)],
            failure,
            created.Count > 0 ? UserCsvImport.PasswordsCsv(created) : null);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/users/import", async (
                IFormFile? file,
                bool? dryRun,
                ImportUsersHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(file, dryRun ?? true, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            // Bearer-token API: no cookies, so no CSRF (docs/09 §3).
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithName("ImportUsers")
            .WithSummary("Validate (dryRun=true) or import sensei/murid accounts from CSV (admin)")
            .Produces<ImportUsersResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
