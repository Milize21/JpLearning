import { FileText, Plus } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { FieldLabel, Select } from "@/components/ui/form";
import { EmptyState, Skeleton } from "@/components/ui/misc";
import { useBook, useDeleteMaterial, useMaterials, type MaterialDto } from "@/features/content/api/content";
import { MaterialCard } from "@/features/content/components/MaterialCard";
import { MaterialFormDialog } from "@/features/content/components/MaterialFormDialog";
import { common } from "@/i18n/labels/common";
import { materials, structure } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import type { ClassDto } from "../api/classroom";

/**
 * "Materi" tab: materials of a chapter of the class's book. Materials belong to the chapter, so every class that opens
 * it sees them (D-26).
 */
export function MaterialsPanel({ classInfo }: { classInfo: ClassDto }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const book = useBook(classInfo.bookId);
  const [chapterId, setChapterId] = useState<string>("");
  const selected = chapterId || book.data?.chapters[0]?.id || "";
  const list = useMaterials(selected || undefined);
  const remove = useDeleteMaterial(selected);
  const [editing, setEditing] = useState<MaterialDto | undefined>(undefined);
  const [formOpen, setFormOpen] = useState(false);
  const [deleting, setDeleting] = useState<MaterialDto | null>(null);

  if (book.isPending) return <Skeleton className="h-40" />;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-col gap-1.5">
          <FieldLabel htmlFor="materials-chapter">{t(structure.chapter)}</FieldLabel>
          <Select
            id="materials-chapter"
            className="w-72 max-w-full"
            value={selected}
            onChange={(event) => {
              setChapterId(event.target.value);
            }}
          >
            {book.data?.chapters.map((chapter) => (
              <option key={chapter.id} value={chapter.id}>
                {t(structure.chapterNumber, { n: chapter.number })} · {textIn(chapter.title, uiLocale)}
              </option>
            ))}
          </Select>
        </div>
        {selected && classInfo.status !== "archived" && (
          <Button
            variant="primary"
            onClick={() => {
              setEditing(undefined);
              setFormOpen(true);
            }}
          >
            <Plus aria-hidden="true" />
            {t(materials.add)}
          </Button>
        )}
      </div>
      {list.isPending && selected && <Skeleton className="h-24" />}
      {list.data?.length === 0 && <EmptyState icon={FileText} title={<Label value={materials.empty} />} />}
      <div className="flex flex-col gap-3">
        {list.data?.map((material) => (
          <MaterialCard
            key={material.id}
            material={material}
            onEdit={(item) => {
              setEditing(item);
              setFormOpen(true);
            }}
            onDelete={setDeleting}
          />
        ))}
      </div>
      {selected && (
        <MaterialFormDialog
          chapterId={selected}
          material={editing}
          open={formOpen}
          onOpenChange={setFormOpen}
        />
      )}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => {
          if (!open) setDeleting(null);
        }}
        title={t(materials.deleteTitle, { name: deleting ? textIn(deleting.title, uiLocale) : "" })}
        description={t(materials.deleteBody)}
        confirmLabel={t(structure.deleteConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        destructive
        busy={remove.isPending}
        onConfirm={() => {
          if (!deleting) return;
          remove.mutate(deleting.id, {
            onSuccess: () => {
              toast.success(t(structure.deleted));
              setDeleting(null);
            },
            onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
          });
        }}
      />
    </div>
  );
}
