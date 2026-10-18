import { describe, expect, it } from "vitest";
import { cn } from "./utils";

describe("cn", () => {
  it("keeps a token text color next to a token font size", () => {
    expect(cn("text-on-action", "text-body-sm")).toBe("text-on-action text-body-sm");
    expect(cn("text-ink-inverse text-caption")).toBe("text-ink-inverse text-caption");
  });

  it("still lets a later utility of the same group win", () => {
    expect(cn("text-body", "text-body-sm")).toBe("text-body-sm");
    expect(cn("text-ink", "text-action")).toBe("text-action");
  });
});
