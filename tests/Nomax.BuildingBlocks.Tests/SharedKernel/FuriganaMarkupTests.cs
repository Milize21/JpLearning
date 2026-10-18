using Nomax.SharedKernel.Text;

namespace Nomax.BuildingBlocks.Tests.SharedKernel;

/// <summary>Same cases as web/src/i18n/furigana.test.ts so server and client parse markup identically (docs/06 §3).</summary>
public sealed class FuriganaMarkupTests
{
    [Fact]
    public void Kanji_with_reading()
    {
        FuriganaMarkup.Parse("{漢字|かんじ}").Segments.ShouldBe([new JaSegment("漢字", "かんじ")]);
    }

    [Fact]
    public void Particle_with_romaji_override_and_no_reading()
    {
        FuriganaMarkup.Parse("{は||wa}").Segments.ShouldBe([new JaSegment("は", null, "wa")]);
    }

    [Fact]
    public void Reading_plus_romaji_override()
    {
        FuriganaMarkup.Parse("{日本|にほん|nihon}").Segments.ShouldBe([new JaSegment("日本", "にほん", "nihon")]);
    }

    [Fact]
    public void Plain_text_is_one_segment_and_spaces_are_word_boundaries()
    {
        FuriganaMarkup.Parse("{勉強|べんきょう} します").Segments.ShouldBe(
            [new JaSegment("勉強", "べんきょう"), new JaSegment(" "), new JaSegment("します")]);
    }

    [Fact]
    public void Full_documentation_example()
    {
        var result = FuriganaMarkup.Parse("{私|わたし}{は||wa}{日本語|にほんご}{を||o}{勉強|べんきょう} します。");

        result.Errors.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty();
        FuriganaMarkup.PlainText(result.Segments).ShouldBe("私は日本語を勉強します。");
    }

    [Fact]
    public void Escaped_braces_bars_and_backslashes_are_literal()
    {
        FuriganaMarkup.Parse("a\\{b\\|c\\}d\\\\").Segments.ShouldBe([new JaSegment("a{b|c}d\\")]);
    }

    [Theory]
    [InlineData("{漢字|かんじ", FuriganaMarkup.UnclosedBrace)]
    [InlineData("漢字}", FuriganaMarkup.UnexpectedClosingBrace)]
    [InlineData("{|かんじ}", FuriganaMarkup.EmptyBase)]
    [InlineData("{日本|に|ni|x}", FuriganaMarkup.TooManySeparators)]
    [InlineData("{漢字|kanji}", FuriganaMarkup.InvalidReading)]
    [InlineData("{漢字}", FuriganaMarkup.KanjiWithoutReading)]
    [InlineData("漢字です", FuriganaMarkup.KanjiWithoutReading)]
    [InlineData("{{n}}", FuriganaMarkup.UnclosedBrace)]
    public void Errors_have_stable_codes(string markup, string code)
    {
        FuriganaMarkup.Parse(markup).Errors.Select(error => error.Code).ShouldContain(code);
    }

    [Fact]
    public void Katakana_and_long_vowel_mark_are_valid_readings()
    {
        FuriganaMarkup.Parse("{珈琲|コーヒー}").Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Standalone_particle_without_romaji_is_a_warning()
    {
        var result = FuriganaMarkup.Parse("{私|わたし} は {学生|がくせい}");

        result.Errors.ShouldBeEmpty();
        result.Warnings.Select(warning => warning.Text).ShouldBe(["は"]);
    }
}
