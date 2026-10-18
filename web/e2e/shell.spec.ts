import { expect, test } from "@playwright/test";
import {
  devUsers,
  expectAccessible,
  expectNoHorizontalScroll,
  loginAndWait,
  skipOnboarding,
} from "./helpers";

test.beforeEach(async ({ page }) => {
  await skipOnboarding(page);
});

test("welcome page has one red login action and passes axe", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("button", { name: /^(Masuk|Log in)$/ })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("lang", /^(id|en)$/);
  await expectAccessible(page);
  await expectNoHorizontalScroll(page);
});

for (const [role, user, home] of [
  ["admin", devUsers.admin, /\/admin$/],
  ["sensei", devUsers.sensei, /\/sensei$/],
  ["murid", devUsers.murid, /\/murid$/],
] as const) {
  test(`${role} logs in and lands on the ${role} home`, async ({ page }) => {
    await loginAndWait(page, user, home);
    await expect(page.getByRole("heading", { level: 1 })).toContainText(/Halo|Hi/);
    await expectAccessible(page);
    await expectNoHorizontalScroll(page);
  });
}

test("a student opening an admin page gets the friendly 403 page", async ({ page }) => {
  await loginAndWait(page, devUsers.murid, /\/murid$/);
  await page.goto("/admin/pengguna");
  await expect(page.getByRole("heading", { level: 1 })).toContainText(
    /bukan untuk peranmu|isn't for your role/,
  );
  await page.getByRole("link", { name: /Kembali ke beranda|Back to home/ }).click();
  await expect(page).toHaveURL(/\/murid$/);
});

test("language and Japanese display change at once and survive a reload", async ({ page }) => {
  await loginAndWait(page, devUsers.sensei, /\/sensei$/);

  await page.getByRole("button", { name: /^(Bahasa|Language):/ }).click();
  await page.getByRole("menuitemradio", { name: "English" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Hi,");

  await page.getByRole("button", { name: /^Japanese display:/ }).click();
  await page.getByRole("menuitemradio", { name: /^Romaji/ }).click();
  await expect(page.locator('[lang="ja-Latn"]:visible').first()).toBeVisible();

  await page.reload();
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Hi,");
  await expect(page.locator('[lang="ja-Latn"]:visible').first()).toBeVisible();

  // Back to the defaults for the other tests.
  await page.getByRole("button", { name: /^Japanese display:/ }).click();
  // Items also contain a live preview, so match the start of the name only.
  await page.getByRole("menuitemradio", { name: /^Kanji \+ furigana(?! \+)/ }).click();
  await page.getByRole("button", { name: /^Language:/ }).click();
  await page.getByRole("menuitemradio", { name: "Bahasa Indonesia" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Halo,");
});

test("logout returns to the welcome page", async ({ page }) => {
  await loginAndWait(page, devUsers.admin, /\/admin$/);
  await page.goto("/keluar");
  await expect(page.getByRole("button", { name: /^(Masuk|Log in)$/ })).toBeVisible();
});
