import { ArrowUp, ClipboardList, Lock, Plus, Search, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { ContentText } from "@/components/japanese/ContentText";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldLabel, Input, Select } from "@/components/ui/form";
import { Alert, Badge, Card, EmptyState, Skeleton } from "@/components/ui/misc";
import { TextBlockInput } from "@/features/content/components/TextBlockInput";
import { publishLabels, quizKindLabels, quizzes as labels } from "@/i18n/labels/assessment";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { kindLabels } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { fieldsToTextBlock, textBlockToFields, textIn, type TextBlockFields } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { formatDateTime, isoToZoned, zonedToIso } from "@/lib/datetime";
import type { ClassDto } from "@/features/classroom/api/classroom";
import {
  useQuizQuestions,
  useQuizStatus,
  useSaveQuiz,
  useSetQuizQuestions,
  useClassQuizzes,
  type QuizDto,
  type QuizKind,
} from "../api/assessment";
import { useQuestions, type QuestionDto } from "../api/practice";
import { QuizAttemptsDialog } from "./QuizAttemptsDialog";

/** "Kuis" tab of a class (docs/07 §11 flow 8): quizzes and exams, their questions, publishing and attempts. */
export function QuizzesPanel({ classInfo }: { classInfo: ClassDto }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const list = useClassQuizzes(classInfo.id);
  const status = useQuizStatus();
  const [editing, setEditing] = useState<QuizDto | null | undefined>(undefined);
  const [picking, setPicking] = useState<QuizDto | null>(null);
  const [attemptsOf, setAttemptsOf] = useState<QuizDto | null>(null);
  const readOnly = classInfo.status === "archived";
  const when = (iso: string | null) => (iso ? formatDateTime(iso, uiLocale, me.timezone) : "–");

  const change = (quiz: QuizDto, action: "publish" | "archive") => {
    status.mutate(
      { id: quiz.id, action },
      {
        onSuccess: () => toast.success(t(publishLabels.done)),
        onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
      },
    );
  };

  return (
    <div className="flex flex-col gap-4">
      {!readOnly && (
        <Button
          variant="primary"
          className="self-start"
          onClick={() => {
            setEditing(null);
          }}
        >
          <Plus aria-hidden="true" />
          {t(labels.create)}
        </Button>
      )}
      {list.isPending && <Skeleton className="h-40" />}
      {list.data?.length === 0 && <EmptyState icon={ClipboardList} title={<Label value={labels.empty} />} />}
      <ul className="grid gap-4 md:grid-cols-2">
        {list.data?.map((quiz) => (
          <li key={quiz.id}>
            <Card className="flex h-full flex-col gap-3">
              <div className="flex flex-wrap items-center gap-2">
                <Badge tone={quiz.kind === "exam" ? "solid" : "neutral"}>
                  {t(quizKindLabels[quiz.kind])}
                </Badge>
                <Badge tone={quiz.status === "published" ? "success" : "neutral"}>
                  {t(publishLabels[quiz.status])}
                </Badge>
                {quiz.hasAttempts && (
                  <Badge>
                    <Lock className="size-3" aria-hidden="true" />
                    {t(labels.attempts)}
                  </Badge>
                )}
              </div>
              <h3 className="text-h3 font-semibold text-ink">
                <ContentText value={quiz.title} />
              </h3>
              <p className="text-body-sm text-ink-muted">
                {t(labels.questionCount, { n: quiz.questionCount, points: quiz.totalPoints })} ·{" "}
                {quiz.timeLimitMin ? t(labels.minutes, { n: quiz.timeLimitMin }) : t(labels.noLimit)}
              </p>
              <p className="text-body-sm text-ink-muted">
                {t(labels.window, { open: when(quiz.openAt), close: when(quiz.closeAt) })}
              </p>
              <div className="mt-auto flex flex-wrap gap-2">
                {!readOnly && quiz.status !== "archived" && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      setEditing(quiz);
                    }}
                  >
                    {t(common.edit)}
                  </Button>
                )}
                {!readOnly && quiz.status !== "archived" && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      setPicking(quiz);
                    }}
                  >
                    {t(labels.questions)}
                  </Button>
                )}
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setAttemptsOf(quiz);
                  }}
                >
                  {t(labels.attempts)}
                </Button>
                {!readOnly && quiz.status === "draft" && (
                  <Button
                    variant="secondary"
                    size="sm"
                    onClick={() => {
                      change(quiz, "publish");
                    }}
                  >
                    {t(publishLabels.publish)}
                  </Button>
                )}
                {!readOnly && quiz.status === "published" && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => {
                      change(quiz, "archive");
                    }}
                  >
                    {t(publishLabels.archive)}
                  </Button>
                )}
              </div>
            </Card>
          </li>
        ))}
      </ul>
      {editing !== undefined && (
        <QuizFormDialog
          classId={classInfo.id}
          quiz={editing ?? undefined}
          onClose={() => {
            setEditing(undefined);
          }}
        />
      )}
      {picking && (
        <QuizQuestionsDialog
          quiz={picking}
          levelId={classInfo.levelId}
          onClose={() => {
            setPicking(null);
          }}
        />
      )}
      {attemptsOf && (
        <QuizAttemptsDialog
          quiz={attemptsOf}
          onClose={() => {
            setAttemptsOf(null);
          }}
        />
      )}
    </div>
  );
}

