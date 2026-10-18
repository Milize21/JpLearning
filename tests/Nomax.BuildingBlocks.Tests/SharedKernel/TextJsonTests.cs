using System.Text.Json;
using Nomax.SharedKernel.Serialization;
using Nomax.SharedKernel.Text;

namespace Nomax.BuildingBlocks.Tests.SharedKernel;

public sealed class TextJsonTests
{
    // Example from docs/03 §3.1.
    private const string DocsExample = """
        {
          "id": "Saya belajar bahasa Jepang.",
          "en": "I study Japanese.",
          "ja": [
            {"t": "私", "r": "わたし"}, {"t": "は", "ro": "wa"},
            {"t": "日本語", "r": "にほんご"}, {"t": "を", "ro": "o"},
            {"t": "勉強", "r": "べんきょう"}, {"t": "します"}
          ]
        }
        """;

    [Fact]
    public void TextBlock_reads_the_docs_example()
    {
        var block = JsonSerializer.Deserialize<TextBlock>(DocsExample, NomaxJson.Options)!;

        block.Indonesian.ShouldBe("Saya belajar bahasa Jepang.");
        block.English.ShouldBe("I study Japanese.");
        block.Japanese!.Count.ShouldBe(6);
        block.Japanese[0].ShouldBe(new JaSegment("私", "わたし"));
        block.Japanese[1].ShouldBe(new JaSegment("は", Romaji: "wa"));
        block.Japanese[5].ShouldBe(new JaSegment("します"));
    }

    [Fact]
    public void TextBlock_writes_short_keys_omits_missing_parts_and_keeps_japanese_readable()
    {
        var block = new TextBlock("Latihan", null, [new JaSegment("練習", "れんしゅう"), new JaSegment("は", Romaji: "wa")]);

        var json = JsonSerializer.Serialize(block, NomaxJson.Options);

        json.ShouldBe("""{"id":"Latihan","ja":[{"t":"練習","r":"れんしゅう"},{"t":"は","ro":"wa"}]}""");
    }

    [Fact]
    public void JaSegment_carries_the_vocab_id()
    {
        var vocabId = Guid.Parse("0199f000-0000-7000-8000-000000000001");

        var json = JsonSerializer.Serialize(new JaSegment("日本", "にほん", "nihon", vocabId), NomaxJson.Options);

        json.ShouldBe("""{"t":"日本","r":"にほん","ro":"nihon","v":"0199f000-0000-7000-8000-000000000001"}""");
    }

    [Fact]
    public void RichText_round_trips()
    {
        var text = new RichText(RichText.MarkdownFormat, "## Salam\n{挨拶|あいさつ}", null);

        var json = JsonSerializer.Serialize(text, NomaxJson.Options);

        json.ShouldBe("""{"format":"md","id":"## Salam\n{挨拶|あいさつ}"}""");
        JsonSerializer.Deserialize<RichText>(json, NomaxJson.Options).ShouldBe(text);
    }

    [Fact]
    public void Html_sensitive_characters_are_still_escaped()
    {
        var json = JsonSerializer.Serialize(new TextBlock("<script>", null, null), NomaxJson.Options);

        json.ShouldNotContain("<script>");
    }
}
