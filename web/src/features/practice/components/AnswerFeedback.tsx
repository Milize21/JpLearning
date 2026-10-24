import { Check, X } from "lucide-react";
import type { Ref } from "react";
import { JapaneseText } from "@/components/japanese/JapaneseText";
import { Button } from "@/components/ui/button";
import { RichTextView } from "@/features/content/components/RichTextView";
import { drill, masteryStatusLabels } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn, toSegments, type ApiTextBlock } from "@/i18n/textblock";
import { cn } from "@/lib/utils";
import type { AnswerResult, SafeQuestion } from "../api/practice";

interface KeyShape {
  correct?: number[];
  order?: number[];
  accepted?: string[];
  pairs?: number[][];
}

/** Text of an option for the "correct answer" line: Japanese in full (furigana + romaji) or the UI language. */
function FullText({ value }: { value: ApiTextBlock | null | undefined }) {
  const { uiLocale } = usePrefs();
  const segments = toSegments(value?.ja);
  if (segments.length > 0) {
    return <JapaneseText value={segments} scriptPref="kanji_furigana_romaji" className="inline-flex" />;
  }
  return <span>{textIn(value, uiLocale)}</span>;
}

function CorrectAnswer({ question, result }: { question: SafeQuestion; result: AnswerResult }) {
  const key = (result.correctAnswer ?? {}) as KeyShape;
  switch (question.kind) {
    case "mcq":
      return (
        <ul className="flex flex-col gap-1">
          {(key.correct ?? []).map((id) => (
            <li key={id}>
              <FullText value={question.choices?.find((choice) => choice.choiceId === id)?.text} />
            </li>
          ))}
        </ul>
      );
    case "tap_sentence":
      return (
        <span className="flex flex-wrap gap-2">
          {(key.order ?? []).map((id) => (
            <FullText key={id} value={question.tokens?.find((token) => token.tokenId === id)?.text} />
          ))}
        </span>
      );
    case "type_answer":
      return (
        <span lang={question.answerScript === "kana" ? "ja" : undefined} className="font-semibold">
          {(key.accepted ?? []).join(" · ")}
        </span>
      );
    case "match":
      return (
        <ul className="flex flex-col gap-1">
          {(question.left ?? []).map((left) => (
            <li key={left.leftId} className="flex flex-wrap items-center gap-2">
              <FullText value={left.text} />
              <span aria-hidden="true">→</span>
              <FullText value={question.right?.find((right) => right.rightId === left.leftId)?.text} />
            </li>
          ))}
        </ul>
      );
    case "flashcard":
      return <FullText value={question.back} />;
    default:
      return null;
  }
}

/**
 * AnswerFeedback of docs/07 §10: correct = success panel with ○ and 正解, wrong = danger panel with × and 不正解, plus
 * the correct answer in full, the explanation and "Next" (Enter). Never colour alone.
 */
export function AnswerFeedback({
  question,
  result,
  onNext,
  nextRef,
}: {
  question: SafeQuestion;
  result: AnswerResult;
  /** Absent in a quiz review, where there is no "next". */
  onNext?: () => void;
  /** Focus moves to "Next" so Enter and screen readers continue from there. */
  nextRef?: Ref<HTMLButtonElement>;
}) {
  const t = useT();
  const ok = result.isCorrect;
  const percent = (value: number) => Math.round(value * 100);

  return (
    <section
      aria-live="assertive"
      className={cn(
        "flex flex-col gap-3 rounded-lg border p-4",
        ok ? "border-tokiwa-600 bg-success-bg" : "border-danger bg-danger-bg",
      )}
    >
      <div className={cn("flex items-center gap-2 font-semibold", ok ? "text-success" : "text-danger")}>
        {ok ? (
          <span
            aria-hidden="true"
            className="inline-flex size-7 items-center justify-center rounded-full border-2 border-current"
          >
            <Check className="size-4" strokeWidth={3} />
          </span>
        ) : (
          <X className="size-7" strokeWidth={3} aria-hidden="true" />
        )}
        <JapaneseText value={ok ? drill.correct : drill.wrong} variant="content" className="text-h3" />
        <span className="text-body">{t(ok ? drill.correct : drill.wrong)}</span>
      </div>
      {!ok && (
        <div className="flex flex-col gap-1 text-body text-ink">
          <span className="text-body-sm text-ink-muted">{t(drill.theAnswer)}</span>
          <CorrectAnswer question={question} result={result} />
        </div>
      )}
      {question.stimulus && (question.stimulus.ja?.length ?? 0) > 0 && (
        <div className="text-body text-ink">
          <FullText value={question.stimulus} />
        </div>
      )}
      {result.explanation && (
        <div className="flex flex-col gap-1 rounded-md bg-surface p-3">
          <span className="text-body-sm font-semibold text-ink-muted">{t(drill.explanation)}</span>
          <RichTextView value={result.explanation} />
        </div>
      )}
      <div className="flex flex-wrap items-center justify-between gap-2">
        {result.mastery ? (
          <span className="text-body-sm text-ink-muted">
            {t(drill.masteryUp, {
              before: percent(result.mastery.before),
              after: percent(result.mastery.after),
            })}{" "}
            · {t(masteryStatusLabels[result.mastery.status])}
          </span>
        ) : (
          <span />
        )}
        {onNext && (
          <Button ref={nextRef} variant="secondary" onClick={onNext}>
            {t(drill.next)}
            <kbd className="ml-1 text-caption opacity-70" aria-hidden="true">
              ⏎
            </kbd>
          </Button>
        )}
      </div>
    </section>
  );
}
