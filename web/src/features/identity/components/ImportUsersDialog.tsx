import { CircleAlert, CircleCheck, Download, FileWarning, Info, TriangleAlert } from "lucide-react";
import { useState } from "react";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldLabel, Input } from "@/components/ui/form";
import { Alert } from "@/components/ui/misc";
import { Table, TD, TH, THead, TR } from "@/components/ui/table";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { importErrorLabel, importUsers } from "@/i18n/labels/identity";
import { useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { useImportUsers, type ImportUsersResponse } from "../api/identity";

/**
 * CSV import in three steps (docs/07 §11 flow 9): check (dry run) → create → download the one-time password file.
 */
export function ImportUsersDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useT();
  const importUsersMutation = useImportUsers();
  const [file, setFile] = useState<File | null>(null);
  const [check, setCheck] = useState<ImportUsersResponse | null>(null);
  const [result, setResult] = useState<ImportUsersResponse | null>(null);
  const [requestError, setRequestError] = useState<string | null>(null);

  const close = (next: boolean) => {
    if (!next) {
      setFile(null);
      setCheck(null);
      setResult(null);
      setRequestError(null);
    }
    onOpenChange(next);
  };

  const run = async (dryRun: boolean) => {
    if (!file) return;
    setRequestError(null);
    try {
      const response = await importUsersMutation.mutateAsync({ file, dryRun });
      if (dryRun) setCheck(response);
      else setResult(response);
    } catch (error) {
      setRequestError(errorCode(error));
    }
  };

  const downloadPasswords = (csv: string) => {
    const url = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = "nomax-password-sementara.csv";
    link.click();
    URL.revokeObjectURL(url);
  };

  const errors = check?.errors ?? [];

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent closeLabel={t(common.close)} className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            <Label value={importUsers.title} companion="inline" />
          </DialogTitle>
          <DialogDescription>{t(importUsers.help)}</DialogDescription>
        </DialogHeader>

        {result ? (
          <div className="flex flex-col gap-3">
            <Alert tone="success" icon={CircleCheck}>
              {t(importUsers.done, { n: result.created.length })}
            </Alert>
            {result.failure && (
              <Alert tone="danger" icon={CircleAlert}>
                <span>
                  {t(importUsers.stopped, { row: result.failure.row, username: result.failure.username })}
                </span>
                <span>{t(errorLabel(result.failure.code))}</span>
              </Alert>
            )}
            {result.passwordsCsv && (
              <>
                <Alert tone="warning" icon={TriangleAlert}>
                  {t(importUsers.downloadOnce)}
                </Alert>
                <Button
                  variant="primary"
                  className="self-start"
                  onClick={() => {
                    downloadPasswords(result.passwordsCsv ?? "");
                  }}
                >
                  <Download aria-hidden="true" />
                  {t(importUsers.downloadPasswords)}
                </Button>
              </>
            )}
            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => {
                  close(false);
                }}
              >
                {t(common.close)}
              </Button>
            </DialogFooter>
          </div>
        ) : (
          <div className="flex flex-col gap-4">
            <Field>
              <FieldLabel htmlFor="csv-file">{t(importUsers.chooseFile)}</FieldLabel>
              <Input
                id="csv-file"
                type="file"
                accept=".csv,text/csv"
                className="h-auto py-2"
                onChange={(event) => {
                  setFile(event.target.files?.[0] ?? null);
                  setCheck(null);
                }}
              />
            </Field>

            {check && (
              <Alert
                tone={errors.length > 0 ? "danger" : "info"}
                icon={errors.length > 0 ? FileWarning : Info}
              >
                {t(importUsers.summary, {
                  n: check.totalRows,
                  create: check.toCreate.length,
                  skip: check.skipped.length,
                })}
              </Alert>
            )}

            {errors.length > 0 && (
              <div className="flex flex-col gap-2">
                <p className="text-body-sm font-semibold text-danger">{t(importUsers.errorsTitle)}</p>
                <Table>
                  <THead>
                    <TR>
                      <TH>{t(importUsers.row)}</TH>
                      <TH>{t(importUsers.column)}</TH>
                      <TH>{t(importUsers.problem)}</TH>
                    </TR>
                  </THead>
                  <tbody>
                    {errors.map((error) => (
                      <TR key={`${error.row}-${error.column}-${error.code}`}>
                        <TD className="tabular-nums">{error.row}</TD>
                        <TD>
                          <code>{error.column}</code>
                        </TD>
                        <TD>{t(importErrorLabel(error.code))}</TD>
                      </TR>
                    ))}
                  </tbody>
                </Table>
              </div>
            )}

            {check && errors.length === 0 && check.toCreate.length === 0 && (
              <Alert tone="info" icon={Info}>
                {t(importUsers.nothingNew)}
              </Alert>
            )}

            {requestError && (
              <Alert tone="danger" icon={CircleAlert}>
                {t(errorLabel(requestError))}
              </Alert>
            )}

            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => {
                  close(false);
                }}
              >
                {t(common.cancel)}
              </Button>
              {check && errors.length === 0 && check.toCreate.length > 0 ? (
                <Button
                  variant="primary"
                  loading={importUsersMutation.isPending}
                  onClick={() => void run(false)}
                >
                  {t(importUsers.create, { n: check.toCreate.length })}
                </Button>
              ) : (
                <Button
                  variant="primary"
                  disabled={!file}
                  loading={importUsersMutation.isPending}
                  onClick={() => void run(true)}
                >
                  {t(importUsers.check)}
                </Button>
              )}
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
