import type { components } from "@/lib/api/schema/identity";

/** One segment of Japanese text (docs/03 §3.1). A segment whose text is a single space marks a romaji word boundary. */
export interface JaSegment {
  readonly t: string;
  readonly r?: string;
  readonly ro?: string;
  readonly v?: string;
}

/** Learning-content text with a Japanese companion (docs/03 §3.1). */
export interface TextBlock {
  readonly id?: string;
  readonly en?: string;
  readonly ja?: readonly JaSegment[];
}

export type UiLocale = components["schemas"]["UiLocale"];
export type ScriptPref = components["schemas"]["ScriptPref"];

/** Per-question override so readings or romaji never give the answer away (docs/06 §9). */
export type JaDisplay = "user_pref" | "plain" | "no_romaji";

export const scriptPrefs: readonly ScriptPref[] = [
  "off",
  "kanji_furigana",
  "kana",
  "romaji",
  "kanji_furigana_romaji",
];
export const uiLocales: readonly UiLocale[] = ["id", "en"];
