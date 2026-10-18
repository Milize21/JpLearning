using System.Text.Json;
using Nomax.SharedKernel.Serialization;

namespace Nomax.BuildingBlocks.Tests.SharedKernel;

public sealed class EnumSnakeCaseTests
{
    // Same members as the script_pref enum in docs/03 §3.3.
    public enum ScriptPref
    {
        Off,
        KanjiFurigana,
        Kana,
        Romaji,
        KanjiFuriganaRomaji,
    }

    public sealed record Holder(ScriptPref ScriptPref);

    [Fact]
    public void Values_match_the_documented_snake_case_names()
    {
        EnumSnakeCase.Values<ScriptPref>().ShouldBe(
            ["kana", "kanji_furigana", "kanji_furigana_romaji", "off", "romaji"]);
    }

    [Theory]
    [InlineData(ScriptPref.Off, "off")]
    [InlineData(ScriptPref.KanjiFurigana, "kanji_furigana")]
    [InlineData(ScriptPref.KanjiFuriganaRomaji, "kanji_furigana_romaji")]
    public void Converts_both_ways(ScriptPref value, string text)
    {
        EnumSnakeCase.ToValue(value).ShouldBe(text);
        EnumSnakeCase.Parse<ScriptPref>(text).ShouldBe(value);
    }

    [Theory]
    [InlineData("KanjiFurigana")]
    [InlineData("kanji-furigana")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_other_spellings(string? text)
    {
        EnumSnakeCase.TryParse<ScriptPref>(text, out _).ShouldBeFalse();
    }

    [Fact]
    public void Json_uses_the_same_names()
    {
        var json = JsonSerializer.Serialize(new Holder(ScriptPref.KanjiFuriganaRomaji), NomaxJson.Options);

        json.ShouldBe("""{"scriptPref":"kanji_furigana_romaji"}""");
        JsonSerializer.Deserialize<Holder>(json, NomaxJson.Options)!.ScriptPref.ShouldBe(ScriptPref.KanjiFuriganaRomaji);
    }

    [Fact]
    public void Json_rejects_numbers_for_enums()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Holder>("""{"scriptPref":1}""", NomaxJson.Options));
    }
}