const toLocalInput = (iso: string | null, timeZone: string) => {
  if (!iso) return "";
  const { date, time } = isoToZoned(iso, timeZone);
  return `${date}T${time}`;
};

const fromLocalInput = (value: string, timeZone: string) => {
  if (!value) return null;
  const [date = "", time = "00:00"] = value.split("T");
  return zonedToIso(date, time, timeZone);
};

function QuizFormDialog({
  classId,
  quiz,
  onClose,
}: {
  classId: string;
  quiz?: QuizDto;
  onClose: () => void;
}) {
  const t = useT();
  const me = useCurrentUser();
  const save = useSaveQuiz(classId);
  const [title, setTitle] = useState<TextBlockFields>(() => textBlockToFields(quiz?.title));
  const [kind, setKind] = useState<QuizKind>(quiz?.kind ?? "quiz");
  const [timeLimit, setTimeLimit] = useState(quiz?.timeLimitMin?.toString() ?? "");
  const [maxAttempts, setMaxAttempts] = useState(quiz?.maxAttempts.toString() ?? "1");
  const [openAt, setOpenAt] = useState(() => toLocalInput(quiz?.openAt ?? null, me.timezone));
  const [closeAt, setCloseAt] = useState(() => toLocalInput(quiz?.closeAt ?? null, me.timezone));
  const [shuffle, setShuffle] = useState(quiz?.shuffle ?? true);
  const [showReview, setShowReview] = useState(quiz?.showReview ?? false);
  const [error, setError] = useState<string | null>(null);
  const titleMissing = title.id.trim() === "";

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent closeLabel={t(common.close)} aria-describedby={undefined} className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>
            <Label value={quiz ? labels.edit : labels.create} companion="inline" />
          </DialogTitle>
        </DialogHeader>
        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            setError(null);
            if (titleMissing) {
              setError("validation_failed");
              return;
            }
            save.mutate(
              {
                quiz,
                body: {
                  title: fieldsToTextBlock(title),
                  kind,
                  timeLimitMin: timeLimit ? Number(timeLimit) : null,
                  maxAttempts: Math.max(1, Number(maxAttempts) || 1),
                  openAt: fromLocalInput(openAt, me.timezone),
                  closeAt: fromLocalInput(closeAt, me.timezone),
                  shuffle,
                  showReview,
                },
              },
              {
                onSuccess: () => {
                  toast.success(t(common.saved));
                  onClose();
                },
                onError: (failure) => {
                  setError(errorCode(failure));
                },
              },
            );
          }}
        >
          {quiz?.hasAttempts && (
            <Alert tone="warning" icon={Lock}>
              {t(labels.locked)}
            </Alert>
          )}
          <TextBlockInput
            id="quiz-title"
            label={labels.name}
            value={title}
            errors={error && titleMissing ? { id: "required" } : undefined}
            onChange={setTitle}
          />
          <div className="grid gap-4 sm:grid-cols-3">
            <Field>
              <FieldLabel htmlFor="quiz-kind">{t(labels.kind)}</FieldLabel>
              <Select
                id="quiz-kind"
                value={kind}
                disabled={quiz?.hasAttempts}
                onChange={(event) => {
                  setKind(event.target.value as QuizKind);
                }}
              >
                <option value="quiz">{t(quizKindLabels.quiz)}</option>
                <option value="exam">{t(quizKindLabels.exam)}</option>
              </Select>
            </Field>
            <Field className="sm:col-span-2">
              <FieldLabel htmlFor="quiz-limit">{t(labels.timeLimit)}</FieldLabel>
              <Input
                id="quiz-limit"
                type="number"
                min={1}
                inputMode="numeric"
                value={timeLimit}
                disabled={quiz?.hasAttempts}
                onChange={(event) => {
                  setTimeLimit(event.target.value);
                }}
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="quiz-attempts">{t(labels.maxAttempts)}</FieldLabel>
              <Input
                id="quiz-attempts"
                type="number"
                min={quiz?.hasAttempts ? quiz.maxAttempts : 1}
                inputMode="numeric"
                value={maxAttempts}
                onChange={(event) => {
                  setMaxAttempts(event.target.value);
                }}
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="quiz-open">{t(labels.openAt)}</FieldLabel>
              <Input
                id="quiz-open"
                type="datetime-local"
                value={openAt}
                disabled={quiz?.hasAttempts}
                onChange={(event) => {
                  setOpenAt(event.target.value);
                }}
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="quiz-close">{t(labels.closeAt)}</FieldLabel>
              <Input
                id="quiz-close"
                type="datetime-local"
                value={closeAt}
                onChange={(event) => {
                  setCloseAt(event.target.value);
                }}
              />
            </Field>
          </div>
          <label className="flex items-center gap-2 text-body text-ink">
            <input
              type="checkbox"
              className="size-4 accent-sumi-900"
              checked={shuffle}
              disabled={quiz?.hasAttempts}
              onChange={(event) => {
                setShuffle(event.target.checked);
              }}
            />
            {t(labels.shuffle)}
          </label>
          <label className="flex items-center gap-2 text-body text-ink">
            <input
              type="checkbox"
              className="size-4 accent-sumi-900"
              checked={showReview}
              onChange={(event) => {
                setShowReview(event.target.checked);
              }}
            />
            {t(labels.showReview)}
          </label>
          {error && error !== "validation_failed" && (
            <Alert tone="danger" icon={Lock}>
              {t(errorLabel(error))}
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
    </Dialog>
  );
}

