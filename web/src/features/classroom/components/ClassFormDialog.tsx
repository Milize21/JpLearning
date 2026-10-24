import { CircleAlert } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { roleHome } from "@/app/roles";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldError, FieldHint, FieldLabel, Input, Select } from "@/components/ui/form";
import { Alert } from "@/components/ui/misc";
import { useLevels } from "@/features/content/api/content";
import { classes } from "@/i18n/labels/classroom";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { fieldErrors } from "@/i18n/labels/identity";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { useCreateClass, type ClassStatus } from "../api/classroom";

/** Create a class; the creator becomes its primary teacher (BR-AKSES-03). */
export function ClassFormDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const navigate = useNavigate();
  const levels = useLevels();
  const create = useCreateClass();
  const [name, setName] = useState("");
  const [levelId, setLevelId] = useState("");
  const [bookId, setBookId] = useState("");
  const [status, setStatus] = useState<ClassStatus>("active");
  const [submitted, setSubmitted] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const level = levels.data?.find((item) => item.id === levelId);
  const nameInvalid = name.trim().length === 0 || name.trim().length > 100;

  const close = (next: boolean) => {
    if (!next) {
      setName("");
      setLevelId("");
      setBookId("");
      setSubmitted(false);
      setFormError(null);
    }
    onOpenChange(next);
  };

  const submit = async () => {
    setSubmitted(true);
    if (nameInvalid || !levelId || !bookId) return;
    try {
      const created = await create.mutateAsync({ name: name.trim(), levelId, bookId, status });
      toast.success(t(classes.created));
      close(false);
      void navigate(`${roleHome[me.role]}/kelas/${created.id}/bab`);
    } catch (error) {
      setFormError(errorCode(error));
    }
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent closeLabel={t(common.close)} aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>
            <Label value={classes.create} companion="inline" />
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
          <Field>
            <FieldLabel htmlFor="class-name">{t(classes.name)}</FieldLabel>
            <Input
              id="class-name"
              value={name}
              maxLength={100}
              aria-invalid={submitted && nameInvalid ? true : undefined}
              aria-describedby="class-name-hint class-name-error"
              onChange={(event) => {
                setName(event.target.value);
              }}
            />
            <FieldHint id="class-name-hint">{t(classes.nameHint)}</FieldHint>
            {submitted && nameInvalid && (
              <FieldError id="class-name-error">{t(fieldErrors.required)}</FieldError>
            )}
          </Field>
          <Field>
            <FieldLabel htmlFor="class-level">{t(classes.level)}</FieldLabel>
            <Select
              id="class-level"
              value={levelId}
              aria-invalid={submitted && !levelId ? true : undefined}
              onChange={(event) => {
                setLevelId(event.target.value);
                setBookId("");
              }}
            >
              <option value="" disabled>
                —
              </option>
              {levels.data?.map((item) => (
                <option key={item.id} value={item.id}>
                  {textIn(item.name, uiLocale)}
                </option>
              ))}
            </Select>
          </Field>
          <Field>
            <FieldLabel htmlFor="class-book">{t(classes.book)}</FieldLabel>
            <Select
              id="class-book"
              value={bookId}
              disabled={!level}
              aria-invalid={submitted && !bookId ? true : undefined}
              onChange={(event) => {
                setBookId(event.target.value);
              }}
            >
              <option value="" disabled>
                —
              </option>
              {level?.books.map((book) => (
                <option key={book.id} value={book.id}>
                  {textIn(book.title, uiLocale)}
                </option>
              ))}
            </Select>
          </Field>
          <Field>
            <FieldLabel htmlFor="class-status">{t(classes.status)}</FieldLabel>
            <Select
              id="class-status"
              value={status}
              aria-describedby="class-status-hint"
              onChange={(event) => {
                setStatus(event.target.value as ClassStatus);
              }}
            >
              <option value="active">{t(classes.active)}</option>
              <option value="draft">{t(classes.draft)}</option>
            </Select>
            <FieldHint id="class-status-hint">{t(classes.draftHint)}</FieldHint>
          </Field>
          {formError && (
            <Alert tone="danger" icon={CircleAlert}>
              {t(errorLabel(formError))}
            </Alert>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              type="button"
              onClick={() => {
                close(false);
              }}
            >
              {t(common.cancel)}
            </Button>
            <Button variant="primary" type="submit" loading={create.isPending}>
              {t(classes.create)}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
