import { ClipboardList, Clock } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { ContentText } from "@/components/japanese/ContentText";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Badge, Card, EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { quizKindLabels, quizzes as labels } from "@/i18n/labels/assessment";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/datetime";
import { useMyQuizzes, useStartAttempt, type StudentQuiz } from "../api/assessment";

/** /murid/kuis — published quizzes and exams of the student's classes (BR-KUIS-01/02). */
export function QuizListPage() {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const navigate = useNavigate();
  const list = useMyQuizzes();
  const start = useStartAttempt();
  const [confirming, setConfirming] = useState<StudentQuiz | null>(null);
  const when = (iso: string | null) => (iso ? formatDateTime(iso, uiLocale, me.timezone) : "–");

  const begin = (quiz: StudentQuiz) => {
    start.mutate(quiz.id, {
      onSuccess: (attempt) => {
        setConfirming(null);
        void navigate(`/murid/kuis/${attempt.id}`);
      },
      onError: (error) => {
        setConfirming(null);
        toast.error(t(errorLabel(errorCode(error))));
      },
    });
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={labels.title} companionClassName="text-body font-medium" />}
        description={t(labels.description)}
      />
      {list.isPending && <Skeleton className="h-40" />}
      {list.data?.length === 0 && (
        <EmptyState icon={ClipboardList} title={<Label value={labels.empty} />}>
          {t(labels.emptyStudent)}
        </EmptyState>
      )}
      <ul className="grid gap-4 md:grid-cols-2">
        {list.data?.map((quiz) => {
          const last = quiz.attempts.at(-1);
          const left = quiz.attemptsUsed < quiz.maxAttempts;
          const notYet = quiz.openAt !== null && new Date(quiz.openAt) > new Date();
          return (
            <li key={quiz.id}>
              <Card className="flex h-full flex-col gap-3">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge tone={quiz.kind === "exam" ? "solid" : "neutral"}>
                    {t(quizKindLabels[quiz.kind])}
                  </Badge>
                  <span className="text-caption text-ink-muted">{quiz.className}</span>
                </div>
                <h2 className="text-h3 font-semibold text-ink">
                  <ContentText value={quiz.title} />
                </h2>
                <p className="flex items-center gap-1 text-body-sm text-ink-muted">
                  <Clock className="size-4" aria-hidden="true" />
                  {quiz.timeLimitMin ? t(labels.minutes, { n: quiz.timeLimitMin }) : t(labels.noLimit)}
                  {" · "}
                  {t(labels.window, { open: when(quiz.openAt), close: when(quiz.closeAt) })}
                </p>
                <p className="text-body-sm text-ink">
                  {t(labels.attemptsUsed, { used: quiz.attemptsUsed, max: quiz.maxAttempts })}
                  {quiz.bestScorePct !== null && <> · {t(labels.best, { pct: quiz.bestScorePct })}</>}
                </p>
                <div className="mt-auto flex flex-wrap gap-2">
                  {quiz.inProgressAttemptId ? (
                    <Button
                      variant="primary"
                      onClick={() => {
                        void navigate(`/murid/kuis/${quiz.inProgressAttemptId ?? ""}`);
                      }}
                    >
                      {t(labels.resume)}
                    </Button>
                  ) : quiz.isOpen && left ? (
                    <Button
                      variant="primary"
                      onClick={() => {
                        setConfirming(quiz);
                      }}
                    >
                      {t(labels.start)}
                    </Button>
                  ) : (
                    <Badge>{notYet ? t(labels.notYetOpen) : t(labels.closed)}</Badge>
                  )}
                  {last && last.status !== "in_progress" && (
                    <Button
                      variant="outline"
                      onClick={() => {
                        void navigate(`/murid/kuis/${last.id}`);
                      }}
                    >
                      {t(labels.result)}
                    </Button>
                  )}
                </div>
              </Card>
            </li>
          );
        })}
      </ul>
      <ConfirmDialog
        open={confirming !== null}
        onOpenChange={(open) => {
          if (!open) setConfirming(null);
        }}
        title={t(labels.startConfirm)}
        description={t(labels.startBody)}
        confirmLabel={t(labels.start)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        busy={start.isPending}
        onConfirm={() => {
          if (confirming) begin(confirming);
        }}
      />
    </div>
  );
}
