import AxeBuilder from "@axe-core/playwright";
import { expect, type Page } from "@playwright/test";

/** Development users from infra/keycloak/realm-nomax.json (infra/README.md). */
export const devUsers = {
  admin: { username: "admin.dev", password: "Nomax-dev-admin" },
  sensei: { username: "sensei.dev", password: "Nomax-dev-sensei" },
  murid: { username: "murid1.dev", password: "Nomax-dev-murid1" },
} as const;

/** Marks onboarding as done for the dev users so it does not cover the pages under test. */
export async function skipOnboarding(page: Page) {
  await page.addInitScript(() => {
    for (const id of ["1", "2", "3", "4"]) {
      window.localStorage.setItem(`nomax.onboarded.a0000000-0000-4000-8000-00000000000${id}`, "1");
    }
  });
}

/** Login through the Keycloak form (selectors are Keycloak's own ids, independent of language). */
export async function login(page: Page, username: string, password: string) {
  await page.goto("/");
  await page.getByRole("button", { name: /^(Masuk|Log in)$/ }).click();
  await page.locator("#username").fill(username);
  await page.locator("#password").fill(password);
  await page.locator("#kc-login").click();
}

export async function loginAndWait(page: Page, user: { username: string; password: string }, home: RegExp) {
  await login(page, user.username, user.password);
  await expect(page).toHaveURL(home);
}

/** axe: no serious or critical WCAG 2.x A/AA violations (docs/07 §14, docs/09 §1). */
export async function expectAccessible(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"])
    .analyze();
  const serious = results.violations.filter(
    (violation) => violation.impact === "serious" || violation.impact === "critical",
  );
  expect(
    serious.map(
      (violation) => `${violation.id}: ${violation.nodes.map((node) => node.target.join(" ")).join(", ")}`,
    ),
  ).toEqual([]);
}

/** Phone layouts must not scroll sideways at 360 px (docs/07 §6). */
export async function expectNoHorizontalScroll(page: Page) {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
}

export function uniqueName(prefix: string) {
  return `${prefix}.${Date.now().toString(36)}${Math.floor(Math.random() * 1000).toString(36)}`;
}
