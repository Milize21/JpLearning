import { CircleAlert, Lock, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldHint, FieldLabel, Input, Select, Textarea } from "@/components/ui/form";
import { Alert } from "@/components/ui/misc";
import { useBook, useLevels } from "@/features/content/api/content";
import { TextBlockInput } from "@/features/content/components/TextBlockInput";
import { common } from "@/i18n/labels/common";
import { structure } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { fieldErrors } from "@/i18n/labels/identity";
import { bank, categoryLabels, editor, jaDisplayLabels, kindLabels } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { fieldsToTextBlock, textIn } from "@/i18n/textblock";
import type { JaDisplay } from "@/i18n/types";
import { errorCode } from "@/lib/api/errors";
import {
  categories,
  useSaveQuestion,
  type Category,
  type QuestionDto,
  type QuestionKind,
} from "../api/practice";
import { OptionInput } from "./OptionInput";
import {
  createRequest,
  editableKinds,
  emptyForm,
  formFromQuestion,
  formProblems,
  payloadOf,
  tagsOf,
  type AnswerScript,
  type QuestionForm,
} from "./questionForm";

const problemLabels = {
  chapter: bank.chapter,
  prompt: editor.prompt,
  stimulus: editor.stimulus,
  choices: editor.choices,
  correct: editor.needCorrect,
  tokens: editor.tokens,
  accepted: editor.accepted,
  pairs: editor.pairs,
  card: editor.front,
  kind: editor.notSupported,
} as const;

/** Create or edit a question (docs/05 §7). Content of an answered question is read only (BR-SOAL-04). */
export function QuestionEditorDialog({
  question,
  open,
  onOpenChange,
}: {
  question?: QuestionDto;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {open && (
        <QuestionEditor
          key={question?.id ?? "new"}
          question={question}
          onClose={() => {
            onOpenChange(false);
          }}
        />
      )}
    </Dialog>
  );
}

