import { BookOpenText, ChevronLeft, ChevronRight, Trash2, Upload } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { ContentText } from "@/components/japanese/ContentText";
import { JapaneseText } from "@/components/japanese/JapaneseText";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { FieldLabel, Select } from "@/components/ui/form";
import { EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { Table, TD, TH, THead, TR } from "@/components/ui/table";
import { common } from "@/i18n/labels/common";
import { reference, structure } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn, toSegments } from "@/i18n/textblock";
import type { UiLabel } from "@/i18n/tb";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import {
  referenceTypes,
  useBooks,
  useDeleteReference,
  useLevels,
  useMapReferenceChapter,
  useReference,
  type ReferenceItems,
  type ReferenceType,
} from "../api/content";
import { ImportReferenceDialog } from "../components/ImportReferenceDialog";

const typeLabels: Record<ReferenceType, UiLabel> = {
  kana: reference.kana,
  kanji: reference.kanji,
  vocab: reference.vocab,
  grammar: reference.grammar,
  sentences: reference.sentences,
};

const pageSize = 50;

/** /admin/referensi — reference data per level, import with source and license, chapter mapping (BR-KONTEN-04). */
export function AdminReferencePage() {
  const t = useT();
  const { uiLocale } = usePrefs();
  const levels = useLevels();
  const [type, setType] = useState<ReferenceType>("kana");
  const [levelId, setLevelId] = useState<string>("");
  const [page, setPage] = useState(1);
  const [importing, setImporting] = useState(false);
  const [deleting, setDeleting] = useState<string | null>(null);
  const selectedLevelId = levelId || levels.data?.[0]?.id || "";
  const level = levels.data?.find((item) => item.id === selectedLevelId);
  const items = useReference(type, { levelId: selectedLevelId, page, pageSize }, selectedLevelId !== "");
  const mapChapter = useMapReferenceChapter();
  const remove = useDeleteReference();

  // Chapters of every book of the level, for the mapping select.
  const books = useBooks((level?.books ?? []).map((book) => book.id));
  const chapters = books.flatMap((book) =>
    (book.data?.chapters ?? []).map((chapter) => ({
      id: chapter.id,
      name: `${textIn(book.data?.title, uiLocale)} · ${t(structure.chapterNumber, { n: chapter.number })}`,
    })),
  );
  const totalPages = Math.max(1, Math.ceil((items.data?.totalCount ?? 0) / pageSize));

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={reference.title} companionClassName="text-body font-medium" />}
        description={t(reference.description)}
        actions={
          <Button
            variant="primary"
            disabled={!level}
            onClick={() => {
              setImporting(true);
            }}
          >
            <Upload aria-hidden="true" />
            {t(reference.import)}
          </Button>
        }
      />

      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div role="tablist" aria-label={t(reference.title)} className="flex flex-wrap gap-1 border-b border-line">
          {referenceTypes.map((item) => (
            <button
              key={item}
              type="button"
              role="tab"
              aria-selected={item === type}
              className={cn(
                "min-h-11 border-b-2 px-3 text-body-sm text-ink-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none",
                item === type ? "border-action font-semibold text-ink" : "border-transparent hover:text-ink",
              )}
              onClick={() => {
                setType(item);
                setPage(1);
              }}
            >
              {t(typeLabels[item])}
            </button>
          ))}
        </div>
        <div className="flex items-center gap-2">
          <FieldLabel htmlFor="reference-level">{t(reference.filterLevel)}</FieldLabel>
          <Select
            id="reference-level"
            className="w-48"
            value={selectedLevelId}
            onChange={(event) => {
              setLevelId(event.target.value);
              setPage(1);
            }}
          >
            {levels.data?.map((item) => (
              <option key={item.id} value={item.id}>
                {textIn(item.name, uiLocale)}
              </option>
            ))}
          </Select>
        </div>
      </div>

      {items.isPending && <Skeleton className="h-48" />}
      {items.data?.items.length === 0 && <EmptyState icon={BookOpenText} title={<Label value={reference.empty} />} />}
      {items.data && items.data.items.length > 0 && (
        <div className="overflow-x-auto">
          <Table>
            <THead>
              <TR>
                <ReferenceHeader type={type} />
                <TH>{t(reference.mapChapter)}</TH>
                <TH>{t(reference.source)}</TH>
                <TH>
                  <span className="sr-only">{t(common.actions)}</span>
                </TH>
              </TR>
            </THead>
            <tbody>
              {items.data.items.map((item) => (
                <TR key={item.id}>
                  <ReferenceCells type={type} item={item} />
                  <TD>
                    <Select
                      aria-label={t(reference.mapChapter)}
                      className="min-w-44"
                      value={item.chapterId ?? ""}
                      title={item.chapterId ? undefined : t(reference.unmapped)}
                      onChange={(event) => {
                        mapChapter.mutate(
                          { type, id: item.id, chapterId: event.target.value || null },
                          {
                            onSuccess: () => toast.success(t(common.saved)),
                            onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
                          },
                        );
                      }}
                    >
                      <option value="">{t(reference.noChapter)}</option>
                      {chapters.map((chapter) => (
                        <option key={chapter.id} value={chapter.id}>
                          {chapter.name}
                        </option>
                      ))}
                    </Select>
                  </TD>
                  <TD className="text-caption text-ink-muted">
                    {item.source}
                    <br />
                    {item.license}
                  </TD>
                  <TD>
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={t(structure.delete)}
                      onClick={() => {
                        setDeleting(item.id);
                      }}
                    >
                      <Trash2 aria-hidden="true" />
                    </Button>
                  </TD>
                </TR>
              ))}
            </tbody>
          </Table>
        </div>
      )}

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2 text-body-sm">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => {
              setPage(page - 1);
            }}
          >
            <ChevronLeft aria-hidden="true" />
            {t(common.previous)}
          </Button>
          <span>{t(common.pageOf, { page, pages: totalPages })}</span>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= totalPages}
            onClick={() => {
              setPage(page + 1);
            }}
          >
            {t(common.next)}
            <ChevronRight aria-hidden="true" />
          </Button>
        </div>
      )}

      <ImportReferenceDialog open={importing} onOpenChange={setImporting} type={type} typeLabel={t(typeLabels[type])} level={level} />
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => {
          if (!open) setDeleting(null);
        }}
        title={t(reference.deleteTitle)}
        description={t(reference.deleteBody)}
        confirmLabel={t(structure.deleteConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        destructive
        busy={remove.isPending}
        onConfirm={() => {
          if (!deleting) return;
          remove.mutate(
            { type, id: deleting },
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

function ReferenceHeader({ type }: { type: ReferenceType }) {
  const t = useT();
  switch (type) {
    case "kana":
      return (
        <>
          <TH>{t(reference.character)}</TH>
          <TH>{t(reference.romaji)}</TH>
        </>
      );
    case "kanji":
      return (
        <>
          <TH>{t(reference.character)}</TH>
          <TH>{t(reference.reading)}</TH>
          <TH>{t(reference.meanings)}</TH>
        </>
      );
    case "vocab":
      return (
        <>
          <TH>{t(reference.character)}</TH>
          <TH>{t(reference.reading)}</TH>
          <TH>{t(reference.meanings)}</TH>
        </>
      );
    case "grammar":
      return (
        <>
          <TH>{t(reference.pattern)}</TH>
          <TH>{t(reference.meanings)}</TH>
        </>
      );
    default:
      return <TH>{t(reference.text)}</TH>;
  }
}

function ReferenceCells({ type, item }: { type: ReferenceType; item: ReferenceItems[ReferenceType] }) {
  const { uiLocale } = usePrefs();
  const meanings = (value: { id: string[]; en: string[] }) => (uiLocale === "en" && value.en.length > 0 ? value.en : value.id).join(", ");
  switch (type) {
    case "kana": {
      const kana = item as ReferenceItems["kana"];
      return (
        <>
          <TD lang="ja" className="font-learn text-h3">
            {kana.character}
          </TD>
          <TD lang="ja-Latn">{kana.romaji}</TD>
        </>
      );
    }
    case "kanji": {
      const kanji = item as ReferenceItems["kanji"];
      return (
        <>
          <TD lang="ja" className="font-learn text-h3">
            {kanji.character}
          </TD>
          <TD lang="ja" className="font-ja">
            {[...kanji.onyomi, ...kanji.kunyomi].join("、")}
          </TD>
          <TD>{meanings(kanji.meanings)}</TD>
        </>
      );
    }
    case "vocab": {
      const vocab = item as ReferenceItems["vocab"];
      return (
        <>
          <TD lang="ja" className="font-ja text-body-lg">
            {vocab.word}
          </TD>
          <TD lang="ja" className="font-ja">
            {vocab.reading}
          </TD>
          <TD>{meanings(vocab.meanings)}</TD>
        </>
      );
    }
    case "grammar": {
      const grammar = item as ReferenceItems["grammar"];
      return (
        <>
          <TD lang="ja" className="font-ja">
            {grammar.pattern}
          </TD>
          <TD>
            <ContentText value={grammar.title} />
          </TD>
        </>
      );
    }
    default: {
      const sentence = item as ReferenceItems["sentences"];
      return (
        <TD>
          <span className="flex flex-col">
            <JapaneseText value={toSegments(sentence.text.ja)} />
            <span className="text-body-sm text-ink-muted">{textIn(sentence.text, uiLocale)}</span>
          </span>
        </TD>
      );
    }
  }
}
