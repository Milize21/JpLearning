using Nomax.SharedKernel.Text;

namespace Nomax.BuildingBlocks.Tests.SharedKernel;

/// <summary>docs/03 §3.1–3.2 validation shared by every module.</summary>
public sealed class TextRulesTests
{
    [Fact]
    public void Title_needs_indonesian_only()
    {
        TextRules.Validate(new TextBlock("Bab 1", null, null), TextBlockUsage.Title).ShouldBeEmpty();
        TextRules.Validate(new TextBlock(" ", "Chapter 1", null), TextBlockUsage.Title).Select(issue => issue.Path).ShouldBe(["id"]);
        TextRules.Validate(null, TextBlockUsage.Title).ShouldNotBeEmpty();
    }

    [Fact]
    public void Sentence_needs_indonesian_and_japanese()
    {
        TextRules.Validate(new TextBlock("Saya makan.", null, [new JaSegment(" ")]), TextBlockUsage.Sentence)
            .Select(issue => issue.Path).ShouldBe(["ja"]);
    }

    [Fact]
    public void Stimulus_needs_indonesian_or_japanese()
    {
        TextRules.Validate(new TextBlock(null, null, [new JaSegment("か")]), TextBlockUsage.Stimulus).ShouldBeEmpty();
        TextRules.Validate(new TextBlock(null, "only english", null), TextBlockUsage.Stimulus).ShouldNotBeEmpty();
    }

    [Fact]
    public void Segments_follow_the_furigana_rules()
    {
        var issues = TextRules.Validate(
            new TextBlock("x", null, [new JaSegment("漢字"), new JaSegment("か", "か"), new JaSegment("は", null, "Wa"), new JaSegment("日本", "nihon")]),
            TextBlockUsage.Title);

        issues.Select(issue => issue.Path).ShouldBe(["ja[0].r", "ja[1].r", "ja[2].ro", "ja[3].r"]);
    }

    [Fact]
    public void Lengths_are_limited()
    {
        TextRules.Validate(new TextBlock(new string('a', 2001), null, null), TextBlockUsage.Title).ShouldNotBeEmpty();
        TextRules.Validate(new TextBlock("x", null, [.. Enumerable.Repeat(new JaSegment("か"), 501)]), TextBlockUsage.Title)
            .Select(issue => issue.Path).ShouldContain("ja");
    }

    [Theory]
    [InlineData("## Judul\n\nTeks **tebal** dan [tautan](https://example.com).", true)]
    [InlineData("Teks <script>alert(1)</script>", false)]
    [InlineData("<img src=x>", false)]
    [InlineData("![gambar](https://example.com/a.png)", false)]
    [InlineData("[tautan](http://example.com)", false)]
    [InlineData("x < 3 dan 5 > 4", true)]
    public void Rich_text_is_restricted_markdown(string markdown, bool valid)
    {
        (TextRules.Validate(new RichText(RichText.MarkdownFormat, markdown, null)).Count == 0).ShouldBe(valid);
    }

    [Theory]
    [InlineData("https://zoom.us/j/1", true)]
    [InlineData("http://zoom.us/j/1", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://", false)]
    public void Links_are_https_only(string url, bool valid)
    {
        TextRules.IsHttpsUrl(url).ShouldBe(valid);
    }
}
