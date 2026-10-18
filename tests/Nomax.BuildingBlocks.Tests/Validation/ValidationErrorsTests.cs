using FluentValidation.Results;
using Nomax.BuildingBlocks.Validation;

namespace Nomax.BuildingBlocks.Tests.Validation;

public sealed class ValidationErrorsTests
{
    [Theory]
    [InlineData("DisplayName", "displayName")]
    [InlineData("Items[0].DisplayName", "items[0].displayName")]
    [InlineData("ui_locale", "ui_locale")]
    [InlineData("", "")]
    public void Field_paths_follow_the_json_names(string propertyPath, string expected)
    {
        ValidationErrors.ToCamelCasePath(propertyPath).ShouldBe(expected);
    }

    [Fact]
    public void Groups_messages_per_field_without_duplicates()
    {
        var result = new ValidationResult(
        [
            new ValidationFailure("DisplayName", "must not be empty"),
            new ValidationFailure("DisplayName", "must not be empty"),
            new ValidationFailure("DisplayName", "must be at most 80 characters"),
            new ValidationFailure("Timezone", "must be an IANA zone"),
        ]);

        var errors = ValidationErrors.From(result);

        errors.Keys.ShouldBe(["displayName", "timezone"], ignoreOrder: true);
        errors["displayName"].ShouldBe(["must not be empty", "must be at most 80 characters"]);
    }
}
