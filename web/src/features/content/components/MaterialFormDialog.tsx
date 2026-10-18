import { CircleAlert } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldError, FieldHint, FieldLabel, Input, Select, Textarea } from "@/components/ui/form";
import { Alert } from "@/components/ui/misc";
import { common } from "@/i18n/labels/common";
import { materials } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { fieldErrors } from "@/i18n/labels/identity";
import { parseFurigana } from "@/i18n/furigana";
import { useT } from "@/i18n/prefs";
import { fieldsToTextBlock, textBlockToFields, type TextBlockFields } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { isoToZoned, zonedToIso } from "@/lib/datetime";
import { useCreateMaterial, useUpdateMaterial, type MaterialDto, type MaterialKind } from "../api/content";
import { emptyTextBlock, TextBlockInput } from "./TextBlockInput";
import { UploadField } from "./UploadField";

/** Same checks as the server (TextRules): no raw HTML, no inline images, https links only. */
function markdownProblem(markdown: string): boolean {
  if (/<[A-Za-z/!?]/u.test(markdown) || /!\[[^\]]*\]\(/u.test(markdown)) return true;
  for (const match of markdown.matchAll(/\[[^\]]*\]\(([^)\s]*)/gu)) {
    if (!(match[1] ?? "").startsWith("https://")) return true;
  }
  return false;
}

/** Create or edit a material of a chapter (staff, BR-KONTEN-01/02/05). */
export function MaterialFormDialog({
  chapterId,
  material,
  open,
  onOpenChange,
}: {
  chapterId: string;
  material?: MaterialDto;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {open && (
        <MaterialForm
          key={material?.id ?? "new"}
          chapterId={chapterId}
          material={material}
          onClose={() => {
            onOpenChange(false);
          }}
        />
      )}
    </Dialog>
  );
}

