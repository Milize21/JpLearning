import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { ContentText } from "@/components/japanese/ContentText";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { FieldLabel, Select } from "@/components/ui/form";
import { Badge, Skeleton } from "@/components/ui/misc";
import { Switch } from "@/components/ui/switch";
import { useBook } from "@/features/content/api/content";
import { unlocks as labels } from "@/i18n/labels/classroom";
import { common } from "@/i18n/labels/common";
import { structure } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { formatDate } from "@/lib/datetime";
import { useClassUnlocks, useSetUnlock, type ClassDto } from "../api/classroom";

/**
 * "Bab" tab of a class (docs/07 §11 flow 6): one switch per chapter of the class's book. Closing asks for confirmation
 * (BR-AKSES-02); "open up to chapter N" opens every earlier chapter in one go.
 */
export function ChaptersPanel({ classInfo }: { classInfo: ClassDto }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const book = useBook(classInfo.bookId);
  const unlocked = useClassUnlocks(classInfo.id);
  const setUnlock = useSetUnlock(classInfo.id);
  const [closing, setClosing] = useState<{ id: string; name: string } | null>(null);
  const [bulkBusy, setBulkBusy] = useState(false);
  const readOnly = classInfo.status === "archived";
  const unlockedAt = new Map((unlocked.data ?? []).map((unlock) => [unlock.chapterId, unlock.unlockedAt]));
  const chapters = book.data?.chapters ?? [];

  const change = (chapterId: string, open: boolean) =>
    setUnlock.mutateAsync({ chapterId, unlocked: open }).catch((error: unknown) => {
      toast.error(t(errorLabel(errorCode(error))));
      throw error;
    });

  const openUntil = async (number: number) => {
    setBulkBusy(true);
    const toOpen = chapters.filter((chapter) => chapter.number <= number && !unlockedAt.has(chapter.id));
    try {
      for (const chapter of toOpen) {
        await change(chapter.id, true);
      }
      toast.success(t(labels.openUntilDone, { n: toOpen.length }));
    } catch {
      // The failing chapter was reported above.
    } finally {
      setBulkBusy(false);
    }
  };

  if (book.isPending || unlocked.isPending) return <Skeleton className="h-40" />;

  return (
    <div className="flex flex-col gap-4">
      {!readOnly && chapters.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          <FieldLabel htmlFor="open-until">{t(labels.openUntil)}</FieldLabel>
          <Select
            id="open-until"
            className="w-auto"
            value=""
            disabled={bulkBusy}
            onChange={(event) => {
              if (event.target.value) void openUntil(Number(event.target.value));
            }}
          >
            <option value="">—</option>
            {chapters.map((chapter) => (
              <option key={chapter.id} value={chapter.number}>
                {t(structure.chapterNumber, { n: chapter.number })}
              </option>
            ))}
          </Select>
        </div>
      )}
      {chapters.length === 0 && <p className="text-body-sm text-ink-muted">{t(structure.emptyChapters)}</p>}
      <ol className="flex flex-col divide-y divide-line rounded-lg border border-line bg-surface">
        {chapters.map((chapter) => {
          const openedOn = unlockedAt.get(chapter.id);
          const name = `${t(structure.chapterNumber, { n: chapter.number })} · ${textIn(chapter.title, uiLocale)}`;
          return (
            <li key={chapter.id} className="flex items-center gap-3 px-4 py-3">
              <Badge>{t(structure.chapterNumber, { n: chapter.number })}</Badge>
              <span className="flex min-w-0 flex-1 flex-col">
                <ContentText value={chapter.title} className="text-ink" />
                <span className="text-caption text-ink-muted">
                  {openedOn ? t(labels.openedOn, { date: formatDate(openedOn, uiLocale, me.timezone) }) : t(labels.locked)}
                </span>
              </span>
              <Switch
                checked={openedOn !== undefined}
                disabled={readOnly || setUnlock.isPending || bulkBusy}
                aria-label={t(labels.toggle, { name })}
                onCheckedChange={(checked) => {
                  if (checked) {
                    void change(chapter.id, true).then(() => toast.success(t(labels.opened)));
                  } else {
                    setClosing({ id: chapter.id, name });
                  }
                }}
              />
            </li>
          );
        })}
      </ol>
      <ConfirmDialog
        open={closing !== null}
        onOpenChange={(open) => {
          if (!open) setClosing(null);
        }}
        title={t(labels.closeTitle, { name: closing?.name ?? "" })}
        description={t(labels.closeBody)}
        confirmLabel={t(labels.closeConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        destructive
        busy={setUnlock.isPending}
        onConfirm={() => {
          if (!closing) return;
          void change(closing.id, false).then(() => {
            toast.success(t(labels.closed));
            setClosing(null);
          });
        }}
      />
    </div>
  );
}
