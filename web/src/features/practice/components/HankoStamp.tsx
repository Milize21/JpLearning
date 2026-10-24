import { JapaneseText } from "@/components/japanese/JapaneseText";
import { summary } from "@/i18n/labels/practice";
import { useT } from "@/i18n/prefs";
import { cn } from "@/lib/utils";

/** Stamp of docs/07 §8 by accuracy: ≥ 80 % よくできました, 50–79 % がんばりました, < 50 % がんばりましょう. */
function stampFor(accuracy: number) {
  if (accuracy >= 0.8) return summary.stampGreat;
  if (accuracy >= 0.5) return summary.stampTried;
  return summary.stampKeepGoing;
}

/** HankoStamp: red outlined circle tilted −8°, Japanese text in aka-700; aria-label carries the meaning. */
export function HankoStamp({ accuracy, className }: { accuracy: number; className?: string }) {
  const t = useT();
  const label = stampFor(accuracy);
  return (
    <div
      role="img"
      aria-label={t(label)}
      className={cn(
        "flex size-36 -rotate-[8deg] items-center justify-center rounded-full border-4 border-aka-700 p-3 text-center text-aka-700",
        "motion-safe:transition-transform motion-safe:duration-[var(--nx-duration-slow)]",
        className,
      )}
    >
      <JapaneseText
        value={label}
        variant="content"
        scriptPref="off"
        className="text-h3 font-bold leading-tight"
      />
    </div>
  );
}
