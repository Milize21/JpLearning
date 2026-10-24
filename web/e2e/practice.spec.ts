import { expect, test, type Page } from "@playwright/test";
import {
  devUsers,
  expectAccessible,
  expectNoHorizontalScroll,
  loginAndWait,
  skipOnboarding,
} from "./helpers";

// Seed of docs/03 §13: murid1.dev is in "Kelas Demo Dasar" with chapter 1 (row a) open and published kana questions.

test.beforeEach(async ({ page }) => {
  await skipOnboarding(page);
});

/** Answers whatever question is shown: the first choice, or "a" for a typed answer. */
async function answerCurrent(page: Page) {
  const typed = page.getByLabel(/^(Jawabanmu|Your answer)$/);
  if (await typed.isVisible()) {
    await typed.fill("a");
    await page.getByRole("button", { name: /^(Kirim|Submit)$/ }).click();
  } else {
    await page
      .getByRole("list", { name: /Pilihan jawaban|Answer choices/ })
      .getByRole("button")
      .first()
      .click();
  }
}

test("student drills hiragana with instant feedback, ends with a summary and sees weak spots", async ({
  page,
}) => {
  await loginAndWait(page, devUsers.murid, /\/murid$/);
  await page.goto("/murid/latihan");
  await expect(page.getByRole("button", { name: /^(Mulai|Start)$/ })).toBeVisible();
  await expectAccessible(page);
  await page.getByRole("button", { name: /^(Mulai|Start)$/ }).click();
  await expect(page).toHaveURL(/\/murid\/latihan\/[0-9a-f-]{36}$/);

  // Focus mode: no main navigation, question heading and feedback after answering.
  await expect(page.getByRole("navigation", { name: /Navigasi utama|Main navigation/ })).toHaveCount(0);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expectNoHorizontalScroll(page);
  await page.getByLabel(/Lanjut otomatis|Continue automatically/).uncheck();
  await answerCurrent(page);
  await expect(page.getByText(/^(Benar!|Belum tepat|Correct!|Not quite)$/).first()).toBeVisible();
  await expect(page.getByText(/Jawaban 1|Answers 1/)).toBeVisible();
  await expectAccessible(page);

  // Esc asks before ending; the summary shows the accuracy and a hanko stamp.
  await page.keyboard.press("Escape");
  await page
    .getByRole("alertdialog")
    .getByRole("button", { name: /Ya, akhiri|Yes, end/ })
    .click();
  await expect(page.getByRole("heading", { name: /Ringkasan latihan|Practice summary/ })).toBeVisible();
  await expect(
    page.getByRole("img", {
      name: /Bagus sekali|Kamu sudah berusaha|Ayo terus semangat|Very good|You tried hard|Keep it up/,
    }),
  ).toBeVisible();
  await expectAccessible(page);

  await page.goto("/murid/kelemahan");
  await expect(page.getByRole("heading", { level: 1 })).toContainText(/Kelemahanku|My weak spots/);
  await expectAccessible(page);
  await expectNoHorizontalScroll(page);
});

test("teacher writes a question, tries it as a student and archives it; class progress passes axe", async ({
  page,
  viewport,
}) => {
  test.skip((viewport?.width ?? 0) < 1000, "authoring runs on desktop only");
  await loginAndWait(page, devUsers.sensei, /\/sensei$/);
  await page.goto("/sensei/bank-soal");
  await expect(page.getByRole("heading", { level: 1 })).toContainText(/Bank soal|Question bank/);
  await expectAccessible(page);

  const prompt = `E2E soal ${Date.now().toString(36)}`;
  await page.getByRole("button", { name: /^(Buat soal|New question)$/ }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel(/^Level$/).selectOption({ label: "Dasar" });
  await dialog.getByLabel(/^(Bab|Chapter)$/).selectOption({ index: 1 });
  await dialog.locator("#q-prompt-id").fill(prompt);
  await dialog.locator("#q-stimulus-ja").fill("あ");
  await dialog.locator("#q-choice-0-id").fill("a");
  await dialog.locator("#q-choice-1-id").fill("o");
  await dialog.getByRole("button", { name: /^(Simpan|Save)$/ }).click();
  await expect(dialog).toBeHidden();

  await page.getByRole("searchbox").fill(prompt);
  await page.getByRole("button", { name: /^(Cari|Search)$/ }).click();
  const row = page.getByRole("listitem").filter({ hasText: prompt });
  await expect(row).toBeVisible();

  await row.getByRole("button", { name: /Coba jawab|Try it/ }).click();
  const preview = page.getByRole("dialog");
  await preview
    .getByRole("list", { name: /Pilihan jawaban|Answer choices/ })
    .getByRole("button", { name: /\ba\b/ })
    .click();
  await expect(preview.getByText(/^(Benar!|Correct!)$/).first()).toBeVisible();
  await expectAccessible(page);
  await page.keyboard.press("Escape");

  await row.getByRole("button", { name: /Aksi|Actions/ }).click();
  await page.getByRole("menuitem", { name: /Arsipkan|Archive/ }).click();
  await expect(row.getByText(/Diarsipkan|Archived/)).toBeVisible();

  await page.goto("/sensei/kelas");
  await page
    .getByRole("link", { name: /Kelas Demo Dasar/ })
    .first()
    .click();
  await page.getByRole("link", { name: /^(Progres|Progress)$/ }).click();
  await expect(page).toHaveURL(/\/progres$/);
  await expect(page.getByText(/Murid Satu|murid1|Murid/).first()).toBeVisible();
  await expectAccessible(page);
});
