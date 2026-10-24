import { ChevronRight, Lock } from "lucide-react";
import { Link } from "react-router";
import { ContentText } from "@/components/japanese/ContentText";
import { Badge } from "@/components/ui/misc";
import { chapterPage, structure } from "@/i18n/labels/content";
import { useT } from "@/i18n/prefs";
import type { ChapterSummaryDto } from "../api/content";

/**
 * ChapterCard of docs/07 §10 for students: open chapters link to their materials, locked ones are greyed with a lock
 * icon and text (never colour alone). The "mastered" state arrives with practice in phase 2.
 */
export function ChapterCard({ chapter }: { chapter: ChapterSummaryDto }) {
  const t = useT();
  const number = t(structure.chapterNumber, { n: chapter.number });

  if (chapter.isUnlocked === false) {
    return (
      <div className="flex items-center gap-3 rounded-lg border border-line bg-sumi-50 px-4 py-3 text-sumi-500">
        <Badge>{number}</Badge>
        <span className="min-w-0 flex-1">
          <ContentText value={chapter.title} companion="none" />
        </span>
        <span className="flex items-center gap-1 text-caption">
          <Lock className="size-4" aria-hidden="true" />
          {t(chapterPage.locked)}
        </span>
      </div>
    );
  }

  return (
    <Link
      to={`/murid/bab/${chapter.id}`}
      className="flex items-center gap-3 rounded-lg border border-line bg-surface px-4 py-3 transition-shadow hover:shadow-sm focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
    >
      <Badge tone="brand">{number}</Badge>
      <span className="min-w-0 flex-1 text-ink">
        <ContentText value={chapter.title} />
      </span>
      <span className="sr-only">{t(chapterPage.unlocked)}</span>
      <ChevronRight className="size-5 shrink-0 text-ink-subtle" aria-hidden="true" />
    </Link>
  );
}
