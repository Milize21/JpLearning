import { usePrefs } from "@/i18n/prefs";
import { textIn, toSegments, type ApiTextBlock } from "@/i18n/textblock";
import { cn } from "@/lib/utils";
import { JapaneseText } from "./JapaneseText";
import type { Companion } from "./Label";

/**
 * A content TextBlock (titles of levels, books, chapters, materials): the text in the UI language with its Japanese
 * companion, following the user's script preference (docs/06 §4, §8). Labels of the UI itself use {@link Label}.
 */
export function ContentText({
  value,
  companion = "below",
  className,
  companionClassName,
}: {
  value: ApiTextBlock | null | undefined;
  companion?: Exclude<Companion, "tooltip">;
  className?: string;
  companionClassName?: string;
}) {
  const { uiLocale, scriptPref } = usePrefs();
  const text = textIn(value, uiLocale);
  const segments = toSegments(value?.ja);
  const showCompanion = companion !== "none" && scriptPref !== "off" && segments.length > 0;

  if (!showCompanion) return <span className={className}>{text}</span>;

  const japanese = (
    <JapaneseText
      value={segments}
      variant="companion"
      className={cn(
        companion === "below" && "block text-caption font-medium text-ink-muted",
        companion === "inline" && "text-ink-muted",
        companionClassName,
      )}
    />
  );

  if (companion === "inline") {
    return (
      <span className={className}>
        {text} <span aria-hidden="true">· </span>
        {japanese}
      </span>
    );
  }

  return (
    <span className={cn("inline-flex flex-col", className)}>
      <span>{text}</span>
      {japanese}
    </span>
  );
}
