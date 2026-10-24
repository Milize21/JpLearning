import { ChevronLeft, CircleAlert, FileText, Lock } from "lucide-react";
import { Link, useParams } from "react-router";
import { ContentText } from "@/components/japanese/ContentText";
import { Label } from "@/components/japanese/Label";
import { Badge, EmptyState, Skeleton } from "@/components/ui/misc";
import { chapterPage, materials, structure } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { useChapter, useMaterials } from "../api/content";
import { MaterialCard } from "../components/MaterialCard";

/** /murid/bab/:chapterId — the materials of one open chapter (BR-KONTEN-01); locked chapters answer 403. */
export function ChapterPage() {
  const t = useT();
  const { chapterId } = useParams();
  const chapter = useChapter(chapterId);
  const list = useMaterials(chapterId);
  const code = chapter.isError ? errorCode(chapter.error) : list.isError ? errorCode(list.error) : null;

  const back = (
    <Link
      to="/murid/materi"
      className="inline-flex items-center gap-1 self-start rounded-sm text-body-sm text-ink-muted hover:text-ink focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
    >
      <ChevronLeft className="size-4" aria-hidden="true" />
      {t(chapterPage.back)}
    </Link>
  );

  if (code === "chapter_locked") {
    return (
      <div className="flex flex-col gap-6">
        {back}
        <EmptyState icon={Lock} title={<Label value={chapterPage.lockedTitle} />}>
          {t(chapterPage.lockedBody)}
        </EmptyState>
      </div>
    );
  }
  if (code) {
    return (
      <div className="flex flex-col gap-6">
        {back}
        <EmptyState icon={CircleAlert} title={t(errorLabel(code))} />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      {back}
      {!chapter.data ? (
        <Skeleton className="h-16" />
      ) : (
        <div className="flex flex-col gap-2">
          <Badge tone="brand" className="self-start">
            {t(structure.chapterNumber, { n: chapter.data.number })}
          </Badge>
          <h1 className="text-h2 font-bold text-ink sm:text-h1">
            <ContentText value={chapter.data.title} companionClassName="text-body font-medium" />
          </h1>
        </div>
      )}
      <section className="flex flex-col gap-3" aria-labelledby="chapter-materials">
        <h2 id="chapter-materials" className="text-body font-bold text-ink">
          <Label value={materials.title} companion="inline" companionClassName="text-body-sm font-medium" />
        </h2>
        {list.isPending && <Skeleton className="h-24" />}
        {list.data?.length === 0 && (
          <EmptyState icon={FileText} title={<Label value={materials.empty} />}>
            {t(materials.emptyStudent)}
          </EmptyState>
        )}
        {list.data?.map((material) => (
          <MaterialCard key={material.id} material={material} />
        ))}
      </section>
    </div>
  );
}