interface Picked {
  questionId: string;
  points: number;
  label: string;
  kind: QuestionDto["kind"];
}

/** Picks published questions (BR-KUIS-07: no self-checks) and sets points and order. */
function QuizQuestionsDialog({
  quiz,
  levelId,
  onClose,
}: {
  quiz: QuizDto;
  levelId: string;
  onClose: () => void;
}) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const current = useQuizQuestions(quiz.id);
  const save = useSetQuizQuestions();
  const [search, setSearch] = useState("");
  const [q, setQ] = useState("");
  const bank = useQuestions({ levelId, status: "published", q, page: 1, pageSize: 50 });
  const [picked, setPicked] = useState<Picked[] | null>(null);
  const list: Picked[] =
    picked ??
    (current.data ?? []).map((item) => ({
      questionId: item.questionId,
      points: item.points,
      label: textIn(item.prompt, uiLocale),
      kind: item.kind,
    }));
  const locked = quiz.hasAttempts;
  const allowed = (kind: QuestionDto["kind"]) =>
    kind === "mcq" ||
    kind === "tap_sentence" ||
    kind === "type_answer" ||
    kind === "match" ||
    kind === "long_answer";

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent closeLabel={t(common.close)} aria-describedby={undefined} className="sm:max-w-4xl">
        <DialogHeader>
          <DialogTitle>
            <Label value={labels.pickQuestions} companion="inline" />
          </DialogTitle>
        </DialogHeader>
        {locked && (
          <Alert tone="warning" icon={Lock}>
            {t(labels.locked)}
          </Alert>
        )}
        <p className="text-body-sm text-ink-muted">{t(labels.pickHint)}</p>
        <div className="grid gap-4 md:grid-cols-2">
          <section className="flex flex-col gap-2" aria-labelledby="bank-heading">
            <h3 id="bank-heading" className="text-body font-bold text-ink">
              {t(labels.questions)}
            </h3>
            <form
              role="search"
              className="flex gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                setQ(search);
              }}
            >
              <Input
                aria-label={t(common.search)}
                type="search"
                value={search}
                onChange={(event) => {
                  setSearch(event.target.value);
                }}
              />
              <Button variant="outline" size="icon" type="submit" aria-label={t(common.search)}>
                <Search aria-hidden="true" />
              </Button>
            </form>
            {bank.isPending && <Skeleton className="h-32" />}
            <ul className="flex max-h-80 flex-col divide-y divide-line overflow-y-auto rounded-md border border-line">
              {bank.data?.items
                .filter((question) => allowed(question.kind))
                .map((question) => {
                  const added = list.some((item) => item.questionId === question.id);
                  return (
                    <li key={question.id} className="flex items-center gap-2 px-3 py-2">
                      <span className="flex min-w-0 flex-1 flex-col">
                        <span className="truncate text-body-sm text-ink">
                          {textIn(question.prompt, uiLocale)}
                        </span>
                        <span className="text-caption text-ink-muted">{t(kindLabels[question.kind])}</span>
                      </span>
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={locked || added}
                        onClick={() => {
                          setPicked([
                            ...list,
                            {
                              questionId: question.id,
                              points: 1,
                              label: textIn(question.prompt, uiLocale),
                              kind: question.kind,
                            },
                          ]);
                        }}
                      >
                        <Plus aria-hidden="true" />
                        <span className="sr-only">{textIn(question.prompt, uiLocale)}</span>
                      </Button>
                    </li>
                  );
                })}
            </ul>
          </section>
          <section className="flex flex-col gap-2" aria-labelledby="picked-heading">
            <h3 id="picked-heading" className="text-body font-bold text-ink">
              {t(labels.selected, { n: list.length })}
            </h3>
            <ol className="flex max-h-96 flex-col gap-2 overflow-y-auto">
              {list.map((item, index) => (
                <li
                  key={item.questionId}
                  className="flex items-center gap-2 rounded-md border border-line p-2"
                >
                  <span className="w-6 text-body-sm text-ink-muted">{index + 1}.</span>
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate text-body-sm text-ink">{item.label}</span>
                    <span className="text-caption text-ink-muted">{t(kindLabels[item.kind])}</span>
                  </span>
                  <Input
                    aria-label={`${t(labels.points)} ${String(index + 1)}`}
                    type="number"
                    min={0.5}
                    step={0.5}
                    className="w-20"
                    value={item.points}
                    disabled={locked}
                    onChange={(event) => {
                      setPicked(
                        list.map((entry, position) =>
                          position === index ? { ...entry, points: Number(event.target.value) } : entry,
                        ),
                      );
                    }}
                  />
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`${t(labels.moveUp)} ${String(index + 1)}`}
                    disabled={locked || index === 0}
                    onClick={() => {
                      const next = [...list];
                      const [moved] = next.splice(index, 1);
                      if (moved) next.splice(index - 1, 0, moved);
                      setPicked(next);
                    }}
                  >
                    <ArrowUp aria-hidden="true" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`${t(common.cancel)} ${String(index + 1)}`}
                    disabled={locked}
                    onClick={() => {
                      setPicked(list.filter((_, position) => position !== index));
                    }}
                  >
                    <Trash2 aria-hidden="true" />
                  </Button>
                </li>
              ))}
            </ol>
          </section>
        </div>
        <DialogFooter>
          <Button variant="outline" type="button" onClick={onClose}>
            {t(common.cancel)}
          </Button>
          <Button
            variant="primary"
            loading={save.isPending}
            disabled={locked || picked === null}
            onClick={() => {
              save.mutate(
                {
                  quiz,
                  questions: list.map((item) => ({
                    questionId: item.questionId,
                    points: item.points > 0 ? item.points : 1,
                  })),
                },
                {
                  onSuccess: () => {
                    toast.success(t(common.saved));
                    onClose();
                  },
                  onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
                },
              );
            }}
          >
            {t(common.save)}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
