import { expect, test, type Browser, type Page } from "@playwright/test";
import { devUsers, expectAccessible, loginAndWait, skipOnboarding } from "./helpers";

// Writes quizzes and assignments to "Kelas Demo Dasar"; runs once, on the desktop layout, and archives what it made.
test.skip(({ viewport }) => (viewport?.width ?? 0) < 1000, "assessment flows run on desktop only");

async function signedIn(
  browser: Browser,
  user: { username: string; password: string },
  home: RegExp,
): Promise<Page> {
  const context = await browser.newContext();
  const page = await context.newPage();
  await skipOnboarding(page);
  await loginAndWait(page, user, home);
  return page;
}

async function openClassTab(page: Page, tab: RegExp) {
  await page.goto("/sensei/kelas");
  await page
    .getByRole("link", { name: /Kelas Demo Dasar/ })
    .first()
    .click();
  await page.getByRole("link", { name: tab }).click();
}

test("teacher builds and publishes a quiz; student takes it and sees the result; teacher sees the attempt", async ({
  browser,
}) => {
  const title = `E2E Kuis ${Date.now().toString(36)}`;
  const sensei = await signedIn(browser, devUsers.sensei, /\/sensei$/);
  await openClassTab(sensei, /^(Kuis & ujian|Quizzes & exams)$/);
  await expectAccessible(sensei);

  await sensei.getByRole("button", { name: /^(Buat kuis|New quiz)$/ }).click();
  const form = sensei.getByRole("dialog");
  await form.locator("#quiz-title-id").fill(title);
  await form.getByLabel(/Kesempatan mengerjakan|Attempts allowed/).fill("2");
  await form.getByRole("button", { name: /^(Simpan|Save)$/ }).click();
  await expect(form).toBeHidden();

  const card = sensei.getByRole("listitem").filter({ hasText: title });
  await card.getByRole("button", { name: /^(Soal|Questions)$/ }).click();
  const picker = sensei.getByRole("dialog");
  await picker.getByRole("list").first().getByRole("button").first().click();
  await picker.getByRole("list").first().getByRole("button").nth(1).click();
  await expect(picker.getByText(/Terpilih \(2\)|Selected \(2\)/)).toBeVisible();
  await expectAccessible(sensei);
  await picker.getByRole("button", { name: /^(Simpan|Save)$/ }).click();
  await expect(picker).toBeHidden();
  await card.getByRole("button", { name: /^(Terbitkan|Publish)$/ }).click();
  await expect(card.getByText(/^(Terbit|Published)$/)).toBeVisible();

  // Student: start, answer both questions, submit.
  const murid = await signedIn(browser, devUsers.murid, /\/murid$/);
  await murid.goto("/murid/kuis");
  const quizCard = murid.getByRole("listitem").filter({ hasText: title });
  await quizCard.getByRole("button", { name: /^(Mulai|Start)$/ }).click();
  await murid
    .getByRole("alertdialog")
    .getByRole("button", { name: /^(Mulai|Start)$/ })
    .click();
  await expect(murid).toHaveURL(/\/murid\/kuis\/[0-9a-f-]{36}$/);
  await expect(murid.getByRole("navigation", { name: /Daftar soal|Question list/ })).toBeVisible();
  await expectAccessible(murid);

  for (let question = 0; question < 2; question++) {
    const typed = murid.getByLabel(/^(Jawabanmu|Your answer)$/);
    if (await typed.isVisible()) {
      await typed.fill("a");
      await murid.getByRole("button", { name: /^(Kirim|Submit)$/ }).click();
    } else {
      await murid
        .getByRole("list", { name: /Pilihan jawaban|Answer choices/ })
        .getByRole("button")
        .first()
        .click();
    }
    await expect(murid.getByText(/^(Tersimpan|Saved)$/)).toBeVisible();
    if (question === 0) await murid.getByRole("button", { name: /^(Berikutnya|Next)$/ }).click();
  }

  await murid.getByRole("button", { name: /Kirim jawaban|Submit answers/ }).click();
  await murid
    .getByRole("alertdialog")
    .getByRole("button", { name: /Ya, kirim|Yes, submit/ })
    .click();
  await expect(murid.getByRole("heading", { name: /^(Hasil|Result)/ })).toBeVisible();
  await expect(murid.getByText(/Skor \d+|Score \d+/)).toBeVisible();
  await expectAccessible(murid);

  await murid.goto("/murid/nilai");
  await expect(murid.getByText("Kelas Demo Dasar")).toBeVisible();
  await expectAccessible(murid);

  // Teacher: the attempt is listed; then archive the quiz again.
  await sensei.reload();
  await sensei
    .getByRole("listitem")
    .filter({ hasText: title })
    .getByRole("button", { name: /Pengerjaan|Attempts/ })
    .click();
  await expect(sensei.getByRole("dialog").getByText(/#1/)).toBeVisible();
  await sensei.keyboard.press("Escape");
  await sensei
    .getByRole("listitem")
    .filter({ hasText: title })
    .getByRole("button", { name: /^(Arsipkan|Archive)$/ })
    .click();
});

test("teacher publishes an assignment; student submits text; teacher grades it; grades table passes axe", async ({
  browser,
}) => {
  const title = `E2E Tugas ${Date.now().toString(36)}`;
  const sensei = await signedIn(browser, devUsers.sensei, /\/sensei$/);
  await openClassTab(sensei, /^(Tugas|Assignments)$/);
  await sensei.getByRole("button", { name: /^(Buat tugas|New assignment)$/ }).click();
  const form = sensei.getByRole("dialog");
  await form.locator("#assignment-title-id").fill(title);
  await form.getByLabel(/Instruksi|Instructions/).fill("Tulis satu kalimat perkenalan.");
  await form.getByRole("button", { name: /^(Simpan|Save)$/ }).click();
  await expect(form).toBeHidden();
  const card = sensei.getByRole("listitem").filter({ hasText: title });
  await card.getByRole("button", { name: /^(Terbitkan|Publish)$/ }).click();
  await expect(card.getByText(/^(Terbit|Published)$/)).toBeVisible();

  const murid = await signedIn(browser, devUsers.murid, /\/murid$/);
  await murid.goto("/murid/tugas");
  await murid.getByRole("link", { name: new RegExp(title) }).click();
  await murid.getByLabel(/Jawaban tertulis|Written answer/).fill("Watashi wa murid desu.");
  await expectAccessible(murid);
  await murid.getByRole("button", { name: /^(Kirim tugas|Submit assignment)$/ }).click();
  await expect(murid.getByText(/^(Terkirim|Submitted) /)).toBeVisible();

  await card.getByRole("button", { name: /^(Kiriman|Submissions)$/ }).click();
  const dialog = sensei.getByRole("dialog");
  await expect(dialog.getByText("Watashi wa murid desu.")).toBeVisible();
  await dialog
    .getByLabel(/Nilai \(0–100\)|Grade \(0–100\)/)
    .first()
    .fill("90");
  await dialog
    .getByRole("button", { name: /Simpan nilai|Save grade/ })
    .first()
    .click();
  await expect(sensei.getByText(/Nilai tersimpan|Grade saved/)).toBeVisible();
  await sensei.keyboard.press("Escape");
  await card.getByRole("button", { name: /^(Arsipkan|Archive)$/ }).click();

  await sensei.getByRole("link", { name: /^(Nilai|Grades)$/ }).click();
  await expect(sensei.getByRole("table").first()).toBeVisible();
  await expectAccessible(sensei);
});