function MaterialForm({ chapterId, material, onClose }: { chapterId: string; material?: MaterialDto; onClose: () => void }) {
  const t = useT();
  const me = useCurrentUser();
  const create = useCreateMaterial(chapterId);
  const update = useUpdateMaterial(chapterId);
  const [kind, setKind] = useState<MaterialKind>(material?.kind ?? "richtext");
  const [title, setTitle] = useState<TextBlockFields>(material ? textBlockToFields(material.title) : emptyTextBlock);
  const [bodyId, setBodyId] = useState(material?.body?.id ?? "");
  const [bodyEn, setBodyEn] = useState(material?.body?.en ?? "");
  const [url, setUrl] = useState(material?.url ?? "");
  const [fileId, setFileId] = useState<string | null>(material?.file?.id ?? null);
  const initialPublish = material?.publishAt ? isoToZoned(material.publishAt, me.timezone) : { date: "", time: "" };
  const [publishDate, setPublishDate] = useState(initialPublish.date);
  const [publishTime, setPublishTime] = useState(initialPublish.time);
  const [submitted, setSubmitted] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const titleMissing = title.id.trim() === "";
  const titleMarkupInvalid = title.ja.trim() !== "" && parseFurigana(title.ja).errors.length > 0;
  const contentInvalid =
    kind === "richtext" ? bodyId.trim() === "" || markdownProblem(bodyId) || markdownProblem(bodyEn) : kind === "link" ? !url.startsWith("https://") : fileId === null;
  const invalid = titleMissing || titleMarkupInvalid || contentInvalid;
  const publishAt = publishDate && publishTime ? zonedToIso(publishDate, publishTime, me.timezone) : null;

  const submit = async () => {
    setSubmitted(true);
    setFormError(null);
    if (invalid) return;
    const titleBlock = fieldsToTextBlock(title);
    const body = kind === "richtext" ? { format: "md", id: bodyId.trim(), en: bodyEn.trim() === "" ? null : bodyEn.trim() } : null;
    try {
      if (material) {
        await update.mutateAsync({
          id: material.id,
          changes: {
            version: material.version,
            title: titleBlock,
            body,
            url: kind === "link" ? url.trim() : null,
            fileId: kind === "file" ? fileId : null,
            publishAt,
            clearPublishAt: publishAt === null,
            sortOrder: material.sortOrder,
          },
        });
      } else {
        await create.mutateAsync({
          kind,
          title: titleBlock,
          body,
          url: kind === "link" ? url.trim() : null,
          fileId: kind === "file" ? fileId : null,
          publishAt,
          sortOrder: 0,
        });
        toast.success(t(materials.created));
      }
      onClose();
    } catch (error) {
      setFormError(errorCode(error));
    }
  };

  return (
    <DialogContent closeLabel={t(common.close)} aria-describedby={undefined} className="max-h-[90dvh] max-w-2xl overflow-y-auto">
      <DialogHeader>
        <DialogTitle>
          <Label value={material ? materials.edit : materials.add} companion="inline" />
        </DialogTitle>
      </DialogHeader>
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
      >
        {!material && (
          <Field>
            <FieldLabel htmlFor="material-kind">{t(materials.kind)}</FieldLabel>
            <Select
              id="material-kind"
              value={kind}
              onChange={(event) => {
                setKind(event.target.value as MaterialKind);
              }}
            >
              <option value="richtext">{t(materials.kindRichtext)}</option>
              <option value="file">{t(materials.kindFile)}</option>
              <option value="link">{t(materials.kindLink)}</option>
            </Select>
          </Field>
        )}

        <TextBlockInput
          id="material-title"
          label={materials.titleField}
          value={title}
          onChange={setTitle}
          errors={{ id: submitted && titleMissing ? "required" : undefined }}
        />

        {kind === "richtext" && (
          <>
            <Field>
              <FieldLabel htmlFor="material-body">{t(materials.body)}</FieldLabel>
              <Textarea
                id="material-body"
                rows={10}
                className="font-ja"
                value={bodyId}
                aria-invalid={submitted && (bodyId.trim() === "" || markdownProblem(bodyId)) ? true : undefined}
                aria-describedby="material-body-hint material-body-error"
                onChange={(event) => {
                  setBodyId(event.target.value);
                }}
              />
              <FieldHint id="material-body-hint">{t(materials.bodyHint)}</FieldHint>
              {submitted && (bodyId.trim() === "" || markdownProblem(bodyId)) && (
                <FieldError id="material-body-error">{t(bodyId.trim() === "" ? fieldErrors.required : fieldErrors.invalid)}</FieldError>
              )}
            </Field>
            <Field>
              <FieldLabel htmlFor="material-body-en">
                {t(materials.bodyEnglish)} <span className="font-normal text-ink-muted">({t(common.optional)})</span>
              </FieldLabel>
              <Textarea
                id="material-body-en"
                rows={5}
                value={bodyEn}
                onChange={(event) => {
                  setBodyEn(event.target.value);
                }}
              />
            </Field>
          </>
        )}

        {kind === "link" && (
          <Field>
            <FieldLabel htmlFor="material-url">{t(materials.url)}</FieldLabel>
            <Input
              id="material-url"
              type="url"
              inputMode="url"
              value={url}
              aria-invalid={submitted && !url.startsWith("https://") ? true : undefined}
              aria-describedby="material-url-error"
              onChange={(event) => {
                setUrl(event.target.value);
              }}
            />
            {submitted && !url.startsWith("https://") && <FieldError id="material-url-error">{t(fieldErrors.invalid)}</FieldError>}
          </Field>
        )}

        {kind === "file" && (
          <Field>
            <FieldLabel htmlFor="material-file">{t(materials.file)}</FieldLabel>
            {material?.file && fileId === material.file.id ? (
              <div className="flex items-center justify-between gap-3 rounded-lg border border-line px-4 py-3 text-body-sm">
                <span className="truncate">{material.file.originalName}</span>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => {
                    setFileId(null);
                  }}
                >
                  {t(common.edit)}
                </Button>
              </div>
            ) : (
              <UploadField
                id="material-file"
                purpose="material"
                onUploaded={(id) => {
                  setFileId(id);
                }}
                onCleared={() => {
                  setFileId(null);
                }}
              />
            )}
            {submitted && fileId === null && <FieldError id="material-file-error">{t(fieldErrors.required)}</FieldError>}
          </Field>
        )}

        <Field>
          <FieldLabel htmlFor="material-publish-date">
            {t(materials.publishAt)} <span className="font-normal text-ink-muted">({t(common.optional)})</span>
          </FieldLabel>
          <div className="flex gap-2">
            <Input
              id="material-publish-date"
              type="date"
              value={publishDate}
              aria-describedby="material-publish-hint"
              onChange={(event) => {
                setPublishDate(event.target.value);
              }}
            />
            <Input
              type="time"
              aria-label={t(materials.publishAt)}
              value={publishTime}
              onChange={(event) => {
                setPublishTime(event.target.value);
              }}
            />
          </div>
          <FieldHint id="material-publish-hint">{t(materials.publishAtHint)}</FieldHint>
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
          <Button variant="primary" type="submit" loading={create.isPending || update.isPending}>
            {t(common.save)}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  );
}
