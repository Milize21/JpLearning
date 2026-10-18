import { CircleAlert, CircleCheck, Download } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldHint, FieldLabel, Input, Select } from "@/components/ui/form";
import { Alert } from "@/components/ui/misc";
import { Table, TD, TH, THead, TR } from "@/components/ui/table";
import { common } from "@/i18n/labels/common";
import { reference, referenceImportErrors, structure } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { useImportReference, type ImportReferenceResponse, type LevelDto, type ReferenceType } from "../api/content";

/** Columns of each import format (ReferenceImport.ColumnsOf on the server); the first ones are required. */
export const referenceColumns: Record<ReferenceType, string> = {
  kana: "char, script, romaji, row_key, variant, sort_order, chapter",
  kanji: "character, meanings_id, onyomi, kunyomi, meanings_en, stroke_count, kanjivg_code, chapter",
  vocab: "word, reading, meanings_id, meanings_en, pos, tags, jmdict_seq, chapter",
  grammar: "pattern, title_id, title_en, title_ja, explanation_id, explanation_en, tags, chapter",
  sentences: "text_id, text_ja, text_en, tags, grammar_pattern, tatoeba_id, chapter",
};

/**
 * Reference import of docs/05 §5: every row is validated first (dry run), errors are listed per cell, and only a clean
 * file is imported. Source and license are mandatory (AGENTS.md §12).
 */
export function ImportReferenceDialog({
  open,
  onOpenChange,
  type,
  typeLabel,
  level,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  type: ReferenceType;
  typeLabel: string;
  level: LevelDto | undefined;
}) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const importReference = useImportReference();
  const [file, setFile] = useState<File | null>(null);
  const [source, setSource] = useState("");
  const [license, setLicense] = useState("");
  const [bookId, setBookId] = useState("");
  const [result, setResult] = useState<ImportReferenceResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const close = (next: boolean) => {
    if (!next) {
      setFile(null);
      setResult(null);
      setError(null);
    }
    onOpenChange(next);
  };

  const run = async (dryRun: boolean) => {
    if (!file || !level) return;
    setError(null);
    try {
      const response = await importReference.mutateAsync({
        type,
        file,
        source,
        license,
        levelId: level.id,
        bookId: bookId || undefined,
        dryRun,
      });
      setResult(response);
      if (!dryRun) toast.success(t(reference.imported, { n: response.created }));
    } catch (failure) {
      setError(errorCode(failure));
    }
  };

  const ready = file !== null && source.trim() !== "" && license.trim() !== "" && level !== undefined;
  const errors = result?.errors ?? [];
  const importedNow = result !== null && !result.dryRun;

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent closeLabel={t(common.close)} aria-describedby={undefined} className="max-h-[90dvh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            <Label value={reference.importTitle} vars={{ type: typeLabel }} companion="inline" />
          </DialogTitle>
        </DialogHeader>
        <div className="flex flex-col gap-4">
          <p className="text-body-sm text-ink-muted">{t(reference.importHelp)}</p>
          <p className="rounded-md bg-canvas-subtle px-3 py-2 font-mono text-caption text-ink">
            {t(reference.columns, { columns: referenceColumns[type] })}
          </p>
          <Field>
            <FieldLabel htmlFor="import-file">{t(reference.file)}</FieldLabel>
            <Input
              id="import-file"
              type="file"
              accept=".csv,.json,text/csv,application/json"
              onChange={(event) => {
                setFile(event.target.files?.[0] ?? null);
                setResult(null);
              }}
            />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field>
              <FieldLabel htmlFor="import-source">{t(reference.source)}</FieldLabel>
              <Input
                id="import-source"
                value={source}
                maxLength={200}
                onChange={(event) => {
                  setSource(event.target.value);
                }}
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="import-license">{t(reference.license)}</FieldLabel>
              <Input
                id="import-license"
                value={license}
                maxLength={100}
                aria-describedby="import-license-hint"
                onChange={(event) => {
                  setLicense(event.target.value);
                }}
              />
              <FieldHint id="import-license-hint">{t(reference.licenseHint)}</FieldHint>
            </Field>
          </div>
          <Field>
            <FieldLabel htmlFor="import-book">
              {t(structure.book)} <span className="font-normal text-ink-muted">({t(common.optional)})</span>
            </FieldLabel>
            <Select
              id="import-book"
              value={bookId}
              onChange={(event) => {
                setBookId(event.target.value);
                setResult(null);
              }}
            >
              <option value="">{t(reference.noChapter)}</option>
              {level?.books.map((book) => (
                <option key={book.id} value={book.id}>
                  {textIn(book.title, uiLocale)}
                </option>
              ))}
            </Select>
          </Field>

          {error && (
            <Alert tone="danger" icon={CircleAlert}>
              {t(errorLabel(error))}
            </Alert>
          )}

          {result && errors.length === 0 && (
            <Alert tone="success" icon={CircleCheck}>
              {importedNow
                ? t(reference.imported, { n: result.created })
                : t(reference.summary, { n: result.totalRows, create: result.toCreate, skip: result.skipped })}
            </Alert>
          )}

          {errors.length > 0 && (
            <div className="flex flex-col gap-2">
              <Alert tone="danger" icon={CircleAlert}>
                {t(reference.errorsTitle)}
              </Alert>
              <Table>
                <THead>
                  <TR>
                    <TH>{t(reference.row)}</TH>
                    <TH>{t(reference.column)}</TH>
                    <TH>{t(reference.problem)}</TH>
                  </TR>
                </THead>
                <tbody>
                  {errors.slice(0, 100).map((cell, index) => (
                    <TR key={index}>
                      <TD>{cell.row}</TD>
                      <TD className="font-mono">{cell.column || t(reference.wholeFile)}</TD>
                      <TD>
                        {t(
                          referenceImportErrors[cell.code as keyof typeof referenceImportErrors] ??
                            referenceImportErrors.invalid_format,
                        )}
                      </TD>
                    </TR>
                  ))}
                </tbody>
              </Table>
            </div>
          )}
        </div>
        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => {
              close(false);
            }}
          >
            {t(importedNow ? common.close : common.cancel)}
          </Button>
          {!importedNow &&
            (result && errors.length === 0 && result.toCreate > 0 ? (
              <Button variant="primary" loading={importReference.isPending} onClick={() => void run(false)}>
                <Download aria-hidden="true" />
                {t(reference.createN, { n: result.toCreate })}
              </Button>
            ) : (
              <Button variant="primary" disabled={!ready} loading={importReference.isPending} onClick={() => void run(true)}>
                {t(reference.check)}
              </Button>
            ))}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
