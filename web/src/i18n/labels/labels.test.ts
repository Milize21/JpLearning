import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { parseFurigana } from "../furigana";
import type { UiLabel } from "../tb";
import * as assessment from "./assessment";
import * as classroom from "./classroom";
import { common } from "./common";
import * as content from "./content";
import { errors } from "./errors";
import * as identity from "./identity";
import * as navModule from "./nav";
import * as practice from "./practice";

const labelModules: Record<string, unknown> = {
  common,
  errors,
  ...navModule,
  ...identity,
  ...content,
  ...classroom,
  ...practice,
  ...assessment,
};

function collectLabels(): Array<[string, UiLabel]> {
  const found: Array<[string, UiLabel]> = [];
  for (const [group, value] of Object.entries(labelModules)) {
    if (typeof value !== "object" || value === null) continue;
    for (const [key, label] of Object.entries(value as Record<string, unknown>)) {
      if (typeof label === "object" && label !== null && "jaMarkup" in label) {
        found.push([`${group}.${key}`, label as UiLabel]);
      }
    }
  }
  return found;
}

describe("UI labels (docs/06 §6)", () => {
  const labels = collectLabels();

  it("exist", () => {
    expect(labels.length).toBeGreaterThan(100);
  });

  it.each(labels)("%s has all three languages and clean Japanese markup", (_name, label) => {
    expect(label.id.trim()).not.toBe("");
    expect(typeof label.en === "string" ? label.en : label.en.other).not.toBe("");
    expect(label.ja.length).toBeGreaterThan(0);
    const parsed = parseFurigana(label.jaMarkup);
    expect(parsed.errors).toEqual([]);
    // Rule 4: は/へ/を as particles need a romaji override.
    expect(parsed.warnings).toEqual([]);
  });

  it("has a label for every API error code of docs/05 §2", () => {
    const catalog = [
      "validation_failed",
      "unauthorized",
      "forbidden",
      "account_inactive",
      "chapter_locked",
      "not_found",
      "method_not_allowed",
      "conflict",
      "concurrency_conflict",
      "has_dependents",
      "invalid_state",
      "already_submitted",
      "attempts_exhausted",
      "attempt_expired",
      "quiz_not_open",
      "session_ended",
      "empty_pool",
      "file_not_ready",
      "payload_too_large",
      "file_too_large",
      "unsupported_media_type",
      "rate_limited",
      "keycloak_unavailable",
      "internal_error",
    ];
    expect(
      Object.keys(errors)
        .filter((code) => code !== "network")
        .sort(),
    ).toEqual([...catalog].sort());
  });

  it("marks every AI-written Japanese line for review by a sensei (AGENTS.md §9)", () => {
    const dir = join(import.meta.dirname);
    for (const file of readdirSync(dir).filter(
      (name) => name.endsWith(".ts") && !name.endsWith(".test.ts"),
    )) {
      const unmarked = readFileSync(join(dir, file), "utf8")
        .split("\n")
        .filter((line) => /tb\(|^\s*"[^"]*[぀-ヿ一-鿿]/u.test(line))
        .filter((line) => /[぀-ヿ一-鿿]/u.test(line) && !line.includes("ja-review"))
        .filter((line) => !line.includes("あ button") && !line.includes("tombol あ"));
      expect(unmarked, file).toEqual([]);
    }
  });
});
