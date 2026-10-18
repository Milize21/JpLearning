import { Tooltip } from "radix-ui";
import { usePrefs } from "@/i18n/prefs";
import { formatLabel, type LabelVars, type UiLabel } from "@/i18n/tb";
import { cn } from "@/lib/utils";
import { JapaneseText } from "./JapaneseText";

/** Where the Japanese companion goes (docs/06 §8). */
export type Companion = "below" | "inline" | "tooltip" | "none";

export interface LabelProps {
  value: UiLabel;
  companion?: Companion;
  vars?: LabelVars;
  className?: string;
  companionClassName?: string;
}

/**
 * UI text in the user's language plus its Japanese companion. Every visible UI string goes through this component
 * or useT() (AGENTS.md rule 5).
 */
export function Label({ value, companion = "below", vars, className, companionClassName }: LabelProps) {
  const { uiLocale, scriptPref } = usePrefs();
  const text = formatLabel(value, uiLocale, vars);
  const showCompanion = companion !== "none" && scriptPref !== "off";
  const japanese = (
    <JapaneseText
      value={value}
      vars={vars}
      variant="companion"
      className={cn(
        companion === "below" && "block text-caption font-medium text-ink-muted",
        companion === "inline" && "text-ink-muted",
        companionClassName,
      )}
    />
  );

  if (!showCompanion) return <span className={className}>{text}</span>;

  if (companion === "tooltip") {
    return (
      <Tooltip.Provider delayDuration={300}>
        <Tooltip.Root>
          <Tooltip.Trigger asChild>
            <span className={className}>{text}</span>
          </Tooltip.Trigger>
          <Tooltip.Portal>
            <Tooltip.Content
              sideOffset={6}
              className="z-50 rounded-md bg-sumi-900 px-2 py-1 text-body-sm text-ink-inverse shadow-md"
            >
              <JapaneseText value={value} vars={vars} variant="companion" />
            </Tooltip.Content>
          </Tooltip.Portal>
        </Tooltip.Root>
      </Tooltip.Provider>
    );
  }

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
