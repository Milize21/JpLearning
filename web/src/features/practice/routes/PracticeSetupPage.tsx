import { useState } from "react";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Card, PageHeader, Skeleton } from "@/components/ui/misc";
import { FieldLabel, Select } from "@/components/ui/form";
import { useClasses } from "@/features/classroom/api/classroom";
import { useBooks } from "@/features/content/api/content";
import { errorLabel } from "@/i18n/labels/errors";
import { structure } from "@/i18n/labels/content";
import { categoryLabels, setup } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { categories, useStartDrill, type Category, type DrillMode } from "../api/practice";

/** /murid/latihan — choose category, chapter, mode and input, then start (docs/07 §11 flow 3). */
export function PracticeSetupPage() {
  const t = useT();
  const { uiLocale } = usePrefs();
  const navigate = useNavigate();
  const classes = useClasses();
  const bookIds = [
    ...new Set((classes.data ?? []).filter((item) => item.status !== "draft").map((item) => item.bookId)),
  ];
  const books = useBooks(bookIds);
  // The start button appears only once chapters are known, so it never flips from disabled to enabled under the user.
  const loadingChapters = classes.isPending || books.some((book) => book.isPending);
  const openChapters = books.flatMap(({ data }) =>
    (data?.chapters ?? []).filter((chapter) => chapter.isUnlocked !== false),
  );
  const start = useStartDrill();
  const [category, setCategory] = useState<Category>("hiragana");
  const [chapterId, setChapterId] = useState("");
  const [mode, setMode] = useState<DrillMode>("normal");
  const [inputMode, setInputMode] = useState<"choice" | "type">("choice");

  const begin = () => {
    start.mutate(
      { category, mode, inputMode, chapterIds: chapterId ? [chapterId] : null },
      {
        onSuccess: (started) => {
          void navigate(`/murid/latihan/${started.sessionId}`, {
            state: { batch: started.batch, fallback: started.fallback },
          });
        },
        onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
      },
    );
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={setup.title} companionClassName="text-body font-medium" />}
        description={t(setup.description)}
      />
      <Card className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <FieldLabel htmlFor="drill-category">{t(setup.category)}</FieldLabel>
          <Select
            id="drill-category"
            value={category}
            onChange={(event) => {
              setCategory(event.target.value as Category);
            }}
          >
            {categories.map((value) => (
              <option key={value} value={value}>
                {t(categoryLabels[value])}
              </option>
            ))}
          </Select>
        </div>
        <div className="flex flex-col gap-1.5">
          <FieldLabel htmlFor="drill-chapter">{t(setup.chapters)}</FieldLabel>
          <Select
            id="drill-chapter"
            value={chapterId}
            onChange={(event) => {
              setChapterId(event.target.value);
            }}
          >
            <option value="">{t(setup.allChapters)}</option>
            {openChapters.map((chapter) => (
              <option key={chapter.id} value={chapter.id}>
                {t(structure.chapterNumber, { n: chapter.number })} · {textIn(chapter.title, uiLocale)}
              </option>
            ))}
          </Select>
        </div>
        <div className="flex flex-col gap-1.5">
          <FieldLabel htmlFor="drill-mode">{t(setup.mode)}</FieldLabel>
          <Select
            id="drill-mode"
            value={mode}
            onChange={(event) => {
              setMode(event.target.value as DrillMode);
            }}
          >
            <option value="normal">{t(setup.modeNormal)}</option>
            <option value="weak_focus">{t(setup.modeWeak)}</option>
          </Select>
          <p className="text-body-sm text-ink-muted">
            {t(mode === "normal" ? setup.modeNormalHint : setup.modeWeakHint)}
          </p>
        </div>
        <div className="flex flex-col gap-1.5">
          <FieldLabel htmlFor="drill-input">{t(setup.input)}</FieldLabel>
          <Select
            id="drill-input"
            value={inputMode}
            onChange={(event) => {
              setInputMode(event.target.value as "choice" | "type");
            }}
          >
            <option value="choice">{t(setup.inputChoice)}</option>
            <option value="type">{t(setup.inputType)}</option>
          </Select>
        </div>
        {loadingChapters ? (
          <Skeleton className="h-12 w-28" />
        ) : openChapters.length === 0 ? (
          <p className="text-body-sm text-ink-muted">{t(setup.noChapters)}</p>
        ) : (
          <Button
            variant="primary"
            size="lg"
            className="self-start"
            loading={start.isPending}
            onClick={begin}
          >
            {t(setup.start)}
          </Button>
        )}
      </Card>
    </div>
  );
}
