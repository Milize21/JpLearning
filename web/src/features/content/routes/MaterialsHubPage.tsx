import { BookOpen } from "lucide-react";
import { ContentText } from "@/components/japanese/ContentText";
import { Label } from "@/components/japanese/Label";
import { EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { useClasses } from "@/features/classroom/api/classroom";
import { classes as classLabels } from "@/i18n/labels/classroom";
import { chapterPage } from "@/i18n/labels/content";
import { useT } from "@/i18n/prefs";
import { useBooks } from "../api/content";
import { ChapterCard } from "../components/ChapterCard";

/** /murid/materi — the books of the student's classes with their chapters, open or locked (BR-AKSES-01). */
export function MaterialsHubPage() {
  const t = useT();
  const list = useClasses();
  const bookIds = [
    ...new Set((list.data ?? []).filter((item) => item.status !== "draft").map((item) => item.bookId)),
  ];
  const books = useBooks(bookIds);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={chapterPage.myMaterials} companionClassName="text-body font-medium" />}
        description={t(chapterPage.materialsDescription)}
      />
      {(list.isPending || books.some((book) => book.isPending)) && <Skeleton className="h-40" />}
      {list.data && bookIds.length === 0 && (
        <EmptyState icon={BookOpen} title={<Label value={classLabels.empty} />} />
      )}
      {books.map(({ data: book }) =>
        book ? (
          <section key={book.id} className="flex flex-col gap-3" aria-labelledby={`book-${book.id}`}>
            <h2 id={`book-${book.id}`} className="text-h3 font-semibold text-ink">
              <ContentText value={book.title} companionClassName="text-body-sm font-medium" />
            </h2>
            <ol className="flex flex-col gap-2">
              {book.chapters.map((chapter) => (
                <li key={chapter.id}>
                  <ChapterCard chapter={chapter} />
                </li>
              ))}
            </ol>
          </section>
        ) : null,
      )}
    </div>
  );
}
