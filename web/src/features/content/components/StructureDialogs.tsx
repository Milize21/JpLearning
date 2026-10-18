import { zodResolver } from "@hookform/resolvers/zod";
import { CircleAlert } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldError, FieldHint, FieldLabel, Input } from "@/components/ui/form";
import { Alert } from "@/components/ui/misc";
import { common } from "@/i18n/labels/common";
import { structure } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { fieldErrors } from "@/i18n/labels/identity";
import { useT } from "@/i18n/prefs";
import { fieldsToTextBlock, textBlockToFields } from "@/i18n/textblock";
import type { UiLabel } from "@/i18n/tb";
import { errorCode } from "@/lib/api/errors";
import {
  useSaveBook,
  useSaveChapter,
  useSaveLevel,
  type BookSummaryDto,
  type ChapterSummaryDto,
  type LevelDto,
} from "../api/content";
import { emptyTextBlock, TextBlockInput, textBlockSchema } from "./TextBlockInput";

type Target =
  | { kind: "level"; level?: LevelDto }
  | { kind: "book"; levelId: string; book?: BookSummaryDto }
  | { kind: "chapter"; bookId: string; chapter?: ChapterSummaryDto; nextNumber: number };

const schema = z.object({
  code: z.string(),
  number: z.coerce.number<number>().int().min(1).max(9999),
  sortOrder: z.coerce.number<number>().int().min(0).max(100_000),
  text: textBlockSchema,
});

type FormValues = z.infer<typeof schema>;

const titles: Record<Target["kind"], { create: UiLabel; edit: UiLabel; text: UiLabel }> = {
  level: { create: structure.addLevel, edit: structure.editLevel, text: structure.level },
  book: { create: structure.addBook, edit: structure.editBook, text: structure.book },
  chapter: { create: structure.addChapter, edit: structure.editChapter, text: structure.chapter },
};

/** Create or edit a level, book or chapter (admin, BR-KONTEN-03). Names are TextBlocks with a Japanese companion. */
export function StructureDialog({ target, onClose }: { target: Target | null; onClose: () => void }) {
  return (
    <Dialog
      open={target !== null}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      {target && <StructureForm key={JSON.stringify(target)} target={target} onClose={onClose} />}
    </Dialog>
  );
}

function StructureForm({ target, onClose }: { target: Target; onClose: () => void }) {
  const t = useT();
  const saveLevel = useSaveLevel();
  const saveBook = useSaveBook();
  const saveChapter = useSaveChapter();
  const [formError, setFormError] = useState<string | null>(null);
  const existing = target.kind === "level" ? target.level : target.kind === "book" ? target.book : target.chapter;
  const editing = existing !== undefined;

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      code: target.kind === "level" ? (target.level?.code ?? "") : "",
      number: target.kind === "chapter" ? (target.chapter?.number ?? target.nextNumber) : 1,
      sortOrder: existing?.sortOrder ?? (target.kind === "chapter" ? target.nextNumber : 0),
      text: existing
        ? textBlockToFields(target.kind === "level" ? target.level?.name : target.kind === "book" ? target.book?.title : target.chapter?.title)
        : emptyTextBlock,
    },
  });
  const { control, register, handleSubmit, formState, setError } = form;

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    if (target.kind === "level" && !/^[a-z0-9-]{1,16}$/u.test(values.code)) {
      setError("code", { message: "invalid" });
      return;
    }
    const text = fieldsToTextBlock(values.text);
    try {
      if (target.kind === "level") {
        await saveLevel.mutateAsync({ level: target.level, input: { code: values.code, name: text, sortOrder: values.sortOrder } });
      } else if (target.kind === "book") {
        await saveBook.mutateAsync({ book: target.book, input: { levelId: target.levelId, title: text, sortOrder: values.sortOrder } });
      } else {
        await saveChapter.mutateAsync({
          chapter: target.chapter,
          input: { bookId: target.bookId, number: values.number, title: text, sortOrder: values.sortOrder },
        });
      }
      toast.success(t(common.saved));
      onClose();
    } catch (error) {
      setFormError(errorCode(error));
    }
  });

  const pending = saveLevel.isPending || saveBook.isPending || saveChapter.isPending;
  const labels = titles[target.kind];

  return (
    <DialogContent closeLabel={t(common.close)} aria-describedby={undefined} className="max-h-[90dvh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>
          <Label value={editing ? labels.edit : labels.create} companion="inline" />
        </DialogTitle>
      </DialogHeader>
      <form onSubmit={(event) => void onSubmit(event)} noValidate className="flex flex-col gap-4">
        {target.kind === "level" && (
          <Field>
            <FieldLabel htmlFor="code">{t(structure.code)}</FieldLabel>
            <Input
              id="code"
              autoComplete="off"
              autoCapitalize="none"
              aria-invalid={formState.errors.code ? true : undefined}
              aria-describedby="code-hint code-error"
              {...register("code")}
            />
            <FieldHint id="code-hint">{t(structure.codeHint)}</FieldHint>
            {formState.errors.code && <FieldError id="code-error">{t(fieldErrors.invalid)}</FieldError>}
          </Field>
        )}
        {target.kind === "chapter" && (
          <Field>
            <FieldLabel htmlFor="number">{t(structure.number)}</FieldLabel>
            <Input id="number" type="number" min={1} max={9999} inputMode="numeric" {...register("number")} />
          </Field>
        )}
        <Controller
          control={control}
          name="text"
          render={({ field, fieldState }) => (
            <TextBlockInput
              id="text"
              label={labels.text}
              value={field.value}
              onChange={field.onChange}
              errors={{
                id: (fieldState.error as { id?: { message?: string } } | undefined)?.id?.message,
                ja: (fieldState.error as { ja?: { message?: string } } | undefined)?.ja?.message,
              }}
            />
          )}
        />
        <Field>
          <FieldLabel htmlFor="sortOrder">{t(structure.sortOrder)}</FieldLabel>
          <Input id="sortOrder" type="number" min={0} inputMode="numeric" {...register("sortOrder")} />
        </Field>
        {formError && (
          <Alert tone="danger" icon={CircleAlert}>
            {t(errorLabel(formError))}
          </Alert>
        )}
        <DialogFooter>
          <Button variant="outline" type="button" onClick={onClose}>
            {t(common.cancel)}
          </Button>
          <Button variant="primary" type="submit" loading={pending}>
            {t(common.save)}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  );
}

export type { Target as StructureTarget };
