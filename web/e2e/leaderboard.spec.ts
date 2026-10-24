import { expect, test, type Page } from "@playwright/test";
import {
  devUsers,
  expectAccessible,
  expectNoHorizontalScroll,
  loginAndWait,
  skipOnboarding,
} from "./helpers";

// Phase 4 (docs/08 §7): a correct drill answer reaches the student through the hub ("+1") and the live board.

test.beforeEach(async ({ page }) => {
  await skipOnboarding(page);
});

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

test("a correct drill answer shows +points and the student's row on the live leaderboard", async ({
  page,
}) => {
  test.setTimeout(90_000);
  await loginAndWait(page, devUsers.murid, /\/murid$/);
  await page.goto("/murid/latihan");
  await page.getByRole("button", { name: /^(Mulai|Start)$/ }).click();
  await expect(page).toHaveURL(/\/murid\/latihan\/[0-9a-f-]{36}$/);
  await page.getByLabel(/Lanjut otomatis|Continue automatically/).uncheck();

  // Answer until one is correct; the first choice is right often enough within a few questions.
  const correct = page.getByText(/^(Benar!|Correct!)$/).first();
  for (let i = 0; i < 30; i++) {
    await answerCurrent(page);
    await expect(page.getByText(/^(Benar!|Belum tepat|Correct!|Not quite)$/).first()).toBeVisible();
    if (await correct.isVisible()) break;
    await page.getByRole("button", { name: /^(Lanjut|Next)$/ }).click();
  }
  await expect(correct).toBeVisible();

  // docs/08 §7: points reach the student within 2 s (outbox poll ≤ 1 s + consumer + hub push).
  await expect(page.getByText(/^\+\d+(\.\d)? (poin|points)$/)).toBeVisible({ timeout: 2_000 });

  await page.goto("/murid/peringkat");
  await expect(page.getByRole("heading", { level: 1 })).toContainText(/Peringkat|Leaderboard/);
  await expect(page.getByText(/^(Live)$/)).toBeVisible({ timeout: 10_000 });
  await expect(page.getByText(/\((Kamu|You)\)/).first()).toBeVisible();
  await expectAccessible(page);
  await expectNoHorizontalScroll(page);
});

test("staff see every level's board and admins the season page", async ({ page, viewport }) => {
  test.skip((viewport?.width ?? 0) < 1000, "season management runs on desktop");
  await loginAndWait(page, devUsers.admin, /\/admin$/);
  await page.goto("/admin/peringkat");
  await expect(page.getByLabel(/^(Level)$/)).toBeVisible();
  await expect(page.getByRole("heading", { name: /Arsip season|Season archive/ })).toBeVisible();
  await expectAccessible(page);

  // The archive dialog needs a closed season; closing a real one would reset the dev boards, so the list and the
  // standings of one closed season are served from here while the real dialog renders them.
  const closedId = "0192f1e2-0000-7000-8000-00000000c105";
  await page.route(/\/api\/insight\/seasons\?/, async (route) => {
    const response = await route.fetch();
    const seasons = (await response.json()) as { levelId: string }[];
    const levelId = seasons[0]?.levelId ?? "b0000000-0000-4000-8000-000000000001";
    await route.fulfill({
      response,
      json: [
        ...seasons,
        {
          id: closedId,
          levelId,
          name: "Arsip · Musim 0",
          status: "closed",
          startsAt: "2026-01-01T00:00:00Z",
          endsAt: "2026-03-31T00:00:00Z",
        },
      ],
    });
  });
  await page.route(new RegExp(`/api/insight/seasons/${closedId}/standings`), (route) =>
    route.fulfill({
      json: {
        season: {
          id: closedId,
          levelId: "b0000000-0000-4000-8000-000000000001",
          name: "Arsip · Musim 0",
          status: "closed",
          startsAt: "2026-01-01T00:00:00Z",
          endsAt: "2026-03-31T00:00:00Z",
        },
        items: [
          { rank: 1, entryKey: "aaaaaaaaaaaaaaaa", displayName: "Ani", points: 120 },
          { rank: 2, entryKey: "bbbbbbbbbbbbbbbb", displayName: "Budi", points: 100 },
          { rank: 2, entryKey: "cccccccccccccccc", displayName: "Citra", points: 100 },
          { rank: 4, entryKey: "dddddddddddddddd", displayName: "Dina", points: 90 },
        ],
        page: 1,
        pageSize: 50,
        totalCount: 4,
        me: null,
      },
    }),
  );
  await page.reload();
  await page.getByRole("button", { name: /Lihat peringkat|View ranking/ }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("Citra")).toBeVisible();
  await expectAccessible(page);
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await page.unrouteAll();

  await page.goto("/admin/season");
  await expect(page.getByRole("heading", { level: 1 })).toContainText(/Season/);
  await expect(page.getByRole("button", { name: /Tutup season|Close season/ }).first()).toBeVisible();
  await expectAccessible(page);

  // The destructive confirmation is checked too, then cancelled: nothing is closed.
  await page
    .getByRole("button", { name: /Tutup season|Close season/ })
    .first()
    .click();
  await expect(page.getByRole("alertdialog").or(page.getByRole("dialog"))).toBeVisible();
  await expectAccessible(page);
  await page.getByRole("button", { name: /^(Batal|Cancel)$/ }).click();
  await expect(page.getByRole("alertdialog").or(page.getByRole("dialog"))).toBeHidden();
});
