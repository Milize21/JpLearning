import { Check, ChevronLeft, CircleAlert, Clock, Hourglass } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router";
import { ContentText } from "@/components/japanese/ContentText";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Field, FieldLabel, Textarea } from "@/components/ui/form";
import { Alert, Badge, Card, EmptyState, Skeleton } from "@/components/ui/misc";
import { attempt as labels } from "@/i18n/labels/assessment";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import {
  saveAttemptAnswer,
  useAttempt,
  useSubmitAttempt,
  type Attempt,
  type AttemptQuestion,
} from "../api/assessment";
import { AnswerControls, type DrillAnswer } from "../components/AnswerControls";
import { AnswerFeedback } from "../components/AnswerFeedback";
import { answerSummary } from "../components/answerSummary";
import { HankoStamp } from "../components/HankoStamp";
import { QuestionPrompt, QuestionText } from "../components/QuestionText";

/**
 * /murid/kuis/:attemptId — focus mode (docs/07 §11 flow 4): one question at a time with a navigator, autosave on every
 * answer (BR-KUIS-06), the server timer (BR-KUIS-03) and, after submitting, the score and the review of BR-KUIS-09.
 */
export function QuizAttemptPage() {
  const t = useT();
  const { attemptId = "" } = useParams();
  const query = useAttempt(attemptId);

  if (query.isPending) {
    return (
      <div className="mx-auto max-w-3xl p-6">
        <Skeleton className="h-64" />
      </div>
    );
  }
  if (!query.data) {
    return (
      <div className="mx-auto max-w-3xl p-6">
        <EmptyState icon={CircleAlert} title={t(errorLabel(errorCode(query.error)))} />
      </div>
    );
  }

  return query.data.status === "in_progress" ? (
    <TakingView key={query.data.id} attempt={query.data} />
  ) : (
    <ResultView attempt={query.data} />
  );
}

