namespace Nomax.Modules.Content.Domain;

// Stored as snake_case text with CHECK constraints (docs/03 §3.3).

public enum MaterialKind
{
    File,
    Link,
    Richtext,
}

public enum FileStatus
{
    Uploading,
    Ready,
    Failed,
    Deleted,
}

public enum FilePurpose
{
    Material,
    BookCover,
    Submission,
    QuestionImport,
}

public enum KanaScript
{
    Hiragana,
    Katakana,
}

public enum KanaVariant
{
    Seion,
    Dakuon,
    Handakuon,
    Youon,
}

/// <summary>The <c>{type}</c> segment of <c>/reference/{type}</c> (docs/05 §5).</summary>
public enum ReferenceType
{
    Kana,
    Kanji,
    Vocab,
    Grammar,
    Sentences,
}
