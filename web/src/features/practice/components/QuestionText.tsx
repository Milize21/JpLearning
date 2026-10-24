import { JapaneseText } from "@/components/japanese/JapaneseText";
import { ContentText } from "@/components/japanese/ContentText";
import { usePrefs } from "@/i18n/prefs";
import { textIn, toSegments, type ApiTextBlock } from "@/i18n/textblock";
import type { JaDisplay } from "@/i18n/types";
import { cn } from "@/lib/utils";

/**
 * Text of a stimulus, choice, token or card (docs/03 §8.1): Japanese when present, rendered with the question's
 * jaDisplay so furigana or romaji never give the answer away (docs/06 §9); otherwise the text in the UI language.
 */
export function QuestionText({
  value,
  display,
  className,
  large = false,
}: {
  value: ApiTextBlock | null | undefined;
  display: JaDisplay;
  className?: string;
  /** Big stimulus; single kana/kanji use the textbook font (docs/07 §10 QuestionCard). */
  large?: boolean;
}) {
  const { uiLocale } = usePrefs();
  const segments = toSegments(value?.ja);
  const text = textIn(value, uiLocale);

  if (segments.length > 0) {
    // Kana and kanji are single code points, so Array.from counts characters correctly here.
    const characters = Array.from(segments.map((segment) => segment.t).join("")).length;
    return (
      <span className={cn("inline-flex flex-col items-center gap-1", className)}>
        <JapaneseText
          value={segments}
          display={display}
          className={cn(large && "text-display", large && characters <= 2 && "font-learn")}
        />
        {text && <span className="text-body-sm text-ink-muted">{text}</span>}
      </span>
    );
  }

  return <span className={cn(large && "text-h1 font-semibold", className)}>{text}</span>;
}

/** The instruction of a question with its Japanese companion. */
export function QuestionPrompt({ value }: { value: ApiTextBlock }) {
  return <ContentText value={value} className="text-body font-semibold text-ink" />;
}
