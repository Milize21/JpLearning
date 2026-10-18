import { isPlaceholder, parseFurigana } from "./furigana";
import type { JaSegment, UiLocale } from "./types";

/** English plural forms; Indonesian and Japanese have none (docs/06 §6.3). */
export interface PluralForms {
  readonly one: string;
  readonly other: string;
}

/** A UI label in all three languages (docs/06 §6). Create it only with {@link tb}. */
export interface UiLabel {
  readonly id: string;
  readonly en: string | PluralForms;
  readonly ja: readonly JaSegment[];
  readonly jaMarkup: string;
}

export type LabelVars = Readonly<Record<string, string | number>>;

/**
 * tb(id, en, jaMarkup): the three languages are mandatory by signature. The Japanese markup is parsed once when the
 * label module loads; invalid markup throws, and the label tests load every label module.
 */
export function tb(id: string, en: string | PluralForms, jaMarkup: string): UiLabel {
  const parsed = parseFurigana(jaMarkup);
  if (parsed.errors.length > 0) {
    const issues = parsed.errors
      .map((error) => `${error.code}${error.text ? ` (${error.text})` : ""}`)
      .join(", ");
    throw new Error(`Invalid Japanese markup in label "${id}": ${issues}`);
  }
  return Object.freeze({ id, en, ja: Object.freeze(parsed.segments), jaMarkup });
}

const PLURAL_RULES = new Intl.PluralRules("en");

/** The label in the UI language with {{placeholders}} filled in. */
export function formatLabel(label: UiLabel, locale: UiLocale, vars?: LabelVars): string {
  let template: string;
  if (locale === "id") {
    template = label.id;
  } else if (typeof label.en === "string") {
    template = label.en;
  } else {
    const count = Number(vars?.n ?? 0);
    template = PLURAL_RULES.select(count) === "one" ? label.en.one : label.en.other;
  }
  return interpolate(template, vars);
}

export function interpolate(template: string, vars?: LabelVars): string {
  return template.replace(/\{\{(\w+)\}\}/gu, (match, name: string) => {
    const value = vars?.[name];
    return value === undefined ? match : String(value);
  });
}

/** Japanese segments with {{placeholders}} replaced by their values. */
export function labelSegments(label: UiLabel, vars?: LabelVars): readonly JaSegment[] {
  if (!vars) return label.ja;
  return label.ja.map((segment) =>
    isPlaceholder(segment.t) ? { t: interpolate(segment.t, vars) } : segment,
  );
}
