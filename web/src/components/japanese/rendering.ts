import type { JaDisplay, ScriptPref } from "@/i18n/types";

export type JapaneseVariant = "companion" | "content";

export interface Rendering {
  main: "none" | "plain" | "furigana" | "kana";
  romaji: "none" | "inline" | "line";
}

/** What to show for a preference, variant and jaDisplay (docs/06 §4 and §9). */
export function renderingFor(pref: ScriptPref, variant: JapaneseVariant, display: JaDisplay): Rendering {
  if (display === "plain") return { main: "plain", romaji: "none" };

  let rendering: Rendering;
  if (variant === "companion") {
    rendering = {
      off: { main: "none", romaji: "none" },
      kanji_furigana: { main: "furigana", romaji: "none" },
      kana: { main: "kana", romaji: "none" },
      romaji: { main: "none", romaji: "inline" },
      kanji_furigana_romaji: { main: "furigana", romaji: "inline" },
    }[pref] as Rendering;
  } else {
    rendering = {
      off: { main: "plain", romaji: "none" },
      kanji_furigana: { main: "furigana", romaji: "none" },
      kana: { main: "kana", romaji: "none" },
      romaji: { main: "plain", romaji: "line" },
      kanji_furigana_romaji: { main: "furigana", romaji: "line" },
    }[pref] as Rendering;
  }

  if (display === "no_romaji" && rendering.romaji !== "none") {
    return { main: rendering.main === "none" ? "plain" : rendering.main, romaji: "none" };
  }
  return rendering;
}
