import { describe, expect, it } from "vitest";
import { kanaText, parseFurigana, plainText, toMarkup } from "./furigana";

describe("parseFurigana (docs/06 §3)", () => {
  it("parses kanji with reading", () => {
    expect(parseFurigana("{漢字|かんじ}").segments).toEqual([{ t: "漢字", r: "かんじ" }]);
  });

  it("parses a particle with a romaji override and no reading", () => {
    expect(parseFurigana("{は||wa}").segments).toEqual([{ t: "は", ro: "wa" }]);
  });

  it("parses reading plus romaji override", () => {
    expect(parseFurigana("{日本|にほん|nihon}").segments).toEqual([{ t: "日本", r: "にほん", ro: "nihon" }]);
  });

  it("keeps plain text as one segment and turns spaces into word boundaries", () => {
    expect(parseFurigana("{勉強|べんきょう} します").segments).toEqual([
      { t: "勉強", r: "べんきょう" },
      { t: " " },
      { t: "します" },
    ]);
  });

  it("parses the full docs example", () => {
    const { segments, errors, warnings } = parseFurigana(
      "{私|わたし}{は||wa}{日本語|にほんご}{を||o}{勉強|べんきょう} します。",
    );
    expect(errors).toEqual([]);
    expect(warnings).toEqual([]);
    expect(plainText(segments)).toBe("私は日本語を勉強します。");
    expect(kanaText(segments)).toBe("わたしはにほんごをべんきょうします。");
  });

  it("keeps {{placeholders}} as their own segment", () => {
    expect(parseFurigana("{{n}}{問|もん}").segments).toEqual([{ t: "{{n}}" }, { t: "問", r: "もん" }]);
  });

  it("supports escaped braces, bars and backslashes", () => {
    expect(parseFurigana("a\\{b\\|c\\}d\\\\").segments).toEqual([{ t: "a{b|c}d\\" }]);
  });

  it.each([
    ["{漢字|かんじ", "unclosed_brace"],
    ["漢字}", "unexpected_closing_brace"],
    ["{|かんじ}", "empty_base"],
    ["{日本|に|ni|x}", "too_many_separators"],
    ["{漢字|kanji}", "invalid_reading"],
    ["{漢字}", "kanji_without_reading"],
    ["漢字です", "kanji_without_reading"],
  ])("reports %s as %s", (markup, code) => {
    expect(parseFurigana(markup).errors.map((error) => error.code)).toContain(code);
  });

  it("accepts katakana and the long vowel mark as reading", () => {
    expect(parseFurigana("{珈琲|コーヒー}").errors).toEqual([]);
  });

  it("warns about a standalone particle without romaji override", () => {
    const { errors, warnings } = parseFurigana("{私|わたし} は {学生|がくせい}");
    expect(errors).toEqual([]);
    expect(warnings.map((warning) => warning.text)).toEqual(["は"]);
  });
});

describe("toMarkup", () => {
  it.each([
    "{私|わたし}{は||wa}{日本語|にほんご}{を||o}{勉強|べんきょう} します。",
    "{日本|にほん|nihon}",
    "a\\{b\\|c\\}d\\\\",
  ])("round-trips %s", (markup) => {
    expect(toMarkup(parseFurigana(markup).segments)).toBe(markup);
  });
});
