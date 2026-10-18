using Nomax.SharedKernel.Results;

namespace Nomax.BuildingBlocks.Tests.SharedKernel;

public sealed class ResultTests
{
    [Fact]
    public void Success_carries_the_value()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Failure_carries_the_error_and_hides_the_value()
    {
        Result<int> result = AppError.NotFound();

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.NotFound);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Validation_error_keeps_field_errors()
    {
        var error = AppError.Validation(new Dictionary<string, string[]> { ["displayName"] = ["must not be empty"] });

        error.Code.ShouldBe(ErrorCodes.ValidationFailed);
        error.ValidationErrors!["displayName"].ShouldBe(["must not be empty"]);
    }

    [Fact]
    public void Non_generic_success_is_shared()
    {
        Result.Success().ShouldBeSameAs(Result.Success());
        Result.Failure(AppError.Conflict("taken")).Error!.Code.ShouldBe(ErrorCodes.Conflict);
    }
}
