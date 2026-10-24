import { useNavigate } from "react-router";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { useChapter } from "@/features/content/api/content";
import { structure } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { categoryLabels, weaknesses as labels } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import {
  categories,
  useStartDrill,
  type Category,
  type WeaknessGroup,
  type Weaknesses,
} from "../api/practice";
import { ItemListHeading, MasteryItemRow } from "./MasteryItemRow";

const isCategory = (key: string): key is Category => (categories as readonly string[]).includes(key);

function ChapterName({ id }: { id: string }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const chapter = useChapter(id);
  if (!chapter.data) return <>{t(labels.chapter)}</>;
  return (
    <>
      {t(structure.chapterNumber, { n: chapter.data.number })} · {textIn(chapter.data.title, uiLocale)}
    </>
  );
}

/** Readable name of a BR-MASTERY-03 group: category, kana row, particle, other tag or chapter. */
export function GroupName({ group }: { group: WeaknessGroup }) {
  const t = useT();
  if (group.kind === "category")
    return <>{isCategory(group.key) ? t(categoryLabels[group.key]) : group.key}</>;
  if (group.kind === "chapter") return <ChapterName id={group.key} />;
  const [prefix, name = ""] = group.key.split(":", 2);
  if (prefix === "kana_row") return <>{t(labels.kanaRow, { row: name })}</>;
  if (prefix === "particle") return <>{t(labels.particle, { name })}</>;
  return <>{t(labels.tag, { name: group.key })}</>;
}

/**
 * WeaknessList of docs/07 §10: the weakest groups with "Practise this" (weak-focus mode, BR-DRILL-04) and the weakest
 * items. `practise` is only offered to the student themselves.
 */
export function WeaknessList({ data, practise = false }: { data: Weaknesses; practise?: boolean }) {
  const t = useT();
  const navigate = useNavigate();
  const start = useStartDrill();
  const pct = (value: number) => Math.round(value * 100);

  const practiseCategory = (category: Category) => {
    start.mutate(
      { category, mode: "weak_focus" },
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

  const weakestCategory = data.weakestItems[0]?.category;

  return (
    <div className="flex flex-col gap-6">
      <section className="flex flex-col gap-3" aria-labelledby="weak-groups">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <ItemListHeading id="weak-groups" value={labels.groups} />
          {practise && weakestCategory && (
            <Button
              variant="primary"
              loading={start.isPending}
              onClick={() => {
                practiseCategory(weakestCategory);
              }}
            >
              {t(labels.practiceThis)}
            </Button>
          )}
        </div>
        <ul className="grid gap-3 md:grid-cols-2">
          {data.groups.map((group) => (
            <li
              key={`${group.kind}:${group.key}`}
              className="flex flex-col gap-2 rounded-lg border border-line bg-surface p-4"
            >
              <span className="font-semibold text-ink">
                <GroupName group={group} />
              </span>
              <span className="text-body-sm text-ink-muted">
                {t(labels.averageMastery, { pct: pct(group.averageMastery) })} ·{" "}
                {t(labels.accuracy, { pct: pct(group.accuracy) })}
              </span>
              <span className="text-caption text-ink-muted">
                {t(labels.itemCount, { n: group.itemCount })}
                {group.weakCount > 0 && (
                  <span className="text-danger"> · {t(labels.weakCount, { n: group.weakCount })}</span>
                )}
              </span>
              {practise && group.kind === "category" && isCategory(group.key) && (
                <Button
                  variant="secondary"
                  size="sm"
                  className="self-start"
                  disabled={start.isPending}
                  onClick={() => {
                    if (isCategory(group.key)) practiseCategory(group.key);
                  }}
                >
                  {t(labels.practiceThis)}
                </Button>
              )}
            </li>
          ))}
        </ul>
      </section>
      <section className="flex flex-col gap-3" aria-labelledby="weak-items">
        <ItemListHeading id="weak-items" value={labels.items} />
        <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
          {data.weakestItems.map((item) => (
            <MasteryItemRow key={`${item.itemType}:${item.itemId}`} item={item} />
          ))}
        </ul>
      </section>
    </div>
  );
}