function QuestionEditor({ question, onClose }: { question?: QuestionDto; onClose: () => void }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const levels = useLevels();
  const save = useSaveQuestion();
  const [form, setForm] = useState<QuestionForm>(() => (question ? formFromQuestion(question) : emptyForm()));
  const [problems, setProblems] = useState<string[]>([]);
  const [serverError, setServerError] = useState<string | null>(null);
  const level = levels.data?.find((item) => item.id === form.levelId);
  // An existing question knows its chapter, not its book: find the book among the level's books.
  const bookId = form.bookId || (level?.books.length === 1 ? (level.books[0]?.id ?? "") : form.bookId);
  const book = useBook(bookId || undefined);
  const locked = question?.hasAnswers === true;
  const set = <K extends keyof QuestionForm>(key: K, value: QuestionForm[K]) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const submit = async () => {
    const found = formProblems(form);
    setProblems(found);
    setServerError(null);
    if (found.length > 0) return;
    try {
      if (question) {
        await save.mutateAsync({
          question,
          update: {
            version: question.version,
            prompt: fieldsToTextBlock(form.prompt),
            explanation: form.explanation.trim()
              ? { format: "md", id: form.explanation.trim(), en: null }
              : null,
            clearExplanation: form.explanation.trim() === "",
            tags: tagsOf(form),
            ...(locked
              ? {}
              : {
                  kind: form.kind,
                  payload: payloadOf(form),
                  category: form.category,
                  levelId: form.levelId,
                  chapterId: form.chapterId,
                }),
          },
        });
      } else {
        await save.mutateAsync({ create: createRequest(form) });
      }
      toast.success(t(bank.done));
      onClose();
    } catch (error) {
      setServerError(errorCode(error));
    }
  };

  const content = (
    <fieldset disabled={locked} className="flex flex-col gap-4 disabled:opacity-80">
      <legend className="sr-only">{t(editor.stimulus)}</legend>
      {form.kind !== "match" && form.kind !== "flashcard" && (
        <Field>
          <FieldLabel htmlFor="q-stimulus-id">
            {t(form.kind === "tap_sentence" ? editor.stimulus : editor.stimulusOptional)}
          </FieldLabel>
          <OptionInput
            id="q-stimulus"
            label={t(editor.stimulus)}
            value={form.stimulus}
            onChange={(value) => {
              set("stimulus", value);
            }}
          />
        </Field>
      )}

      {form.kind === "mcq" && (
        <Field>
          <FieldLabel htmlFor="q-choice-0-id">{t(editor.choices)}</FieldLabel>
          <ol className="flex flex-col gap-2">
            {form.choices.map((choice, index) => (
              <li key={index} className="flex items-start gap-2">
                <label className="mt-2 flex items-center gap-1 text-body-sm text-ink">
                  <input
                    type={form.multi ? "checkbox" : "radio"}
                    name="q-correct"
                    className="size-4 accent-sumi-900"
                    checked={choice.correct}
                    onChange={(event) => {
                      set(
                        "choices",
                        form.choices.map((item, position) => ({
                          ...item,
                          correct:
                            position === index ? event.target.checked : form.multi ? item.correct : false,
                        })),
                      );
                    }}
                  />
                  <span className="sr-only">{t(editor.isCorrect)}</span>
                </label>
                <OptionInput
                  id={`q-choice-${String(index)}`}
                  label={t(editor.choice, { n: index + 1 })}
                  value={choice.text}
                  onChange={(value) => {
                    set(
                      "choices",
                      form.choices.map((item, position) =>
                        position === index ? { ...item, text: value } : item,
                      ),
                    );
                  }}
                />
                <Button
                  variant="ghost"
                  size="icon"
                  type="button"
                  aria-label={`${t(editor.remove)} ${t(editor.choice, { n: index + 1 })}`}
                  disabled={form.choices.length <= 2}
                  onClick={() => {
                    set(
                      "choices",
                      form.choices.filter((_, position) => position !== index),
                    );
                  }}
                >
                  <Trash2 aria-hidden="true" />
                </Button>
              </li>
            ))}
          </ol>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <Button
              variant="outline"
              size="sm"
              type="button"
              disabled={form.choices.length >= 6}
              onClick={() => {
                set("choices", [...form.choices, { text: { id: "", en: "", ja: "" }, correct: false }]);
              }}
            >
              <Plus aria-hidden="true" />
              {t(editor.addChoice)}
            </Button>
            <label className="flex items-center gap-2 text-body-sm text-ink">
              <input
                type="checkbox"
                className="size-4 accent-sumi-900"
                checked={form.multi}
                onChange={(event) => {
                  set("multi", event.target.checked);
                }}
              />
              {t(editor.multi)}
            </label>
          </div>
        </Field>
      )}

      {form.kind === "tap_sentence" && (
        <Field>
          <FieldLabel htmlFor="q-token-0-id">{t(editor.tokens)}</FieldLabel>
          <ol className="flex flex-col gap-2">
            {form.tokens.map((token, index) => (
              <li key={index} className="flex items-start gap-2">
                <span className="mt-2 w-6 text-body-sm text-ink-muted">{index + 1}.</span>
                <OptionInput
                  id={`q-token-${String(index)}`}
                  label={t(editor.token, { n: index + 1 })}
                  value={token}
                  onChange={(value) => {
                    set(
                      "tokens",
                      form.tokens.map((item, position) => (position === index ? value : item)),
                    );
                  }}
                />
                <Button
                  variant="ghost"
                  size="icon"
                  type="button"
                  aria-label={`${t(editor.remove)} ${t(editor.token, { n: index + 1 })}`}
                  disabled={form.tokens.length <= 2}
                  onClick={() => {
                    set(
                      "tokens",
                      form.tokens.filter((_, position) => position !== index),
                    );
                  }}
                >
                  <Trash2 aria-hidden="true" />
                </Button>
              </li>
            ))}
          </ol>
          <Button
            variant="outline"
            size="sm"
            type="button"
            className="self-start"
            disabled={form.tokens.length >= 20}
            onClick={() => {
              set("tokens", [...form.tokens, { id: "", en: "", ja: "" }]);
            }}
          >
            <Plus aria-hidden="true" />
            {t(editor.addToken)}
          </Button>
        </Field>
      )}

      {form.kind === "type_answer" && (
        <>
          <Field>
            <FieldLabel htmlFor="q-script">{t(editor.answerScript)}</FieldLabel>
            <Select
              id="q-script"
              value={form.answerScript}
              onChange={(event) => {
                set("answerScript", event.target.value as AnswerScript);
              }}
            >
              <option value="kana">{t(editor.scriptKana)}</option>
              <option value="romaji">{t(editor.scriptRomaji)}</option>
              <option value="free">{t(editor.scriptFree)}</option>
            </Select>
          </Field>
          <Field>
            <FieldLabel htmlFor="q-accepted">{t(editor.accepted)}</FieldLabel>
            <Textarea
              id="q-accepted"
              rows={3}
              lang={form.answerScript === "kana" ? "ja" : undefined}
              value={form.accepted}
              onChange={(event) => {
                set("accepted", event.target.value);
              }}
            />
          </Field>
          {form.answerScript === "kana" && (
            <label className="flex items-center gap-2 text-body-sm text-ink">
              <input
                type="checkbox"
                className="size-4 accent-sumi-900"
                checked={form.kanaInsensitive}
                onChange={(event) => {
                  set("kanaInsensitive", event.target.checked);
                }}
              />
              {t(editor.kanaInsensitive)}
            </label>
          )}
        </>
      )}

      {form.kind === "match" && (
        <Field>
          <FieldLabel htmlFor="q-left-0-id">{t(editor.pairs)}</FieldLabel>
          <ol className="flex flex-col gap-3">
            {form.pairs.map((pair, index) => (
              <li
                key={index}
                className="flex flex-col gap-2 rounded-md border border-line p-2 sm:flex-row sm:items-start"
              >
                <OptionInput
                  id={`q-left-${String(index)}`}
                  label={t(editor.left, { n: index + 1 })}
                  value={pair.left}
                  onChange={(value) => {
                    set(
                      "pairs",
                      form.pairs.map((item, position) =>
                        position === index ? { ...item, left: value } : item,
                      ),
                    );
                  }}
                />
                <OptionInput
                  id={`q-right-${String(index)}`}
                  label={t(editor.right, { n: index + 1 })}
                  value={pair.right}
                  onChange={(value) => {
                    set(
                      "pairs",
                      form.pairs.map((item, position) =>
                        position === index ? { ...item, right: value } : item,
                      ),
                    );
                  }}
                />
                <Button
                  variant="ghost"
                  size="icon"
                  type="button"
                  aria-label={`${t(editor.remove)} ${t(editor.left, { n: index + 1 })}`}
                  disabled={form.pairs.length <= 3}
                  onClick={() => {
                    set(
                      "pairs",
                      form.pairs.filter((_, position) => position !== index),
                    );
                  }}
                >
                  <Trash2 aria-hidden="true" />
                </Button>
              </li>
            ))}
          </ol>
          <Button
            variant="outline"
            size="sm"
            type="button"
            className="self-start"
            disabled={form.pairs.length >= 8}
            onClick={() => {
              set("pairs", [
                ...form.pairs,
                { left: { id: "", en: "", ja: "" }, right: { id: "", en: "", ja: "" } },
              ]);
            }}
          >
            <Plus aria-hidden="true" />
            {t(editor.addPair)}
          </Button>
        </Field>
      )}

      {form.kind === "flashcard" && (
        <>
          <Field>
            <FieldLabel htmlFor="q-front-id">{t(editor.front)}</FieldLabel>
            <OptionInput
              id="q-front"
              label={t(editor.front)}
              value={form.front}
              onChange={(value) => {
                set("front", value);
              }}
            />
          </Field>
          <Field>
            <FieldLabel htmlFor="q-back-id">{t(editor.back)}</FieldLabel>
            <OptionInput
              id="q-back"
              label={t(editor.back)}
              value={form.back}
              onChange={(value) => {
                set("back", value);
              }}
            />
          </Field>
        </>
      )}

      <Field>
        <FieldLabel htmlFor="q-ja-display">{t(editor.jaDisplay)}</FieldLabel>
        <Select
          id="q-ja-display"
          value={form.jaDisplay}
          aria-describedby="q-ja-display-hint"
          onChange={(event) => {
            set("jaDisplay", event.target.value as JaDisplay);
          }}
        >
          {(["user_pref", "plain", "no_romaji"] as const).map((value) => (
            <option key={value} value={value}>
              {t(jaDisplayLabels[value])}
            </option>
          ))}
        </Select>
        <FieldHint id="q-ja-display-hint">{t(editor.jaDisplayHint)}</FieldHint>
      </Field>
    </fieldset>
  );

  return (
    <DialogContent closeLabel={t(common.close)} aria-describedby={undefined} className="sm:max-w-3xl">
      <DialogHeader>
        <DialogTitle>
          <Label value={question ? bank.edit : bank.create} companion="inline" />
        </DialogTitle>
      </DialogHeader>
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
      >
        {locked && (
          <Alert tone="warning" icon={Lock}>
            {t(bank.locked)}
          </Alert>
        )}
        <div className="grid gap-4 sm:grid-cols-2">
          <Field>
            <FieldLabel htmlFor="q-kind">{t(bank.kind)}</FieldLabel>
            <Select
              id="q-kind"
              value={form.kind}
              disabled={locked}
              onChange={(event) => {
                set("kind", event.target.value as QuestionKind);
              }}
            >
              {editableKinds.map((kind) => (
                <option key={kind} value={kind}>
                  {t(kindLabels[kind])}
                </option>
              ))}
            </Select>
          </Field>
          <Field>
            <FieldLabel htmlFor="q-category">{t(bank.category)}</FieldLabel>
            <Select
              id="q-category"
              value={form.category}
              disabled={locked}
              onChange={(event) => {
                set("category", event.target.value as Category);
              }}
            >
              {categories.map((value) => (
                <option key={value} value={value}>
                  {t(categoryLabels[value])}
                </option>
              ))}
            </Select>
          </Field>
          <Field>
            <FieldLabel htmlFor="q-level">{t(bank.level)}</FieldLabel>
            <Select
              id="q-level"
              value={form.levelId}
              disabled={locked}
              onChange={(event) => {
                setForm({ ...form, levelId: event.target.value, bookId: "", chapterId: "" });
              }}
            >
              <option value="">—</option>
              {levels.data?.map((item) => (
                <option key={item.id} value={item.id}>
                  {textIn(item.name, uiLocale)}
                </option>
              ))}
            </Select>
          </Field>
          <Field>
            <FieldLabel htmlFor="q-book">{t(structure.book)}</FieldLabel>
            <Select
              id="q-book"
              value={bookId}
              disabled={locked || !level}
              onChange={(event) => {
                setForm({ ...form, bookId: event.target.value, chapterId: "" });
              }}
            >
              <option value="">—</option>
              {level?.books.map((item) => (
                <option key={item.id} value={item.id}>
                  {textIn(item.title, uiLocale)}
                </option>
              ))}
            </Select>
          </Field>
          <Field className="sm:col-span-2">
            <FieldLabel htmlFor="q-chapter">{t(bank.chapter)}</FieldLabel>
            <Select
              id="q-chapter"
              value={form.chapterId}
              disabled={locked || !book.data}
              onChange={(event) => {
                set("chapterId", event.target.value);
              }}
            >
              <option value="">—</option>
              {book.data?.chapters.map((chapter) => (
                <option key={chapter.id} value={chapter.id}>
                  {t(structure.chapterNumber, { n: chapter.number })} · {textIn(chapter.title, uiLocale)}
                </option>
              ))}
            </Select>
          </Field>
        </div>

        <TextBlockInput
          id="q-prompt"
          label={editor.prompt}
          value={form.prompt}
          errors={problems.includes("prompt") ? { id: "required" } : undefined}
          onChange={(value) => {
            set("prompt", value);
          }}
        />

        {content}

        <Field>
          <FieldLabel htmlFor="q-explanation">{t(editor.explanation)}</FieldLabel>
          <Textarea
            id="q-explanation"
            rows={3}
            value={form.explanation}
            onChange={(event) => {
              set("explanation", event.target.value);
            }}
          />
        </Field>
        <Field>
          <FieldLabel htmlFor="q-tags">{t(editor.tags)}</FieldLabel>
          <Input
            id="q-tags"
            value={form.tags}
            autoCapitalize="none"
            spellCheck={false}
            onChange={(event) => {
              set("tags", event.target.value);
            }}
          />
        </Field>

        {problems.length > 0 && (
          <Alert tone="danger" icon={CircleAlert}>
            <ul className="list-disc pl-4">
              {problems.map((problem) => (
                <li key={problem}>
                  {problem === "correct" || problem === "kind"
                    ? t(problemLabels[problem])
                    : `${t(problemLabels[problem as keyof typeof problemLabels])}: ${t(fieldErrors.required)}`}
                </li>
              ))}
            </ul>
          </Alert>
        )}
        {serverError && (
          <Alert tone="danger" icon={CircleAlert}>
            {t(errorLabel(serverError))}
          </Alert>
        )}
        <DialogFooter>
          <Button variant="outline" type="button" onClick={onClose}>
            {t(common.cancel)}
          </Button>
          <Button variant="primary" type="submit" loading={save.isPending}>
            {t(common.save)}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  );
}
