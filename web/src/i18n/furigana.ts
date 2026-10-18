import type { JaSegment } from "./types";

/**
 * Parser for the one-line furigana markup of docs/06 §3, shared in spirit with the backend validator:
 *   {漢字|かんじ}  {は||wa}  {日本|にほん|nihon}  plain text  " " (word boundary)  {{n}} (label placeholder)
 *   \{ \} \| \\ are literal characters.
 */

export type ParseIssueCode =
  | "unclosed_brace"
  | "unexpected_closing_brace"
  | "empty_base"
  | "too_many_separators"
  | "invalid_reading"
  | "kanji_without_reading"
  | "particle_without_romaji";

export interface ParseIssue {
  readonly code: ParseIssueCode;
  readonly index: number;
  readonly text?: string;
}

export interface ParseResult {
  readonly segments: JaSegment[];
  readonly errors: ParseIssue[];
  /** Rule 4: は/へ/を standing alone without a romaji override is probably a particle written wrong. */
  readonly warnings: ParseIssue[];
}

const KANJI = /[㐀-䶿一-鿿豈-﫿々〆ヶ]/u;
const READING = /^[ぁ-ゖゝゞァ-ヺー-ヾ]+$/u;
const PARTICLES = new Set(["は", "へ", "を"]);
const ESCAPABLE = "{}|\\";
const PLACEHOLDER = /^\{\{\w+\}\}$/u;

export const containsKanji = (text: string): boolean => KANJI.test(text);

export const isPlaceholder = (text: string): boolean => PLACEHOLDER.test(text);

export function parseFurigana(markup: string): ParseResult {
  const segments: JaSegment[] = [];
  const errors: ParseIssue[] = [];
  const warnings: ParseIssue[] = [];
  let plain = "";
  let plainStart = 0;

  const flushPlain = () => {
    if (plain.length > 0) {
      segments.push({ t: plain });
      checkPlain(plain, plainStart, errors, warnings);
      plain = "";
    }
  };

  let i = 0;
  while (i < markup.length) {
    const c = markup.charAt(i);
    const next = markup.charAt(i + 1);

    if (c === "\\" && next !== "" && ESCAPABLE.includes(next)) {
      if (plain.length === 0) plainStart = i;
      plain += next;
      i += 2;
      continue;
    }

    if (c === "{" && next === "{") {
      const end = markup.indexOf("}}", i + 2);
      if (end < 0) {
        errors.push({ code: "unclosed_brace", index: i });
        break;
      }
      flushPlain();
      segments.push({ t: markup.slice(i, end + 2) });
      i = end + 2;
      continue;
    }

    if (c === "{") {
      const parts: string[] = [];
      let current = "";
      let j = i + 1;
      let closed = false;
      while (j < markup.length) {
        const d = markup.charAt(j);
        const after = markup.charAt(j + 1);
        if (d === "\\" && after !== "" && ESCAPABLE.includes(after)) {
          current += after;
          j += 2;
          continue;
        }
        if (d === "}") {
          closed = true;
          break;
        }
        if (d === "{") break;
        if (d === "|") {
          parts.push(current);
          current = "";
        } else {
          current += d;
        }
        j++;
      }
      parts.push(current);

      if (!closed) {
        errors.push({ code: "unclosed_brace", index: i });
        break;
      }

      flushPlain();
      const [base = "", reading = "", romaji = ""] = parts;
      if (parts.length > 3) errors.push({ code: "too_many_separators", index: i, text: base });
      if (base.length === 0) errors.push({ code: "empty_base", index: i });
      if (reading.length > 0 && !READING.test(reading))
        errors.push({ code: "invalid_reading", index: i, text: reading });
      if (reading.length === 0 && containsKanji(base))
        errors.push({ code: "kanji_without_reading", index: i, text: base });

      segments.push({
        t: base,
        ...(reading.length > 0 ? { r: reading } : {}),
        ...(romaji.length > 0 ? { ro: romaji } : {}),
      });
      i = j + 1;
      continue;
    }

    if (c === "}") {
      errors.push({ code: "unexpected_closing_brace", index: i });
      i++;
      continue;
    }

    if (c === " ") {
      flushPlain();
      if (segments.at(-1)?.t !== " ") segments.push({ t: " " });
      i++;
      continue;
    }

    if (plain.length === 0) plainStart = i;
    plain += c;
    i++;
  }

  flushPlain();
  return { segments, errors, warnings };
}

function checkPlain(text: string, index: number, errors: ParseIssue[], warnings: ParseIssue[]) {
  // Rule 3: kanji always need a reading, otherwise the kana and furigana modes cannot work.
  if (containsKanji(text)) errors.push({ code: "kanji_without_reading", index, text });
  if (PARTICLES.has(text)) warnings.push({ code: "particle_without_romaji", index, text });
}

/** Display text without word-boundary spaces, as Japanese is written. */
export const plainText = (segments: readonly JaSegment[]): string =>
  segments
    .filter((segment) => segment.t !== " ")
    .map((segment) => segment.t)
    .join("");

/** Kana-only text: every kanji segment replaced by its reading. */
export const kanaText = (segments: readonly JaSegment[]): string =>
  segments
    .filter((segment) => segment.t !== " ")
    .map((segment) => segment.r ?? segment.t)
    .join("");

const escapeMarkup = (text: string): string => text.replace(/[{}|\\]/gu, (char) => `\\${char}`);

/** Inverse of {@link parseFurigana}: segments back to editable markup (round-trips for valid markup). */
export const toMarkup = (segments: readonly JaSegment[]): string =>
  segments
    .map((segment) => {
      if (segment.t === " ") return " ";
      if (segment.r && segment.ro) return `{${escapeMarkup(segment.t)}|${segment.r}|${segment.ro}}`;
      if (segment.r) return `{${escapeMarkup(segment.t)}|${segment.r}}`;
      if (segment.ro) return `{${escapeMarkup(segment.t)}||${segment.ro}}`;
      return escapeMarkup(segment.t);
    })
    .join("");
