import { textIn, type ApiTextBlock } from "@/i18n/textblock";
import type { UiLocale } from "@/i18n/types";
import type { SafeQuestion } from "../api/practice";

interface SavedAnswer {
  selected?: number[];
  order?: number[];
  text?: string;
  pairs?: number[][];
  remembered?: boolean;
}

const plain = (block: ApiTextBlock | null | undefined, locale: UiLocale) =>
  (block?.ja ?? []).map((segment) => segment.t).join("") || textIn(block, locale);

/** A short text of a saved quiz answer, so the student sees what is stored (BR-KUIS-06). */
export function answerSummary(question: SafeQuestion, answer: unknown, locale: UiLocale): string {
  if (answer === null || typeof answer !== "object") return "";
  const saved = answer as SavedAnswer;
  switch (question.kind) {
    case "mcq":
      return (saved.selected ?? [])
        .map((id) => plain(question.choices?.find((choice) => choice.choiceId === id)?.text, locale))
        .join(", ");
    case "tap_sentence":
      return (saved.order ?? [])
        .map((id) => plain(question.tokens?.find((token) => token.tokenId === id)?.text, locale))
        .join(" ");
    case "match":
      return (saved.pairs ?? [])
        .map(
          ([left, right]) =>
            `${plain(question.left?.find((item) => item.leftId === left)?.text, locale)} → ${plain(
              question.right?.find((item) => item.rightId === right)?.text,
              locale,
            )}`,
        )
        .join("; ");
    default:
      return saved.text ?? "";
  }
}
