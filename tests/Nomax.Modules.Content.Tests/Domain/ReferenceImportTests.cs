using Nomax.Modules.Content.Domain;

namespace Nomax.Modules.Content.Tests.Domain;

public sealed class ReferenceImportTests
{
    private static (IReadOnlyList<RawImportRow> Rows, List<ImportCellError> Errors) Read(string text, ReferenceType type)
    {
        var errors = new List<ImportCellError>();
        return (ReferenceImport.Read(text, type, errors), errors);
    }

    [Fact]
    public void Kana_csv_is_parsed_with_enum_values_and_chapter_numbers()
    {
        var (rows, errors) = Read("char,script,romaji,row_key,variant,sort_order,chapter\nか,hiragana,ka,ka,seion,6,2\nが,hiragana,ga,ga,dakuon,,\n", ReferenceType.Kana);
        var kana = ReferenceImport.ParseKana(rows, errors);

        errors.ShouldBeEmpty();
        kana.Count.ShouldBe(2);
        kana[0].ShouldBe(new KanaImportRow(2, "か", KanaScript.Hiragana, "ka", "ka", KanaVariant.Seion, 6, 2));
        kana[1].Chapter.ShouldBeNull();
        kana[1].SortOrder.ShouldBe(0);
    }

    [Fact]
    public void Semicolon_csv_from_spreadsheets_is_accepted()
    {
        var (rows, errors) = Read("char;script;romaji;row_key;variant\nキ;katakana;ki;ka;seion\n", ReferenceType.Kana);

        ReferenceImport.ParseKana(rows, errors).ShouldHaveSingleItem().Script.ShouldBe(KanaScript.Katakana);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Every_bad_cell_is_reported_with_row_and_column()
    {
        var (rows, errors) = Read("char,script,romaji,row_key,variant\nka,hiragana,KA,ka,seion\nき,kanji,ki,ka,seion\nく,hiragana,ku,ka,\nく,hiragana,ku,ka,seion\nく,hiragana,ku,ka,seion\n", ReferenceType.Kana);

        ReferenceImport.ParseKana(rows, errors);

        errors.ShouldBe(
        [
            new ImportCellError(2, "char", ReferenceImport.Codes.InvalidValue),
            new ImportCellError(2, "romaji", ReferenceImport.Codes.InvalidFormat),
            new ImportCellError(3, "script", ReferenceImport.Codes.InvalidValue),
            new ImportCellError(4, "variant", ReferenceImport.Codes.Required),
            new ImportCellError(6, "char", ReferenceImport.Codes.DuplicateInFile),
        ]);
    }

    [Fact]
    public void Unknown_and_missing_columns_stop_the_import()
    {
        var (rows, errors) = Read("char,script,warna\nか,hiragana,merah\n", ReferenceType.Kana);

        rows.ShouldBeEmpty();
        errors.ShouldContain(new ImportCellError(1, "warna", ReferenceImport.Codes.UnknownColumn));
        errors.ShouldContain(new ImportCellError(1, "romaji", ReferenceImport.Codes.MissingColumn));
    }

    [Fact]
    public void Empty_file_is_an_error()
    {
        Read("   ", ReferenceType.Vocab).Errors.ShouldBe([new ImportCellError(1, string.Empty, ReferenceImport.Codes.EmptyFile)]);
    }

    [Fact]
    public void Json_arrays_use_the_same_columns_and_lists()
    {
        var (rows, errors) = Read("""
            [{ "word": "食べる", "reading": "たべる", "meanings_id": ["makan"], "meanings_en": "eat|to eat", "pos": "verb_ichidan", "jmdict_seq": 1358280 }]
            """, ReferenceType.Vocab);
        var vocab = ReferenceImport.ParseVocab(rows, errors);

        errors.ShouldBeEmpty();
        var word = vocab.ShouldHaveSingleItem();
        word.Meanings.Indonesian.ShouldBe(["makan"]);
        word.Meanings.English.ShouldBe(["eat", "to eat"]);
        word.JmdictSeq.ShouldBe(1358280);
        word.Row.ShouldBe(2);
    }

    [Fact]
    public void Vocab_reading_must_be_kana()
    {
        var (rows, errors) = Read("word,reading,meanings_id\n食べる,taberu,makan\n", ReferenceType.Vocab);

        ReferenceImport.ParseVocab(rows, errors).ShouldBeEmpty();
        errors.ShouldBe([new ImportCellError(2, "reading", ReferenceImport.Codes.InvalidFormat)]);
    }

    [Fact]
    public void Grammar_titles_use_furigana_markup()
    {
        var (rows, errors) = Read("pattern,title_id,title_ja,tags\n〜は〜です,Kalimat A adalah B,{私|わたし}{は||wa} がくせい です,particle:wa\n", ReferenceType.Grammar);
        var grammar = ReferenceImport.ParseGrammar(rows, errors).ShouldHaveSingleItem();

        errors.ShouldBeEmpty();
        grammar.Title.Japanese![0].Reading.ShouldBe("わたし");
        grammar.Title.Japanese![1].Romaji.ShouldBe("wa");
        grammar.Tags.ShouldBe(["particle:wa"]);
    }

    [Fact]
    public void Sentence_needs_indonesian_and_japanese_and_kanji_need_readings()
    {
        var (rows, errors) = Read("text_id,text_ja\nSaya makan.,私 は たべます\n,{私|わたし}\n", ReferenceType.Sentences);

        ReferenceImport.ParseSentences(rows, errors).ShouldBeEmpty();
        errors.ShouldBe(
        [
            new ImportCellError(2, "text_ja", ReferenceImport.Codes.InvalidFormat),
            new ImportCellError(3, "text_id", ReferenceImport.Codes.Required),
        ]);
    }
}
