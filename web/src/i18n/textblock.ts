import { parseFurigana, toMarkup } from "./furigana";
import type { JaSegment, UiLocale } from "./types";

/** A TextBlock as the API sends it: missing languages are null or absent (docs/03 §3.1). */
export interface ApiTextBlock {
  readonly id?: string | null;
  readonly en?: string | null;
  readonly ja?: readonly ApiJaSegment[] | null;
}

export interface ApiJaSegment {
  readonly t: string;
  readonly r?: string | null;
  readonly ro?: string | null;
  readonly v?: string | null;
}

export const toSegments = (segments: readonly ApiJaSegment[] | null | undefined): JaSegment[] =>
  (segments ?? []).map((segment) => ({
    t: segment.t,
    ...(segment.r ? { r: segment.r } : {}),
    ...(segment.ro ? { ro: segment.ro } : {}),
    ...(segment.v ? { v: segment.v } : {}),
  }));

/** Text of a content TextBlock in the UI language, falling back to the other language. */
export function textIn(block: ApiTextBlock | null | undefined, locale: UiLocale): string {
  if (!block) return "";
  const preferred = locale === "en" ? block.en : block.id;
  const fallback = locale === "en" ? block.id : block.en;
  return preferred?.trim() || fallback?.trim() || "";
}

/** The three form fields of a TextBlock: Indonesian, English, and Japanese as furigana markup (docs/06 §3). */
export interface TextBlockFields {
  id: string;
  en: string;
  ja: string;
}

export const textBlockToFields = (block: ApiTextBlock | null | undefined): TextBlockFields => ({
  id: block?.id ?? "",
  en: block?.en ?? "",
  ja: block?.ja ? toMarkup(toSegments(block.ja)) : "",
});

/** Builds the request body; empty languages are left out. Markup must be valid (checked by the form first). */
export function fieldsToTextBlock(fields: TextBlockFields): { id: string | null; en: string | null; ja: JaSegment[] | null } {
  const ja = fields.ja.trim() === "" ? null : parseFurigana(fields.ja.trim()).segments;
  return {
    id: fields.id.trim() === "" ? null : fields.id.trim(),
    en: fields.en.trim() === "" ? null : fields.en.trim(),
    ja,
  };
}
