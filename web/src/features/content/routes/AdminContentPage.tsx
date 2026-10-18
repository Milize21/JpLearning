import { ChevronDown, ChevronRight, Layers, Pencil, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { ContentText } from "@/components/japanese/ContentText";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Badge, Card, EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { common } from "@/i18n/labels/common";
import { structure } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { useBook, useDeleteStructure, useLevels, type BookSummaryDto, type LevelDto, type StructureKind } from "../api/content";
import { StructureDialog, type StructureTarget } from "../components/StructureDialogs";

interface DeleteTarget {
  kind: StructureKind;
  id: string;
  name: string;
}

/** /admin/konten — levels, books and chapters (BR-KONTEN-03). */
export function AdminContentPage() {
  const t = useT();
  const levels = useLevels();
  const [dialog, setDialog] = useState<StructureTarget | null>(null);
  const [deleting, setDeleting] = useState<DeleteTarget | null>(null);
  const remove = useDeleteStructure();

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={structure.title} companionClassName="text-body font-medium" />}
        description={t(structure.description)}
        actions={
          <Button
            variant="primary"
            onClick={() => {
              setDialog({ kind: "level" });
            }}
          >
            <Plus aria-hidden="true" />
            {t(structure.addLevel)}
          </Button>
        }
      />

      {levels.isPending && <Skeleton className="h-40" />}
      {levels.data?.length === 0 && <EmptyState icon={Layers} title={<Label value={structure.emptyLevels} />} />}
      {levels.data?.map((level) => (
        <LevelCard key={level.id} level={level} onDialog={setDialog} onDelete={setDeleting} />
      ))}

      <StructureDialog
        target={dialog}
        onClose={() => {
          setDialog(null);
        }}
      />
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => {
          if (!open) setDeleting(null);
        }}
        title={t(structure.deleteTitle, { name: deleting?.name ?? "" })}
        description={t(structure.deleteBody)}
        confirmLabel={t(structure.deleteConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        destructive
        busy={remove.isPending}
        onConfirm={() => {
          if (!deleting) return;
          remove.mutate(
            { kind: deleting.kind, id: deleting.id },
            {
              onSuccess: () => {
                toast.success(t(structure.deleted));
                setDeleting(null);
              },
              onError: (error) => {
                toast.error(t(errorLabel(errorCode(error))));
              },
            },
          );
        }}
      />
    </div>
  );
}

function LevelCard({
  level,
  onDialog,
  onDelete,
}: {
  level: LevelDto;
  onDialog: (target: StructureTarget) => void;
  onDelete: (target: DeleteTarget) => void;
}) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const name = textIn(level.name, uiLocale);

  return (
    <Card className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <h2 className="flex items-center gap-3 text-h3 font-bold text-ink">
          <ContentText value={level.name} />
          <Badge tone="solid">{level.code}</Badge>
        </h2>
        <div className="flex gap-1">
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              onDialog({ kind: "book", levelId: level.id });
            }}
          >
            <Plus aria-hidden="true" />
            {t(structure.addBook)}
          </Button>
          <Button
            variant="ghost"
            size="icon"
            aria-label={`${t(common.edit)} ${name}`}
            onClick={() => {
              onDialog({ kind: "level", level });
            }}
          >
            <Pencil aria-hidden="true" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            aria-label={`${t(structure.delete)} ${name}`}
            onClick={() => {
              onDelete({ kind: "level", id: level.id, name });
            }}
          >
            <Trash2 aria-hidden="true" />
          </Button>
        </div>
      </div>
      {level.books.length === 0 ? (
        <p className="text-body-sm text-ink-muted">{t(structure.emptyBooks)}</p>
      ) : (
        <ul className="flex flex-col divide-y divide-line rounded-md border border-line">
          {level.books.map((book) => (
            <BookRow key={book.id} book={book} onDialog={onDialog} onDelete={onDelete} />
          ))}
        </ul>
      )}
    </Card>
  );
}

function BookRow({
  book,
  onDialog,
  onDelete,
}: {
  book: BookSummaryDto;
  onDialog: (target: StructureTarget) => void;
  onDelete: (target: DeleteTarget) => void;
}) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const [open, setOpen] = useState(false);
  const detail = useBook(open ? book.id : undefined);
  const name = textIn(book.title, uiLocale);
  const chapters = detail.data?.chapters ?? [];
  const nextNumber = chapters.reduce((max, chapter) => Math.max(max, chapter.number), 0) + 1;

  return (
    <li>
      <div className="flex items-center gap-2 px-3 py-2">
        <button
          type="button"
          className="flex min-h-11 flex-1 items-center gap-2 rounded-md text-left focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
          aria-expanded={open}
          onClick={() => {
            setOpen(!open);
          }}
        >
          {open ? <ChevronDown className="size-4" aria-hidden="true" /> : <ChevronRight className="size-4" aria-hidden="true" />}
          <span className="flex flex-col">
            <ContentText value={book.title} className="font-semibold text-ink" />
            <span className="text-caption text-ink-muted">{t(structure.chapterCount, { n: book.chapterCount })}</span>
          </span>
        </button>
        <Button
          variant="ghost"
          size="icon"
          aria-label={`${t(common.edit)} ${name}`}
          onClick={() => {
            onDialog({ kind: "book", levelId: book.levelId, book });
          }}
        >
          <Pencil aria-hidden="true" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          aria-label={`${t(structure.delete)} ${name}`}
          onClick={() => {
            onDelete({ kind: "book", id: book.id, name });
          }}
        >
          <Trash2 aria-hidden="true" />
        </Button>
      </div>
      {open && (
        <div className="flex flex-col gap-2 border-t border-line bg-canvas-subtle px-4 py-3">
          {detail.isPending && <Skeleton className="h-16" />}
          {detail.data && chapters.length === 0 && <p className="text-body-sm text-ink-muted">{t(structure.emptyChapters)}</p>}
          <ol className="flex flex-col gap-1">
            {chapters.map((chapter) => {
              const chapterName = textIn(chapter.title, uiLocale);
              return (
                <li key={chapter.id} className="flex items-center gap-2 rounded-md bg-surface px-3 py-2">
                  <Badge>{t(structure.chapterNumber, { n: chapter.number })}</Badge>
                  <ContentText value={chapter.title} companion="inline" className="flex-1 text-body-sm" />
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`${t(common.edit)} ${chapterName}`}
                    onClick={() => {
                      onDialog({ kind: "chapter", bookId: book.id, chapter, nextNumber });
                    }}
                  >
                    <Pencil aria-hidden="true" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`${t(structure.delete)} ${chapterName}`}
                    onClick={() => {
                      onDelete({ kind: "chapter", id: chapter.id, name: chapterName });
                    }}
                  >
                    <Trash2 aria-hidden="true" />
                  </Button>
                </li>
              );
            })}
          </ol>
          <Button
            variant="outline"
            size="sm"
            className="self-start"
            onClick={() => {
              onDialog({ kind: "chapter", bookId: book.id, nextNumber });
            }}
          >
            <Plus aria-hidden="true" />
            {t(structure.addChapter)}
          </Button>
        </div>
      )}
    </li>
  );
}
