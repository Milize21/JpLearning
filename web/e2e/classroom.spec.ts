import { expect, test } from "@playwright/test";
import {
  devUsers,
  expectAccessible,
  expectNoHorizontalScroll,
  loginAndWait,
  skipOnboarding,
} from "./helpers";

// Seed of docs/03 §13: "Kelas Demo Dasar" with murid1.dev, chapter 1 of the kana book open, chapters 2–3 locked.
const lockedChapter = "b2000000-0000-4000-8000-000000000002";

test.beforeEach(async ({ page }) => {
  await skipOnboarding(page);
});

test("student sees chapter 1 open and chapter 2 locked, also when opening it directly", async ({ page }) => {
  await loginAndWait(page, devUsers.murid, /\/murid$/);
  await expect(page.getByRole("link", { name: /Kelas Demo Dasar/ })).toBeVisible();

  await page.goto("/murid/materi");
  const open = page.getByRole("link", { name: /Bab 1|Chapter 1/ });
  await expect(open).toBeVisible();
  await expect(page.getByText(/Terkunci|Locked/).first()).toBeVisible();
  await expectAccessible(page);
  await expectNoHorizontalScroll(page);

  await open.click();
  await expect(page).toHaveURL(/\/murid\/bab\//);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expectAccessible(page);

  await page.goto(`/murid/bab/${lockedChapter}`);
  await expect(page.getByText(/Bab ini belum dibuka|This chapter isn't open yet/)).toBeVisible();
});

test("student class page and schedule pass axe on every layout", async ({ page }) => {
  await loginAndWait(page, devUsers.murid, /\/murid$/);
  await page.getByRole("link", { name: /Kelas Demo Dasar/ }).click();
  await expect(page).toHaveURL(/\/murid\/kelas\//);
  await expect(page.getByRole("heading", { level: 1, name: "Kelas Demo Dasar" })).toBeVisible();
  await expectAccessible(page);
  await expectNoHorizontalScroll(page);

  await page.goto("/murid/jadwal");
  await expect(page.getByRole("button", { name: /Aktifkan pengingat|Turn on reminders/ })).toBeVisible();
  await expectAccessible(page);
  await expectNoHorizontalScroll(page);
});

test("teacher sees the class tabs; schedules and cancels a session", async ({ page, viewport }) => {
  await loginAndWait(page, devUsers.sensei, /\/sensei$/);
  // The login callback may still replace the URL right after landing on the home page; retry the first click.
  await expect(async () => {
    await page
      .getByRole("link", { name: /Kelas Demo Dasar/ })
      .first()
      .click();
    await expect(page).toHaveURL(/\/sensei\/kelas\/[^/]+\/bab$/, { timeout: 2000 });
  }).toPass();
  await expect(page.getByRole("switch").first()).toBeChecked();
  await expectAccessible(page);

  await page.getByRole("link", { name: /^(Anggota|Members)$/ }).click();
  await expect(page.getByText("@murid1.dev", { exact: false })).toBeVisible();
  await expectAccessible(page);

  await page
    .getByRole("link", { name: /^(Jadwal|Schedule)$/ })
    .last()
    .click();
  await expect(page).toHaveURL(/\/jadwal$/);
  // Creating sessions notifies the class; do it once, on the desktop run.
  test.skip((viewport?.width ?? 0) < 1000, "session writes run on desktop only");

  const topic = `E2E ${Date.now().toString(36)}`;
  await page.getByRole("button", { name: /Tambah jadwal|Add session/ }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel(/^(Topik|Topic)$/).fill(topic);
  await dialog.getByLabel(/Link pertemuan|Meeting link/).fill("https://zoom.us/j/123456789");
  await dialog.getByRole("button", { name: /^(Simpan|Save)$/ }).click();
  await expect(dialog).toBeHidden();
  const card = page.locator("section", { hasText: topic }).last();
  await expect(card).toBeVisible();

  await card.getByRole("button", { name: /Batalkan jadwal|Cancel session/ }).click();
  await page
    .getByRole("alertdialog")
    .getByRole("button", { name: /Ya, batalkan|Yes, cancel/ })
    .click();
  // Cancelled sessions stay visible, struck through, so students see the change (BR-JADWAL-04).
  await expect(card.getByText(/^(Dibatalkan|Cancelled)$/)).toBeVisible();
  await expect(card.getByRole("button", { name: /Batalkan jadwal|Cancel session/ })).toBeHidden();
});

test("admin content and reference pages pass axe", async ({ page, viewport }) => {
  test.skip((viewport?.width ?? 0) < 1000, "admin flows run on desktop only");
  await loginAndWait(page, devUsers.admin, /\/admin$/);
  await page
    .getByRole("link", { name: /^(Konten|Content)/ })
    .first()
    .click();
  await expect(page).toHaveURL(/\/admin\/konten$/);
  await expect(page.getByText(/Dasar|Basics/).first()).toBeVisible();
  await expectAccessible(page);

  await page
    .getByRole("link", { name: /Data referensi|Reference data/ })
    .first()
    .click();
  await expect(page).toHaveURL(/\/admin\/referensi$/);
  await expectAccessible(page);
});
