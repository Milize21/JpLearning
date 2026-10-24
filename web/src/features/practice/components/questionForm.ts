import { parseFurigana } from "@/i18n/furigana";
import {
  fieldsToTextBlock,
  textBlockToFields,
  type ApiTextBlock,
  type TextBlockFields,
} from "@/i18n/textblock";
import type { JaDisplay } from "@/i18n/types";
import type { Category, CreateQuestionRequest, QuestionDto, QuestionKind } from "../api/practice";

/** Kinds the editor creates; handwriting (phase 5) and long answers (quizzes, phase 3) are not edited here yet. */
export const editableKinds: readonly QuestionKind[] = [
  "mcq",
  "type_answer",
  "tap_sentence",
  "match",
  "flashcard",
];

export type AnswerScript = "kana" | "romaji" | "free";

export interface QuestionForm {
  kind: QuestionKind;
  levelId: string;
  bookId: string;
  chapterId: string;
  category: Category;
  prompt: TextBlockFields;
  explanation: string;
  tags: string;
  jaDisplay: JaDisplay;
  stimulus: TextBlockFields;
  choices: { text: TextBlockFields; correct: boolean }[];
  multi: boolean;
  tokens: TextBlockFields[];
  accepted: string;
  answerScript: AnswerScript;
  kanaInsensitive: boolean;
  pairs: { left: TextBlockFields; right: TextBlockFields }[];
  front: TextBlockFields;
  back: TextBlockFields;
}

const empty = (): TextBlockFields => ({ id: "", en: "", ja: "" });

export const emptyForm = (): QuestionForm => ({
  kind: "mcq",
  levelId: "",
  bookId: "",
  chapterId: "",
  category: "hiragana",
  prompt: empty(),
  explanation: "",
  tags: "",
  jaDisplay: "user_pref",
  stimulus: empty(),
  choices: [
    { text: empty(), correct: true },
    { text: empty(), correct: false },
  ],
  multi: false,
  tokens: [empty(), empty()],
  accepted: "",
  answerScript: "kana",
  kanaInsensitive: true,
  pairs: [
    { left: empty(), right: empty() },
    { left: empty(), right: empty() },
    { left: empty(), right: empty() },
  ],
  front: empty(),
  back: empty(),
});

interface RawPayload {
  stimulus?: ApiTextBlock | null;
  choices?: ApiTextBlock[];
  correct?: number[];
  multi?: boolean;
  tokens?: ApiTextBlock[];
  answers?: number[][];
  accepted?: string[];
  answerScript?: AnswerScript;
  kanaInsensitive?: boolean | null;
  pairs?: { left: ApiTextBlock; right: ApiTextBlock }[];
  front?: ApiTextBlock;
  back?: ApiTextBlock;
  jaDisplay?: JaDisplay | null;
}

/** Editor state of an existing question; the chapter's book is filled in by the dialog. */
export function formFromQuestion(question: QuestionDto): QuestionForm {
  const payload = (question.payload ?? {}) as RawPayload;
  const base = emptyForm();
  const correct = new Set(payload.correct ?? []);
  const order = payload.answers?.[0] ?? [];
  const tokens = payload.tokens ?? [];
  return {
    ...base,
    kind: question.kind,
    levelId: question.levelId,
    chapterId: question.chapterId,
    category: question.category,
    prompt: textBlockToFields(question.prompt),
    explanation: question.explanation?.id ?? "",
    tags: question.tags.filter((tag) => !tag.startsWith("gen:")).join(", "),
    jaDisplay: payload.jaDisplay ?? "user_pref",
    stimulus: textBlockToFields(payload.stimulus),
    choices:
      payload.choices?.map((text, index) => ({
        text: textBlockToFields(text),
        correct: correct.has(index),
      })) ?? base.choices,
    multi: payload.multi ?? false,
    // Tokens are shown in the correct order; tokens outside it are distractors and come last.
    tokens: tokens.length
      ? [...order, ...tokens.map((_, index) => index).filter((index) => !order.includes(index))].map(
          (index) => textBlockToFields(tokens[index]),
        )
      : base.tokens,
    accepted: (payload.accepted ?? []).join("\n"),
    answerScript: payload.answerScript ?? "kana",
    kanaInsensitive: payload.kanaInsensitive ?? true,
    pairs:
      payload.pairs?.map((pair) => ({
        left: textBlockToFields(pair.left),
        right: textBlockToFields(pair.right),
      })) ?? base.pairs,
    front: textBlockToFields(payload.front),
    back: textBlockToFields(payload.back),
  };
}

