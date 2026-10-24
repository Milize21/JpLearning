import { ChevronRight, Users } from "lucide-react";
import { Link } from "react-router";
import { useCurrentUser } from "@/app/current-user";
import { roleHome } from "@/app/roles";
import { Label } from "@/components/japanese/Label";
import { EmptyState, Skeleton } from "@/components/ui/misc";
import { useClassActivitySummary } from "@/features/insight/api/insight";
import { activity } from "@/i18n/labels/insight";
import { progress } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { formatDateTime } from "@/lib/datetime";
import { useClassProgress } from "../api/practice";
import { GroupName } from "./WeaknessList";

function latest(a: string | null | undefined, b: string | null | undefined): string | null {
  if (!a) return b ?? null;
  if (!b) return a;
  return Date.parse(a) >= Date.parse(b) ? a : b;
}

/**
 * "Progres" tab (docs/07 §11 flow 7): per student the last practice, answers in 7 days, accuracy, average mastery and
 * top weakness. Rows lead to the student detail; table on desktop, cards on phones.
 */
export function ClassProgressPanel({ classId }: { classId: string }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const data = useClassProgress(classId);
  // Phase 4: "terakhir aktif" comes from Insight; the last drill answer covers activity from before it (no backfill).
  const summary = useClassActivitySummary(classId);
  const lastActive = new Map((summary.data ?? []).map((row) => [row.studentId, row.lastActiveAt]));
  const formatLast = (iso: string | null) =>
    iso ? formatDateTime(iso, uiLocale, me.timezone) : t(progress.never);
  const pct = (value: number | null) => (value === null ? "–" : `${String(Math.round(value * 100))}%`);
  const base = roleHome[me.role];

  if (data.isPending) return <Skeleton className="h-48" />;
  if (!data.data || data.data.students.length === 0) {
    return <EmptyState icon={Users} title={<Label value={progress.empty} />} />;
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-body-sm text-ink-muted">{t(progress.description)}</p>
      {/* Column headings for the desktop grid; each phone card names its values itself (sr-only above md). */}
      <p
        className="hidden gap-2 px-4 text-caption font-semibold text-ink-muted md:grid md:grid-cols-[minmax(0,1.5fr)_repeat(4,minmax(0,1fr))_minmax(0,1.5fr)_auto]"
        aria-hidden="true"
      >
        <span>{t(progress.student)}</span>
        <span>{t(activity.lastActive)}</span>
        <span>{t(progress.answers7d)}</span>
        <span>{t(progress.accuracy)}</span>
        <span>{t(progress.averageMastery)}</span>
        <span>{t(progress.topWeakness)}</span>
        <span className="w-4" />
      </p>
      <ul className="flex flex-col divide-y divide-line rounded-lg border border-line bg-surface">
        {data.data.students.map((student) => (
          <li key={student.studentId}>
            <Link
              to={`${base}/murid/${student.studentId}?classId=${classId}`}
              className="grid gap-2 px-4 py-3 hover:bg-canvas-subtle focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none focus-visible:ring-inset md:grid-cols-[minmax(0,1.5fr)_repeat(4,minmax(0,1fr))_minmax(0,1.5fr)_auto] md:items-center"
            >
              <span className="font-semibold text-ink">{student.displayName}</span>
              <span className="text-body-sm text-ink-muted">
                <span className="md:sr-only">{t(activity.lastActive)}: </span>
                {formatLast(latest(lastActive.get(student.studentId), student.lastPracticedAt))}
              </span>
              <span className="text-body-sm text-ink tabular-nums">
                <span className="md:sr-only">{t(progress.answers7d)}: </span>
                {student.answersLast7Days}
              </span>
              <span className="text-body-sm text-ink tabular-nums">
                <span className="md:sr-only">{t(progress.accuracy)}: </span>
                {pct(student.accuracy)}
              </span>
              <span className="text-body-sm text-ink tabular-nums">
                <span className="md:sr-only">{t(progress.averageMastery)}: </span>
                {pct(student.averageMastery)}
              </span>
              <span className="text-body-sm text-ink-muted">
                <span className="md:sr-only">{t(progress.topWeakness)}: </span>
                {student.topWeakness ? <GroupName group={student.topWeakness} /> : "–"}
              </span>
              <ChevronRight className="hidden size-4 text-ink-subtle md:block" aria-hidden="true" />
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
