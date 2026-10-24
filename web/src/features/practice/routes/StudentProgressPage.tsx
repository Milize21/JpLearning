import { ChevronLeft, CircleAlert } from "lucide-react";
import { Link, useParams, useSearchParams } from "react-router";
import { useCurrentUser } from "@/app/current-user";
import { roleHome } from "@/app/roles";
import { Label } from "@/components/japanese/Label";
import { EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { errorLabel } from "@/i18n/labels/errors";
import { categoryLabels, progress, setup, weaknesses } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/datetime";
import { useComments, useStudentSessions, useStudentWeaknesses } from "../api/practice";
import { CommentForm, CommentList } from "../components/Comments";
import { WeaknessList } from "../components/WeaknessList";

/**
 * /sensei/murid/:studentId (and /admin) — weaknesses per category/tag/chapter, recent sessions and comments without a
 * grade (docs/07 §11 flow 7). Only teachers of the student and admins get data; others see "not found".
 */
export function StudentProgressPage() {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const { studentId = "" } = useParams();
  const [params] = useSearchParams();
  const classId = params.get("classId") ?? undefined;
  const weak = useStudentWeaknesses(studentId, classId);
  const sessions = useStudentSessions(studentId);
  const comments = useComments({ studentId });
  const back = classId ? `${roleHome[me.role]}/kelas/${classId}/progres` : `${roleHome[me.role]}/kelas`;

  return (
    <div className="flex flex-col gap-6">
      <Link
        to={back}
        className="inline-flex items-center gap-1 self-start rounded-sm text-body-sm text-ink-muted hover:text-ink focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
      >
        <ChevronLeft className="size-4" aria-hidden="true" />
        {t(progress.back)}
      </Link>
      <PageHeader title={<Label value={progress.detail} companionClassName="text-body font-medium" />} />
      {weak.isPending && <Skeleton className="h-48" />}
      {weak.isError && <EmptyState icon={CircleAlert} title={t(errorLabel(errorCode(weak.error)))} />}
      {weak.data && weak.data.groups.length === 0 && (
        <p className="text-body-sm text-ink-muted">{t(weaknesses.empty)}</p>
      )}
      {weak.data && weak.data.groups.length > 0 && <WeaknessList data={weak.data} />}

      {!weak.isError && (
        <>
          <section className="flex flex-col gap-3" aria-labelledby="student-sessions">
            <h2 id="student-sessions" className="text-body font-bold text-ink">
              <Label
                value={progress.sessions}
                companion="inline"
                companionClassName="text-body-sm font-medium"
              />
            </h2>
            {sessions.data?.items.length === 0 && (
              <p className="text-body-sm text-ink-muted">{t(progress.noSessions)}</p>
            )}
            <ul className="flex flex-col gap-3">
              {sessions.data?.items.map((session) => (
                <li
                  key={session.id}
                  className="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4"
                >
                  <p className="flex flex-wrap items-center justify-between gap-2 text-body text-ink">
                    <span className="font-semibold">
                      {t(categoryLabels[session.category])} ·{" "}
                      {formatDateTime(session.startedAt, uiLocale, me.timezone)}
                    </span>
                    <span className="text-body-sm text-ink-muted">
                      {t(setup.sessionLine, { correct: session.correctCount, total: session.answerCount })}
                    </span>
                  </p>
                  <details>
                    <summary className="cursor-pointer text-body-sm font-semibold text-ink">
                      {t(progress.forSession)}
                    </summary>
                    <div className="mt-2 flex flex-col gap-3">
                      <CommentList
                        comments={comments.data?.filter((comment) => comment.targetId === session.id)}
                        loading={comments.isPending}
                      />
                      <CommentForm studentId={studentId} targetType="drill_session" targetId={session.id} />
                    </div>
                  </details>
                </li>
              ))}
            </ul>
          </section>

          {classId && (
            <section className="flex flex-col gap-3" aria-labelledby="student-comments">
              <h2 id="student-comments" className="text-body font-bold text-ink">
                <Label
                  value={progress.comments}
                  companion="inline"
                  companionClassName="text-body-sm font-medium"
                />
              </h2>
              <CommentList
                comments={comments.data?.filter((comment) => comment.targetType === "progress")}
                loading={comments.isPending}
              />
              <CommentForm studentId={studentId} targetType="progress" targetId={classId} />
            </section>
          )}
        </>
      )}
    </div>
  );
}