/** An option needs Indonesian or Japanese, and valid furigana markup (docs/03 §3.1). */
const optionOk = (fields: TextBlockFields) =>
  (fields.id.trim() !== "" || fields.ja.trim() !== "") &&
  (fields.ja.trim() === "" || parseFurigana(fields.ja.trim()).errors.length === 0);

const filled = (fields: TextBlockFields) => fields.id.trim() !== "" || fields.ja.trim() !== "";

/** Client checks before sending; the server validates the same rules again. Returns a list of problem keys. */
export function formProblems(form: QuestionForm): string[] {
  const problems: string[] = [];
  if (!form.levelId || !form.chapterId) problems.push("chapter");
  if (form.prompt.id.trim() === "") problems.push("prompt");
  if (filled(form.stimulus) && !optionOk(form.stimulus)) problems.push("stimulus");
  switch (form.kind) {
    case "mcq": {
      const choices = form.choices.filter((choice) => filled(choice.text));
      if (choices.length < 2 || !choices.every((choice) => optionOk(choice.text))) problems.push("choices");
      const correct = choices.filter((choice) => choice.correct).length;
      if (correct === 0 || (!form.multi && correct !== 1)) problems.push("correct");
      break;
    }
    case "tap_sentence":
      if (!filled(form.stimulus)) problems.push("stimulus");
      if (form.tokens.filter(filled).length < 2 || !form.tokens.filter(filled).every(optionOk))
        problems.push("tokens");
      break;
    case "type_answer":
      if (form.accepted.split("\n").every((line) => line.trim() === "")) problems.push("accepted");
      break;
    case "match":
      if (form.pairs.filter((pair) => filled(pair.left) && filled(pair.right)).length < 3)
        problems.push("pairs");
      break;
    case "flashcard":
      if (!optionOk(form.front) || !optionOk(form.back)) problems.push("card");
      break;
    default:
      problems.push("kind");
  }
  return problems;
}

const block = (fields: TextBlockFields) => fieldsToTextBlock(fields);

/** The payload of docs/03 §8.1 for the chosen kind. */
export function payloadOf(form: QuestionForm): unknown {
  const stimulus = filled(form.stimulus) ? block(form.stimulus) : undefined;
  switch (form.kind) {
    case "mcq": {
      const choices = form.choices.filter((choice) => filled(choice.text));
      return {
        stimulus,
        choices: choices.map((choice) => block(choice.text)),
        correct: choices.flatMap((choice, index) => (choice.correct ? [index] : [])),
        multi: form.multi,
        jaDisplay: form.jaDisplay,
      };
    }
    case "tap_sentence": {
      const tokens = form.tokens.filter(filled);
      return {
        stimulus,
        tokens: tokens.map(block),
        answers: [tokens.map((_, index) => index)],
        jaDisplay: form.jaDisplay,
      };
    }
    case "type_answer":
      return {
        stimulus,
        accepted: form.accepted
          .split("\n")
          .map((line) => line.trim())
          .filter(Boolean),
        answerScript: form.answerScript,
        kanaInsensitive: form.answerScript === "kana" ? form.kanaInsensitive : undefined,
        jaDisplay: form.jaDisplay,
      };
    case "match":
      return {
        pairs: form.pairs
          .filter((pair) => filled(pair.left) && filled(pair.right))
          .map((pair) => ({ left: block(pair.left), right: block(pair.right) })),
        jaDisplay: form.jaDisplay,
      };
    default:
      return { front: block(form.front), back: block(form.back), jaDisplay: form.jaDisplay };
  }
}

export function tagsOf(form: QuestionForm): string[] {
  return form.tags
    .split(",")
    .map((tag) => tag.trim())
    .filter(Boolean);
}

export function createRequest(form: QuestionForm): CreateQuestionRequest {
  return {
    kind: form.kind,
    levelId: form.levelId,
    chapterId: form.chapterId,
    category: form.category,
    prompt: block(form.prompt),
    payload: payloadOf(form),
    tags: tagsOf(form),
    explanation: form.explanation.trim() ? { format: "md", id: form.explanation.trim(), en: null } : null,
  };
}
