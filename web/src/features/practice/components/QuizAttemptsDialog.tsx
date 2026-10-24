import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldLabel, Input } from "@/components/ui/form";
import { Badge, Skeleton } from "@/components/ui/misc";
import { attempt as attemptLabels, quizzes as labels } from "@/i18n/labels/assessment";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/datetime";
import { useAttempt, useGradeAnswer, useQuizAttempts, type QuizDto } from "../api/assessment";
import { answerSummary } from "./answerSummary";
import { QuestionPrompt } from "./QuestionText";

/** Attempts of a quiz for its teachers, with the grading queue of long answers (BR-KUIS-07/08). */
export function QuizAttemptsDialog({ quiz, onClose }: { quiz: QuizDto; onClose: () => void }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const attempts = useQuizAttempts(quiz.id);
  const [openId, setOpenId] = useState<string | null>(null);

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent closeLabel={t(common.close)} aria-describedby={undefined} className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>
            <Label value={labels.attempts} companion="inline" />
          </DialogTitle>
        </DialogHeader>
        {attempts.isPending && <Skeleton className="h-32" />}
        {attempts.data?.length === 0 && <p className="text-body-sm text-ink-muted">{t(labels.noAttempts)}</p>}
        <ul className="flex flex-col divide-y divide-line rounded-md border border-line">
          {attempts.data?.map((attempt) => (
            <li key={attempt.id} className="flex flex-col gap-2 px-3 py-2">
              <div className="flex flex-wrap items-center gap-2">
                <span className="min-w-0 flex-1 font-semibold text-ink">
                  {attempt.studentName} · #{attempt.attemptNo}
                </span>
                {attempt.scorePct !== null && <Badge tone="solid">{attempt.scorePct}%</Badge>}
                {attempt.pendingManual > 0 && (
                  <Badge tone="warning">{t(labels.pending, { n: attempt.pendingManual })}</Badge>
                )}
                {attempt.submittedAt && (
                  <span className="text-caption text-ink-muted">
                    {formatDateTime(attempt.submittedAt, uiLocale, me.timezone)}
                  </span>
                )}
                {attempt.status !== "in_progress" && (
                  <Button
                    variant="outline"
                    size="sm"
                    aria-expanded={openId === attempt.id}
                    onClick={() => {
                      setOpenId(openId === attempt.id ? null : attempt.id);
                    }}
                  >
                    {t(attemptLabels.resultTitle)}
                  </Button>
                )}
              </div>
              {openId === attempt.id && <AttemptDetail attemptId={attempt.id} />}
            </li>
          ))}
        </ul>
      </DialogContent>
    </Dialog>
  );
}

function AttemptDetail({ attemptId }: { attemptId: string }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const query = useAttempt(attemptId);
  const grade = useGradeAnswer(attemptId);
  const [points, setPoints] = useState<Record<string, string>>({});

  if (!query.data) return <Skeleton className="h-24" />;
  return (
    <ol className="flex flex-col gap-3">
      {query.data.questions.map((item, index) => {
        const isLong = item.question.kind === "long_answer";
        const text =
          item.answer == null ? t(labels.noAnswer) : answerSummary(item.question, item.answer, uiLocale);
        const fieldId = `grade-${attemptId}-${item.question.id}`;
        return (
          <li key={item.question.id} className="flex flex-col gap-2 rounded-md bg-canvas-subtle p-3">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <span className="text-body-sm font-semibold text-ink">
                {t(attemptLabels.question, { n: index + 1 })}
              </span>
              <span className="text-caption text-ink-muted">
                {item.earned === null
                  ? t(labels.pending, { n: 1 })
                  : t(attemptLabels.earned, { earned: item.earned, points: item.points })}
              </span>
            </div>
            <QuestionPrompt value={item.question.prompt} />
            <p className="whitespace-pre-line text-body-sm text-ink">
              <span className="font-semibold">{t(labels.studentAnswer)}: </span>
              {text}
            </p>
            {isLong && item.answer != null && (
              <form
                noValidate
                className="flex flex-wrap items-end gap-2"
                onSubmit={(event) => {
                  event.preventDefault();
                  const value = Number(points[item.question.id] ?? item.earned ?? 0);
                  grade.mutate(
                    { questionId: item.question.id, points: value },
                    {
                      onSuccess: () => toast.success(t(labels.graded)),
                      onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
                    },
                  );
                }}
              >
                <Field>
                  <FieldLabel htmlFor={fieldId}>{t(labels.givePoints, { max: item.points })}</FieldLabel>
                  <Input
                    id={fieldId}
                    type="number"
                    min={0}
                    max={item.points}
                    step={0.5}
                    className="w-28"
                    value={points[item.question.id] ?? item.earned?.toString() ?? ""}
                    onChange={(event) => {
                      setPoints({ ...points, [item.question.id]: event.target.value });
                    }}
                  />
                </Field>
                <Button type="submit" variant="secondary" size="sm" loading={grade.isPending}>
                  {t(labels.gradeAnswer)}
                </Button>
              </form>
            )}
          </li>
        );
      })}
    </ol>
  );
}
