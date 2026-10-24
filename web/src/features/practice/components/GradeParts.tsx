import { grades as labels } from "@/i18n/labels/assessment";
import { useT } from "@/i18n/prefs";
import type { ClassGrade } from "../api/assessment";

/** GradeBadge of docs/07 §10: the 0–4 grade large with the percentage, neutral black (a grade is not "wrong"). */
export function GradeBadge({ gpa, finalPct }: { gpa: number | null; finalPct: number | null }) {
  const t = useT();
  if (gpa === null || finalPct === null) {
    return <span className="text-body-sm text-ink-muted">{t(labels.noComponent)}</span>;
  }
  return (
    <span className="flex flex-col items-end">
      <span className="text-h1 font-bold text-ink tabular-nums">{gpa.toFixed(gpa % 1 === 0 ? 0 : 1)}</span>
      <span className="text-body-sm text-ink-muted tabular-nums">{finalPct.toFixed(2)}%</span>
    </span>
  );
}

/** Averages per component with the weight each one got (BR-NILAI-02 normalizes missing components away). */
export function GradeBreakdownList({ breakdown }: { breakdown: ClassGrade["breakdown"] }) {
  const t = useT();
  const weights = breakdown.weightsUsed as Record<string, number | undefined>;
  const rows = [
    { key: "quiz", label: labels.quizAvg, value: breakdown.quizAvg },
    { key: "exam", label: labels.examAvg, value: breakdown.examAvg },
    { key: "assignment", label: labels.assignmentAvg, value: breakdown.assignmentAvg },
  ] as const;
  return (
    <dl className="grid grid-cols-[1fr_auto] gap-x-4 gap-y-1 text-body-sm">
      {rows.map((row) => (
        <div key={row.key} className="contents">
          <dt className="text-ink-muted">
            {t(row.label)}
            {weights[row.key] !== undefined && <> · {t(labels.weight, { w: weights[row.key] ?? 0 })}</>}
          </dt>
          <dd className="text-right text-ink tabular-nums">
            {row.value === null ? "–" : `${row.value.toFixed(2)}%`}
          </dd>
        </div>
      ))}
    </dl>
  );
}