function formatClock(ms: number): string {
  const total = Math.max(0, Math.floor(ms / 1000));
  const minutes = Math.floor(total / 60);
  const seconds = total % 60;
  return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

function TakingView({ attempt }: { attempt: Attempt }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const submit = useSubmitAttempt(attempt.id);
  const [index, setIndex] = useState(0);
  const [answers, setAnswers] = useState<Record<string, unknown>>(() =>
    Object.fromEntries(
      attempt.questions.filter((item) => item.answer != null).map((item) => [item.question.id, item.answer]),
    ),
  );
  const [saving, setSaving] = useState<"idle" | "saving" | "saved" | "failed">("idle");
  const [confirming, setConfirming] = useState(false);
  const [round, setRound] = useState(0);
  // Server time minus local time, so the countdown follows the server clock (BR-KUIS-03).
  const [offset] = useState(() => new Date(attempt.serverNow).getTime() - Date.now());
  const [now, setNow] = useState(() => Date.now());
  const submitted = useRef(false);
  const current = attempt.questions[index];
  const remaining = attempt.expiresAt ? new Date(attempt.expiresAt).getTime() - (now + offset) : null;
  const unanswered = attempt.questions.filter((item) => answers[item.question.id] === undefined).length;

  useEffect(() => {
    if (attempt.expiresAt === null) return;
    const timer = window.setInterval(() => {
      setNow(Date.now());
    }, 1000);
    return () => {
      window.clearInterval(timer);
    };
  }, [attempt.expiresAt]);

  useEffect(() => {
    if (remaining !== null && remaining <= 0 && !submitted.current) {
      submitted.current = true;
      submit.mutate();
    }
  }, [remaining, submit]);

  const save = async (questionId: string, value: unknown) => {
    setSaving("saving");
    try {
      await saveAttemptAnswer(attempt.id, questionId, value);
      setAnswers((all) => ({ ...all, [questionId]: value }));
      setSaving("saved");
    } catch (error) {
      setSaving("failed");
      if (errorCode(error) === "attempt_expired" && !submitted.current) {
        submitted.current = true;
        submit.mutate();
      }
    }
  };

  if (!current) return null;
  const minuteWarning = remaining !== null && remaining < 5 * 60_000;
  const lastMinute = remaining !== null && remaining < 60_000;

  return (
    <div className="mx-auto flex min-h-dvh max-w-3xl flex-col">
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-line px-4 py-3">
        <h1 className="min-w-0 text-body font-semibold text-ink">
          <ContentText value={attempt.quizTitle} companion="none" />
        </h1>
        <div className="flex items-center gap-2">
          <span className="text-caption text-ink-muted" aria-live="polite">
            {saving === "saving"
              ? t(labels.saving)
              : saving === "saved"
                ? t(labels.saved)
                : saving === "failed"
                  ? t(labels.saveFailed)
                  : ""}
          </span>
          {remaining !== null && (
            <Badge
              tone={lastMinute ? "danger" : minuteWarning ? "warning" : "neutral"}
              className="text-body-sm"
            >
              <Clock className="size-4" aria-hidden="true" />
              {t(labels.timeLeft, { time: formatClock(remaining) })}
            </Badge>
          )}
        </div>
      </header>

      <nav aria-label={t(labels.navigator)} className="border-b border-line px-4 py-3">
        <ol className="flex flex-wrap gap-2">
          {attempt.questions.map((item, position) => {
            const done = answers[item.question.id] !== undefined;
            return (
              <li key={item.question.id}>
                <button
                  type="button"
                  aria-current={position === index ? "step" : undefined}
                  aria-label={`${t(labels.question, { n: position + 1 })} · ${t(done ? labels.answered : labels.unanswered)}`}
                  className={cn(
                    "relative inline-flex size-10 items-center justify-center rounded-md border text-body-sm font-semibold",
                    "focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none",
                    position === index
                      ? "border-2 border-sumi-900 text-ink"
                      : "border-line-strong text-ink-muted",
                    done && "bg-canvas-muted",
                  )}
                  onClick={() => {
                    setIndex(position);
                    setRound(round + 1);
                  }}
                >
                  {position + 1}
                  {done && (
                    <Check className="absolute -top-1 -right-1 size-3.5 text-success" aria-hidden="true" />
                  )}
                </button>
              </li>
            );
          })}
        </ol>
      </nav>

      <article className="flex flex-1 flex-col gap-6 px-4 py-6" aria-labelledby="quiz-question">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 id="quiz-question" className="text-body font-bold text-ink">
            {t(labels.question, { n: index + 1 })}
          </h2>
          <span className="text-caption text-ink-muted">{current.points}</span>
        </div>
        <QuestionPrompt value={current.question.prompt} />
        {current.question.stimulus && (
          <div className="flex justify-center py-2 text-center">
            <QuestionText value={current.question.stimulus} display={current.question.jaDisplay} large />
          </div>
        )}
        {current.question.kind === "long_answer" ? (
          <LongAnswer
            key={current.question.id}
            initial={(answers[current.question.id] as { text?: string } | undefined)?.text ?? ""}
            maxChars={current.question.maxChars ?? 2000}
            onSave={(text) => void save(current.question.id, { text })}
          />
        ) : (
          <AnswerControls
            key={`${current.question.id}:${String(round)}`}
            question={current.question}
            disabled={submit.isPending}
            onAnswer={(value: DrillAnswer) => void save(current.question.id, value)}
          />
        )}
        {answers[current.question.id] !== undefined && current.question.kind !== "long_answer" && (
          <p className="text-body-sm text-ink-muted">
            {t(labels.savedAnswer, {
              value: answerSummary(current.question, answers[current.question.id], uiLocale),
            })}
          </p>
        )}
      </article>

      <footer className="flex flex-wrap items-center justify-between gap-2 border-t border-line px-4 py-3">
        <div className="flex gap-2">
          <Button
            variant="outline"
            disabled={index === 0}
            onClick={() => {
              setIndex(index - 1);
            }}
          >
            {t(labels.previous)}
          </Button>
          <Button
            variant="outline"
            disabled={index === attempt.questions.length - 1}
            onClick={() => {
              setIndex(index + 1);
            }}
          >
            {t(labels.next)}
          </Button>
        </div>
        <Button
          variant="primary"
          loading={submit.isPending}
          onClick={() => {
            setConfirming(true);
          }}
        >
          {t(labels.submit)}
        </Button>
      </footer>

      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t(labels.submitTitle)}
        description={t(labels.submitBody, { n: unanswered })}
        confirmLabel={t(labels.submitConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        busy={submit.isPending}
        onConfirm={() => {
          submitted.current = true;
          submit.mutate(undefined, {
            onSettled: () => {
              setConfirming(false);
            },
          });
        }}
      />
    </div>
  );
}

function LongAnswer({
  initial,
  maxChars,
  onSave,
}: {
  initial: string;
  maxChars: number;
  onSave: (text: string) => void;
}) {
  const t = useT();
  const [text, setText] = useState(initial);
  return (
    <form
      noValidate
      className="flex flex-col gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        onSave(text);
      }}
    >
      <Field>
        <FieldLabel htmlFor="long-answer">{t(labels.yourAnswer)}</FieldLabel>
        <Textarea
          id="long-answer"
          rows={8}
          maxLength={maxChars}
          placeholder={t(labels.longPlaceholder)}
          value={text}
          onChange={(event) => {
            setText(event.target.value);
          }}
          onBlur={() => {
            if (text !== initial) onSave(text);
          }}
        />
        <span className="text-caption text-ink-muted">
          {text.length}/{maxChars}
        </span>
      </Field>
      <Button type="submit" variant="secondary" className="self-end">
        {t(labels.saveLong)}
      </Button>
    </form>
  );
}

