import { toRomaji as wanakanaToRomaji } from "wanakana";
import type { JaSegment } from "./types";

/**
 * Wāpuro romaji per docs/06 §5 (D-13): long vowels doubled (renshuu), ん before a vowel or y written n',
 * particles from their `ro` override, words split only where the markup has a space. Never stored, always derived.
 */
export function toRomaji(segments: readonly JaSegment[]): string {
  const parts: string[] = [];
  let run = "";

  const flushRun = () => {
    if (run.length > 0) {
      parts.push(convertRun(run));
      run = "";
    }
  };

  for (const segment of segments) {
    if (segment.ro !== undefined) {
      flushRun();
      parts.push(` ${segment.ro} `);
    } else if (segment.t === " ") {
      flushRun();
      parts.push(" ");
    } else {
      run += segment.r ?? segment.t;
    }
  }
  flushRun();

  return parts
    .join("")
    .replace(/\s+/gu, " ")
    .replace(/ ([.,!?])/gu, "$1")
    .trim()
    .toLowerCase();
}

const PUNCTUATION: Readonly<Record<string, string>> = {
  "。": ".",
  "、": ",",
  "！": "!",
  "？": "?",
  "「": '"',
  "」": '"',
  "　": " ",
};

// Segments of one word are converted together, so ん at the end of one segment still sees the vowel of the next.
function convertRun(kana: string): string {
  // Punctuation first, so WanaKana does not turn 「」 into typographic quotes.
  const ascii = Array.from(kana, (char) => PUNCTUATION[char] ?? char).join("");
  const withApostrophe = ascii.replace(/([んン])(?=[あいうえおやゆよアイウエオヤユヨ])/gu, "$1'");
  return wanakanaToRomaji(withApostrophe, { upcaseKatakana: false })
    .replace(/n''/gu, "n'")
    .replace(/([aiueo])[-ー]/gu, "$1$1");
}
