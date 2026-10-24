namespace Nomax.Modules.Practice.Domain;

// Enums of docs/03 §3.3, stored as text + CHECK with snake_case values (D-08).

public enum QuestionKind
{
    Mcq,
    TapSentence,
    TypeAnswer,
    Match,
    Flashcard,
    HandwriteKana,
    HandwriteKanji,
    LongAnswer,
}

public enum Category
{
    Hiragana,
    Katakana,
    Kanji,
    Vocab,
    Grammar,
    Sentence,
}

public enum QuestionStatus
{
    Draft,
    Review,
    Published,
    Archived,
}

public enum QuestionScope
{
    Builtin,
    Private,
}

public enum RefItemType
{
    Kana,
    Kanji,
    Vocab,
    Grammar,
    Sentence,
}

public enum MasteryItemType
{
    Kana,
    Kanji,
    Vocab,
    Grammar,
    Sentence,
    Question,
}

public enum InputMode
{
    Choice,
    Tap,
    Type,
    Handwrite,
    SelfCheck,
}

public enum CommentTarget
{
    DrillSession,
    QuizAttempt,
    Submission,
    Progress,
}

/// <summary>Payload field <c>jaDisplay</c> (docs/06 §9): how Japanese text may be shown without leaking the answer.</summary>
public enum JaDisplay
{
    UserPref,
    Plain,
    NoRomaji,
}

/// <summary><c>type_answer.answerScript</c> (docs/03 §8.1).</summary>
public enum AnswerScript
{
    Kana,
    Romaji,
    Free,
}

/// <summary>Drill session mode (docs/03 §7.3 <c>config.mode</c>).</summary>
public enum DrillMode
{
    Normal,
    WeakFocus,
}

/// <summary>BR-MASTERY-02 item status.</summary>
public enum MasteryStatus
{
    NotStarted,
    Learning,
    Weak,
    Mastered,
}
