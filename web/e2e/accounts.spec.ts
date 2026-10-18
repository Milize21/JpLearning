import { expect, test, type Page } from "@playwright/test";
import { devUsers, expectAccessible, login, loginAndWait, skipOnboarding, uniqueName } from "./helpers";

// Account management changes shared data; run these once, on the desktop layout.
test.skip(({ viewport }) => (viewport?.width ?? 0) < 1000, "admin flows run on desktop only");

async function openUsers(page: Page) {
  await skipOnboarding(page);
  await loginAndWait(page, devUsers.admin, /\/admin$/);
  await page
    .getByRole("link", { name: /Pengguna|Users/ })
    .first()
    .click();
  await expect(page).toHaveURL(/\/admin\/pengguna$/);
}

async function createStudent(page: Page, username: string): Promise<string> {
  await page.getByRole("button", { name: /Tambah akun|Add account/ }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel(/^Username$/).fill(username);
  await dialog.getByLabel(/Nama tampilan|Display name/).fill(`E2E ${username}`);
  await dialog.getByRole("button", { name: /Buat akun|Create account/ }).click();
  const password = (await dialog.getByTestId("temporary-password").textContent())?.trim() ?? "";
  expect(password).toHaveLength(12);
  await dialog
    .getByRole("button", { name: /^(Tutup|Close)$/ })
    .last()
    .click();
  return password;
}

test("admin user list passes axe and shows the dev accounts", async ({ page }) => {
  await openUsers(page);
  await expect(page.getByRole("cell", { name: "admin.dev", exact: true })).toBeVisible();
  await expectAccessible(page);
});

test("new student must change the temporary password, sees onboarding, and preferences persist", async ({
  page,
}) => {
  await openUsers(page);
  const username = uniqueName("e2e.murid");
  const temporaryPassword = await createStudent(page, username);
  await page.goto("/keluar");
  await expect(page.getByRole("button", { name: /^(Masuk|Log in)$/ })).toBeVisible();

  await login(page, username, temporaryPassword);
  // Keycloak forces a new password (UPDATE_PASSWORD).
  await page.locator("#password-new").fill("E2e-new-pass-1");
  await page.locator("#password-confirm").fill("E2e-new-pass-1");
  await page.locator("[type=submit]").click();
  await expect(page).toHaveURL(/\/murid$/);

  // Onboarding: English, then romaji.
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: "English" }).click();
  await dialog.getByRole("button", { name: /^(Next|Lanjut)$/ }).click();
  await dialog.getByRole("button", { name: /^Romaji/ }).click();
  await dialog.getByRole("button", { name: /^(Start|Mulai)$/ }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Hi,");

  await page.reload();
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Hi,");
  await expect(page.locator('[lang="ja-Latn"]:visible').first()).toBeVisible();
});

test("deactivated account cannot log in; reset password issues a new one", async ({ page }) => {
  await openUsers(page);
  const username = uniqueName("e2e.nonaktif");
  await createStudent(page, username);

  await page.getByRole("searchbox").fill(username);
  const actions = page
    .getByRole("button", {
      name: new RegExp(`(Aksi untuk|Actions for) E2E ${username.replace(/\./g, "\\.")}`),
    })
    .first();
  await actions.click();
  await page.getByRole("menuitem", { name: /Reset password/ }).click();
  await page
    .getByRole("alertdialog")
    .getByRole("button", { name: /Ya, reset password|Yes, reset password/ })
    .click();
  await expect(page.getByTestId("temporary-password")).toHaveText(/^[A-Za-z0-9]{12}$/);
  await page
    .getByRole("dialog")
    .getByRole("button", { name: /^(Tutup|Close)$/ })
    .last()
    .click();

  await actions.click();
  await page.getByRole("menuitem", { name: /Nonaktifkan|Deactivate/ }).click();
  await page
    .getByRole("alertdialog")
    .getByRole("button", { name: /Ya, nonaktifkan akun|Yes, deactivate/ })
    .click();
  await expect(
    page
      .getByRole("row", { name: new RegExp(username.replace(/\./g, "\\.")) })
      .getByText(/Nonaktif|Inactive/),
  ).toBeVisible();

  await page.goto("/keluar");
  // Wait for the logout round trip to finish before starting a new login.
  await expect(page.getByRole("button", { name: /^(Masuk|Log in)$/ })).toBeVisible();
  await login(page, username, "whatever-1234");
  await expect(page.locator("#input-error, .kc-feedback-text, [id^=input-error]").first()).toBeVisible();
});

test("CSV import validates first, then creates accounts and offers the password file once", async ({
  page,
}) => {
  await openUsers(page);
  await page.getByRole("button", { name: /Impor CSV|Import CSV/ }).click();
  const dialog = page.getByRole("dialog");

  await dialog.getByLabel(/File CSV|CSV file/).setInputFiles({
    name: "salah.csv",
    mimeType: "text/csv",
    buffer: Buffer.from("username,display_name,role\nBAD NAME,,admin\n"),
  });
  await dialog.getByRole("button", { name: /Periksa file|Check file/ }).click();
  await expect(dialog.getByRole("table")).toContainText(/invalid|tidak valid|Peran harus|Role must/i);

  const a = uniqueName("e2e.imp");
  const b = uniqueName("e2e.imp");
  await dialog.getByLabel(/File CSV|CSV file/).setInputFiles({
    name: "benar.csv",
    mimeType: "text/csv",
    buffer: Buffer.from(`username;display_name;role\n${a};Impor A;murid\n${b};Impor B;sensei\n`),
  });
  await dialog.getByRole("button", { name: /Periksa file|Check file/ }).click();
  await dialog.getByRole("button", { name: /Buat 2 akun|Create 2 accounts/ }).click();
  await expect(dialog).toContainText(/2 akun dibuat|2 accounts created/);

  const download = page.waitForEvent("download");
  await dialog.getByRole("button", { name: /Unduh file password|Download password file/ }).click();
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/\.csv$/);
  await expectAccessible(page);
});