function ResultView({ attempt }: { attempt: Attempt }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const score = attempt.scorePct;

  return (
    <div className="mx-auto flex min-h-dvh max-w-3xl flex-col gap-6 px-4 py-8">
      <Link
        to="/murid/kuis"
        className="inline-flex items-center gap-1 self-start rounded-sm text-body-sm text-ink-muted hover:text-ink focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
      >
        <ChevronLeft className="size-4" aria-hidden="true" />
        {t(labels.backToList)}
      </Link>
      <h1 className="text-h2 font-bold text-ink">
        <Label value={labels.resultTitle} companionClassName="text-body font-medium" />
      </h1>
      <Card className="flex flex-col items-center gap-4 sm:flex-row sm:justify-between">
        <div className="flex flex-col gap-1">
          <ContentText value={attempt.quizTitle} className="text-body text-ink-muted" companion="none" />
          {score !== null ? (
            <span className="text-h1 font-bold text-ink tabular-nums">{t(labels.score, { pct: score })}</span>
          ) : (
            <Alert tone="info" icon={Hourglass}>
              {t(labels.waitingManual)}
            </Alert>
          )}
        </div>
        {score !== null && <HankoStamp accuracy={score / 100} />}
      </Card>
      {!attempt.reviewVisible && score !== null && (
        <p className="text-body-sm text-ink-muted">{t(labels.reviewHidden)}</p>
      )}
      {attempt.reviewVisible && (
        <ol className="flex flex-col gap-4">
          {attempt.questions.map((item, position) => (
            <ReviewItem key={item.question.id} item={item} position={position} locale={uiLocale} />
          ))}
        </ol>
      )}
    </div>
  );
}

function ReviewItem({
  item,
  position,
  locale,
}: {
  item: AttemptQuestion;
  position: number;
  locale: "id" | "en";
}) {
  const t = useT();
  return (
    <li className="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-body font-bold text-ink">{t(labels.question, { n: position + 1 })}</h2>
        {item.earned !== null && (
          <span className="text-body-sm text-ink-muted">
            {t(labels.earned, { earned: item.earned, points: item.points })}
          </span>
        )}
      </div>
      <QuestionPrompt value={item.question.prompt} />
      <p className="text-body-sm text-ink">
        {t(labels.savedAnswer, {
          value: item.answer == null ? "–" : answerSummary(item.question, item.answer, locale),
        })}
      </p>
      {item.isCorrect === null ? (
        <Alert tone="info" icon={Hourglass}>
          {t(labels.waitingManual)}
        </Alert>
      ) : (
        <AnswerFeedback
          question={item.question}
          result={{
            isCorrect: item.isCorrect,
            correctAnswer: item.correctAnswer,
            explanation: item.explanation,
            mastery: null,
          }}
        />
      )}
    </li>
  );
}
