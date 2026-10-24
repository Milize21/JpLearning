import { Flame, Info, X } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Alert, Skeleton } from "@/components/ui/misc";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { drill } from "@/i18n/labels/practice";
import { useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import {
  fetchNextBatch,
  useEndDrill,
  useSubmitAnswer,
  type AnswerResult,
  type DrillSummary,
  type SafeQuestion,
} from "../api/practice";
import { AnswerControls, type DrillAnswer } from "../components/AnswerControls";
import { AnswerFeedback } from "../components/AnswerFeedback";
import { DrillSummaryView } from "../components/DrillSummaryView";
import { QuestionPrompt, QuestionText } from "../components/QuestionText";

const AUTO_NEXT_KEY = "nomax.drill.autoNext";
const AUTO_NEXT_MS = 800;

interface StartState {
  batch?: SafeQuestion[];
  fallback?: string | null;
}

function readAutoNext(): boolean {
  try {
    return window.localStorage.getItem(AUTO_NEXT_KEY) !== "0";
  } catch {
    return true;
  }
}

/** UUIDv7 made on the client so a resend of the same answer is idempotent (BR-DRILL-07). */
function uuidv7(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  let time = Date.now();
  for (let i = 5; i >= 0; i--) {
    bytes[i] = time % 256;
    time = Math.floor(time / 256);
  }
  bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x70;
  bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80;
  const hex = [...bytes].map((byte) => byte.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

/**
 * /murid/latihan/:sessionId â€” focus mode without navigation (docs/07 Â§6, Â§11 flow 3): one question at a time, instant
 * feedback â—‹/Ã— from the server, shortcuts 1â€“6 / Enter / Space / Esc, and a summary with a hanko stamp at the end.
 */
export function DrillPage() {
  const t = useT();
  const { sessionId = "" } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const start = (location.state ?? {}) as StartState;
  const submit = useSubmitAnswer(sessionId);
  const end = useEndDrill(sessionId);

  const [queue, setQueue] = useState<SafeQuestion[]>(start.batch ?? []);
  const [loading, setLoading] = useState(!start.batch?.length);
  const [result, setResult] = useState<AnswerResult | null>(null);
  const [answered, setAnswered] = useState(0);
  const [streak, setStreak] = useState(0);
  const [summary, setSummary] = useState<DrillSummary | null>(null);
  const [exiting, setExiting] = useState(false);
  const [autoNext, setAutoNext] = useState(readAutoNext);
  const [shownAt, setShownAt] = useState(() => Date.now());
  const pendingAnswer = useRef<{ id: string; questionId: string } | null>(null);
  const nextRef = useRef<HTMLButtonElement>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const question = queue[0];

  const finish = useCallback(async () => {
    try {
      setSummary(await end.mutateAsync());
    } catch (error) {
      toast.error(t(errorLabel(errorCode(error))));
      void navigate("/murid/latihan");
    }
  }, [end, navigate, t]);

  const applyBatch = useCallback(
    async (questions: SafeQuestion[]) => {
      setLoading(false);
      if (questions.length === 0) {
        await finish();
        return;
      }
      setQueue(questions);
      setShownAt(Date.now());
    },
    [finish],
  );

  const batchFailed = useCallback(
    async (error: unknown) => {
      setLoading(false);
      const code = errorCode(error);
      if (code === "session_ended" || code === "empty_pool") await finish();
      else toast.error(t(errorLabel(code)));
    },
    [finish, t],
  );

  // Callers set `loading` first; the first render already starts with it when there is no batch.
  const loadMore = useCallback(
    () => fetchNextBatch(sessionId).then((next) => applyBatch(next.questions), batchFailed),
    [applyBatch, batchFailed, sessionId],
  );

  // A reload has no batch in the router state: fetch one. State changes only in the promise callbacks.
  useEffect(() => {
    if (start.batch?.length) return;
    void fetchNextBatch(sessionId).then((next) => applyBatch(next.questions), batchFailed);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only on the first render of this session
  }, []);

  const goNext = useCallback(() => {
    setResult(null);
    pendingAnswer.current = null;
    const rest = queue.slice(1);
    if (rest.length === 0) {
      setQueue([]);
      setLoading(true);
      void loadMore();
    } else {
      setQueue(rest);
      setShownAt(Date.now());
    }
  }, [loadMore, queue]);

  useEffect(() => {
    if (question && !result) headingRef.current?.focus();
  }, [question, result]);

  useEffect(() => {
    if (!result) return;
    nextRef.current?.focus();
    if (result.isCorrect && autoNext) {
      const timer = window.setTimeout(goNext, AUTO_NEXT_MS);
      return () => {
        window.clearTimeout(timer);
      };
    }
    return undefined;
  }, [result, autoNext, goNext]);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (summary) return;
      if (event.key === "Escape") {
        event.preventDefault();
        setExiting(true);
      } else if (event.key === "Enter" && result && document.activeElement?.tagName !== "BUTTON") {
        event.preventDefault();
        goNext();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("keydown", onKey);
    };
  }, [goNext, result, summary]);

  const answer = async (value: DrillAnswer) => {
    if (!question || submit.isPending) return;
    // A retry after a network error reuses the same id, so the server counts it once.
    pendingAnswer.current ??= { id: uuidv7(), questionId: question.id };
    try {
      const response = await submit.mutateAsync({
        answerId: pendingAnswer.current.id,
        questionId: question.id,
        answer: value,
        responseMs: Math.max(0, Date.now() - shownAt),
      });
      setResult(response);
      setAnswered((count) => count + 1);
      setStreak((count) => (response.isCorrect ? count + 1 : 0));
    } catch (error) {
      const code = errorCode(error);
      if (code === "session_ended") {
        await finish();
        return;
      }
      if (code === "chapter_locked" || code === "not_found") {
        // The chapter was closed meanwhile: skip the question.
        toast.error(t(errorLabel(code)));
        goNext();
        return;
      }
      toast.error(t(errorLabel(code)));
    }
  };

  if (summary) return <DrillSummaryView summary={summary} />;

  return (
    <div className="mx-auto flex min-h-dvh max-w-3xl flex-col">
      <header className="flex items-center justify-between gap-3 border-b border-line px-4 py-3">
        <Button
          variant="ghost"
          size="icon"
          aria-label={t(drill.exit)}
          onClick={() => {
            setExiting(true);
          }}
        >
          <X aria-hidden="true" />
        </Button>
        <span className="text-body-sm font-semibold text-ink" aria-live="polite">
          {t(drill.answered, { n: answered })}
        </span>
        <span className="flex items-center gap-1 text-body-sm font-semibold text-ink">
          <Flame className="size-4 text-action" aria-hidden="true" />
          {t(drill.streak, { n: streak })}
        </span>
      </header>

      <div className="flex flex-1 flex-col gap-6 px-4 py-6">
        {start.fallback === "normal" && answered === 0 && (
          <Alert icon={Info} tone="info">
            {t(drill.fallback)}
          </Alert>
        )}
        {(loading || !question) && (
          <div className="flex flex-col gap-4" aria-busy="true">
            <span className="sr-only">{t(drill.loading)}</span>
            <Skeleton className="h-8 w-2/3" />
            <Skeleton className="h-32" />
            <Skeleton className="h-24" />
          </div>
        )}
        {!loading && question && (
          <article key={question.id} className="flex flex-col gap-6" aria-labelledby="drill-question">
            <h1 id="drill-question" ref={headingRef} tabIndex={-1} className="focus:outline-none">
              <QuestionPrompt value={question.prompt} />
            </h1>
            {question.stimulus && (
              <div className="flex justify-center py-4 text-center">
                <QuestionText value={question.stimulus} display={question.jaDisplay} large />
              </div>
            )}
            <AnswerControls
              question={question}
              disabled={result !== null || submit.isPending}
              onAnswer={(value) => void answer(value)}
            />
            {result && (
              <AnswerFeedback question={question} result={result} onNext={goNext} nextRef={nextRef} />
            )}
          </article>
        )}
      </div>

      <footer className="flex flex-wrap items-center justify-between gap-2 border-t border-line px-4 py-3 text-caption text-ink-muted">
        <span className="hidden sm:inline">{t(drill.shortcuts)}</span>
        <label className="flex items-center gap-2">
          <input
            type="checkbox"
            className="size-4 accent-sumi-900"
            checked={autoNext}
            onChange={(event) => {
              setAutoNext(event.target.checked);
              try {
                window.localStorage.setItem(AUTO_NEXT_KEY, event.target.checked ? "1" : "0");
              } catch {
                // Only a convenience; the choice simply is not remembered.
              }
            }}
          />
          {t(drill.autoNext)}
        </label>
      </footer>

      <ConfirmDialog
        open={exiting}
        onOpenChange={setExiting}
        title={t(drill.exitTitle)}
        description={t(drill.exitBody)}
        confirmLabel={t(drill.exitConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        busy={end.isPending}
        onConfirm={() => {
          setExiting(false);
          void finish();
        }}
      />
    </div>
  );
}
