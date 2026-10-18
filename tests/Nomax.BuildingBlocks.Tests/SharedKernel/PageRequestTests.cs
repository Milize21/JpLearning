using Nomax.SharedKernel.Paging;

namespace Nomax.BuildingBlocks.Tests.SharedKernel;

public sealed class PageRequestTests
{
    [Fact]
    public void Defaults_to_first_page_of_twenty()
    {
        var request = PageRequest.Create(null, null);

        request.Page.ShouldBe(1);
        request.PageSize.ShouldBe(20);
        request.Skip.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 500, 1, 100)]
    [InlineData(3, 100, 3, 100)]
    public void Clamps_out_of_range_values(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var request = PageRequest.Create(page, pageSize);

        request.Page.ShouldBe(expectedPage);
        request.PageSize.ShouldBe(expectedPageSize);
    }

    [Fact]
    public void Skip_never_overflows()
    {
        var request = PageRequest.Create(int.MaxValue, 100);

        request.Skip.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Skip_counts_previous_pages()
    {
        PageRequest.Create(3, 20).Skip.ShouldBe(40);
    }
}
