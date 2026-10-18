import { Fragment, type ReactNode } from "react";
import { kanaText, plainText } from "@/i18n/furigana";
import { usePrefs } from "@/i18n/prefs";
import { toRomaji } from "@/i18n/romaji";
import { labelSegments, type LabelVars, type UiLabel } from "@/i18n/tb";
import type { JaDisplay, JaSegment, ScriptPref, TextBlock } from "@/i18n/types";
import { cn } from "@/lib/utils";
import { renderingFor, type JapaneseVariant } from "./rendering";

type Value = readonly JaSegment[] | TextBlock | UiLabel;

export interface JapaneseTextProps {
  value: Value;
  /** companion: next to Indonesian/English text; content: Japanese is the learning content (docs/06 §4). */
  variant?: JapaneseVariant;
  /** Per-question override against answer leaks (docs/06 §9). */
  display?: JaDisplay;
  /** Overrides the user's preference, e.g. in the ScriptToggle preview. */
  scriptPref?: ScriptPref;
  vars?: LabelVars;
  className?: string;
}

function segmentsOf(value: Value, vars?: LabelVars): readonly JaSegment[] {
  if (Array.isArray(value)) return value as readonly JaSegment[];
  if ("jaMarkup" in value) return labelSegments(value, vars);
  return (value as TextBlock).ja ?? [];
}

function Furigana({ segments }: { segments: readonly JaSegment[] }) {
  return (
    <>
      {segments.map((segment, index) => {
        if (segment.t === " ") return null;
        if (!segment.r) return <Fragment key={index}>{segment.t}</Fragment>;
        return (
          <ruby key={index}>
            {segment.t}
            <rp>(</rp>
            <rt>{segment.r}</rt>
            <rp>)</rp>
          </ruby>
        );
      })}
    </>
  );
}

/**
 * Renders Japanese text according to the user's script preference (docs/06 §7): lang="ja" for the right glyphs,
 * ruby for furigana, romaji in lang="ja-Latn". Never italic.
 */
export function JapaneseText({
  value,
  variant = "content",
  display = "user_pref",
  scriptPref,
  vars,
  className,
}: JapaneseTextProps) {
  const prefs = usePrefs();
  const segments = segmentsOf(value, vars);
  if (segments.length === 0) return null;

  const { main, romaji } = renderingFor(scriptPref ?? prefs.scriptPref, variant, display);
  if (main === "none" && romaji === "none") return null;

  let japanese: ReactNode = null;
  if (main === "plain") japanese = plainText(segments);
  if (main === "kana") japanese = kanaText(segments);
  if (main === "furigana") japanese = <Furigana segments={segments} />;

  const romajiText = romaji === "none" ? null : toRomaji(segments);
  const hasRuby = main === "furigana" && segments.some((segment) => segment.r);

  return (
    <span
      className={cn(
        "font-ja",
        hasRuby && "leading-ruby",
        romaji === "line" && "inline-flex flex-col",
        className,
      )}
    >
      {japanese !== null && <span lang="ja">{japanese}</span>}
      {romajiText !== null && japanese !== null && romaji === "inline" && <span aria-hidden="true"> · </span>}
      {romajiText !== null && (
        <span lang="ja-Latn" className={cn(romaji === "line" && "text-body-sm text-ink-muted")}>
          {romajiText}
        </span>
      )}
    </span>
  );
}
