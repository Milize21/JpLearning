using System.Reflection;
using Nomax.BuildingBlocks.Errors;
using Nomax.SharedKernel.Results;

namespace Nomax.BuildingBlocks.Tests.Errors;

public sealed class ErrorHttpMappingTests
{
    // The catalog in docs/05 §2, code → HTTP status.
    public static TheoryData<string, int> Catalog => new()
    {
        { "validation_failed", 400 },
        { "unauthorized", 401 },
        { "forbidden", 403 },
        { "account_inactive", 403 },
        { "chapter_locked", 403 },
        { "not_found", 404 },
        { "method_not_allowed", 405 },
        { "conflict", 409 },
        { "concurrency_conflict", 409 },
        { "has_dependents", 409 },
        { "invalid_state", 409 },
        { "already_submitted", 409 },
        { "attempts_exhausted", 409 },
        { "attempt_expired", 409 },
        { "quiz_not_open", 409 },
        { "session_ended", 409 },
        { "empty_pool", 409 },
        { "file_not_ready", 409 },
        { "payload_too_large", 413 },
        { "file_too_large", 413 },
        { "unsupported_media_type", 415 },
        { "rate_limited", 429 },
        { "keycloak_unavailable", 503 },
        { "internal_error", 500 },
    };

    [Theory]
    [MemberData(nameof(Catalog))]
    public void Every_documented_code_has_its_status(string code, int status)
    {
        ErrorHttpMapping.StatusCodeFor(code).ShouldBe(status);
    }

    [Fact]
    public void ErrorCodes_contains_exactly_the_documented_catalog()
    {
        var declared = typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        declared.ShouldBe(Catalog.Select(row => row.Data.Item1).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(400, "validation_failed")]
    [InlineData(401, "unauthorized")]
    [InlineData(403, "forbidden")]
    [InlineData(404, "not_found")]
    [InlineData(405, "method_not_allowed")]
    [InlineData(413, "payload_too_large")]
    [InlineData(415, "unsupported_media_type")]
    [InlineData(429, "rate_limited")]
    [InlineData(500, "internal_error")]
    [InlineData(502, "internal_error")]
    public void Framework_statuses_get_a_code(int status, string code)
    {
        ErrorHttpMapping.CodeForStatus(status).ShouldBe(code);
    }

    [Fact]
    public void Problem_carries_type_title_and_code()
    {
        var problem = ProblemResponses.Create(ErrorCodes.AccountInactive, "The account is deactivated.");

        problem.Status.ShouldBe(403);
        problem.Type.ShouldBe("https://nomax.local/errors/account_inactive");
        problem.Title.ShouldBe("Account inactive");
        problem.Extensions["code"].ShouldBe("account_inactive");
    }
}
