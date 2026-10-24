import { CircleCheck, CircleDashed, CircleDot, TriangleAlert } from "lucide-react";
import { JapaneseText } from "@/components/japanese/JapaneseText";
import { Label } from "@/components/japanese/Label";
import { masteryStatusLabels, weaknesses } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn, toSegments } from "@/i18n/textblock";
import { cn } from "@/lib/utils";
import type { MasteryItem, MasteryStatus as Status } from "../api/practice";

const statusIcons = {
  not_started: CircleDashed,
  learning: CircleDot,
  weak: TriangleAlert,
  mastered: CircleCheck,
} as const;

/** MasteryStatus of docs/07 §10: icon + label; weak = danger, mastered = success, others neutral. */
export function MasteryStatus({ status }: { status: Status }) {
  const t = useT();
  const Icon = statusIcons[status];
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 text-caption font-semibold",
        status === "weak" ? "text-danger" : status === "mastered" ? "text-success" : "text-ink-muted",
      )}
    >
      <Icon className="size-4" aria-hidden="true" />
      {t(masteryStatusLabels[status])}
    </span>
  );
}

/** One item with its label, mastery bar and status. */
export function MasteryItemRow({ item }: { item: MasteryItem }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const segments = toSegments(item.label?.ja);
  const percent = Math.round(item.mastery * 100);
  return (
    <li className="flex items-center gap-3 px-4 py-3">
      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="flex flex-wrap items-baseline gap-2 text-ink">
          {segments.length > 0 && <JapaneseText value={segments} className="text-h3 font-learn" />}
          <span className="text-body">{textIn(item.label, uiLocale)}</span>
        </span>
        <span className="text-caption text-ink-muted">
          {t(weaknesses.attempts, { n: item.attempts })} ·{" "}
          {t(weaknesses.accuracy, {
            pct: item.attempts ? Math.round((item.correct / item.attempts) * 100) : 0,
          })}
        </span>
      </span>
      <span className="flex w-28 shrink-0 flex-col items-end gap-1">
        <MasteryStatus status={item.status} />
        <span
          className="h-1.5 w-full overflow-hidden rounded-full bg-sumi-200"
          role="meter"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={percent}
          aria-label={t(weaknesses.averageMastery, { pct: percent })}
        >
          <span className="block h-full bg-action" style={{ width: `${String(percent)}%` }} />
        </span>
      </span>
    </li>
  );
}

/** Heading for an item list with its Japanese companion. */
export function ItemListHeading({ value, id }: { value: typeof weaknesses.items; id: string }) {
  return (
    <h2 id={id} className="text-body font-bold text-ink">
      <Label value={value} companion="inline" companionClassName="text-body-sm font-medium" />
    </h2>
  );
}
