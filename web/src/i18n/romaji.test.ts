import { describe, expect, it } from "vitest";
import { parseFurigana } from "./furigana";
import { toRomaji } from "./romaji";

const romaji = (markup: string) => toRomaji(parseFurigana(markup).segments);

// Test cases of docs/06 §5; a change in WanaKana must not change these results.
describe("toRomaji (docs/06 §5, D-13)", () => {
  it.each([
    ["{練習|れんしゅう}", "renshuu"],
    [
      "{私|わたし}{は||wa}{日本語|にほんご}{を||o}{勉強|べんきょう}します。",
      "watashi wa nihongo o benkyoushimasu.",
    ],
    ["{勉強|べんきょう} します", "benkyou shimasu"],
    ["{食|た}べる", "taberu"],
    ["ラーメン", "raamen"],
    ["コンピューター", "konpyuutaa"],
    ["{本屋|ほんや}", "hon'ya"],
    ["{禁煙|きんえん}", "kin'en"],
    ["きって", "kitte"],
    ["{東京|とうきょう}", "toukyou"],
    ["{学校|がっこう} {へ||e} {行|い}きます", "gakkou e ikimasu"],
  ])("%s → %s", (markup, expected) => {
    expect(romaji(markup)).toBe(expected);
  });

  it("applies n' across segment borders inside one word", () => {
    expect(romaji("{本|ほん}{屋|や}")).toBe("hon'ya");
  });

  it("maps Japanese punctuation", () => {
    expect(romaji("「はい」、 いいえ！ ほんとう？")).toBe('"hai", iie! hontou?');
  });

  it("extends a vowel for the long mark in hiragana", () => {
    expect(romaji("げーむ")).toBe("geemu");
  });
});
