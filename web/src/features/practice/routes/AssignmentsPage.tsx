import {
  ChevronLeft,
  ChevronRight,
  CircleAlert,
  Download,
  FileText,
  NotebookPen,
  Trash2,
} from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "react-router";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { ContentText } from "@/components/japanese/ContentText";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Field, FieldLabel, Textarea } from "@/components/ui/form";
import { Alert, Badge, Card, EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { RichTextView } from "@/features/content/components/RichTextView";
import { UploadField } from "@/features/content/components/UploadField";
import { assignments as labels } from "@/i18n/labels/assessment";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/datetime";
import {
  useDownloadSubmissionFile,
  useMyAssignments,
  useSubmitAssignment,
  type StudentAssignment,
} from "../api/assessment";

function SubmissionStatus({ item }: { item: StudentAssignment }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const submission = item.submission;
  if (!submission) {
    return (
      <Badge tone={new Date(item.dueAt) < new Date() ? "danger" : "neutral"}>{t(labels.notSubmitted)}</Badge>
    );
  }
  return (
    <span className="flex flex-wrap gap-1">
      <Badge tone="success">
        {t(labels.submitted, { date: formatDateTime(submission.submittedAt, uiLocale, me.timezone) })}
      </Badge>
      {submission.isLate && <Badge tone="warning">{t(labels.late)}</Badge>}
      {submission.gradePct !== null && (
        <Badge tone="solid">{t(labels.gradedLabel, { pct: submission.gradePct })}</Badge>
      )}
    </span>
  );
}

/** /murid/tugas — published assignments of the student's classes with their status (docs/07 §11 flow 5). */
export function AssignmentListPage() {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const list = useMyAssignments();

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={labels.title} companionClassName="text-body font-medium" />}
        description={t(labels.description)}
      />
      {list.isPending && <Skeleton className="h-40" />}
      {list.data?.length === 0 && <EmptyState icon={NotebookPen} title={<Label value={labels.empty} />} />}
      <ul className="flex flex-col gap-3">
        {list.data?.map((item) => (
          <li key={item.id}>
            <Link
              to={`/murid/tugas/${item.id}`}
              className="flex items-center gap-3 rounded-lg border border-line bg-surface p-4 hover:shadow-sm focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
            >
              <span className="flex min-w-0 flex-1 flex-col gap-1">
                <span className="text-caption text-ink-muted">{item.className}</span>
                <ContentText value={item.title} className="font-semibold text-ink" />
                <span className="text-body-sm text-ink-muted">
                  {t(labels.due, { date: formatDateTime(item.dueAt, uiLocale, me.timezone) })}
                </span>
                <SubmissionStatus item={item} />
              </span>
              <ChevronRight className="size-5 shrink-0 text-ink-subtle" aria-hidden="true" />
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** /murid/tugas/:assignmentId — instructions, upload (resumable) and/or text, submit, then grade and feedback. */
export function AssignmentPage() {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const { assignmentId = "" } = useParams();
  const list = useMyAssignments();
  const item = list.data?.find((entry) => entry.id === assignmentId);

  const back = (
    <Link
      to="/murid/tugas"
      className="inline-flex items-center gap-1 self-start rounded-sm text-body-sm text-ink-muted hover:text-ink focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
    >
      <ChevronLeft className="size-4" aria-hidden="true" />
      {t(labels.backToList)}
    </Link>
  );

  if (list.isPending) return <Skeleton className="h-64" />;
  if (!item) {
    return (
      <div className="flex flex-col gap-6">
        {back}
        <EmptyState icon={CircleAlert} title={t(errorLabel("not_found"))} />
      </div>
    );
  }

  const submission = item.submission;
  const canSend = !submission || (item.allowResubmit && submission.gradePct === null);

  return (
    <div className="flex flex-col gap-6">
      {back}
      <div className="flex flex-col gap-2">
        <span className="text-caption text-ink-muted">{item.className}</span>
        <h1 className="text-h2 font-bold text-ink sm:text-h1">
          <ContentText value={item.title} companionClassName="text-body font-medium" />
        </h1>
        <p className="text-body-sm text-ink-muted">
          {t(labels.due, { date: formatDateTime(item.dueAt, uiLocale, me.timezone) })}
        </p>
        <SubmissionStatus item={item} />
      </div>
      <Card>
        <RichTextView value={item.instructions} />
      </Card>
      {submission?.gradePct !== null && submission?.gradePct !== undefined && (
        <Card className="flex flex-col gap-2">
          <span className="text-h2 font-bold text-ink tabular-nums">{submission.gradePct}</span>
          {submission.feedback && (
            <>
              <span className="text-body-sm font-semibold text-ink-muted">{t(labels.feedback)}</span>
              <p className="whitespace-pre-line text-body text-ink">{submission.feedback}</p>
            </>
          )}
        </Card>
      )}
      {submission && (
        <SubmittedFiles submissionId={submission.id} files={submission.files} text={submission.text} />
      )}
      {canSend && <SubmitForm key={submission?.id ?? "new"} item={item} />}
    </div>
  );
}

function SubmittedFiles({
  submissionId,
  files,
  text,
}: {
  submissionId: string;
  files: { id: string; name: string; sizeBytes: number }[];
  text: string | null;
}) {
  const t = useT();
  const download = useDownloadSubmissionFile();
  if (!text && files.length === 0) return null;
  return (
    <Card className="flex flex-col gap-3">
      {text && <p className="whitespace-pre-line text-body text-ink">{text}</p>}
      <ul className="flex flex-col gap-2">
        {files.map((file) => (
          <li key={file.id} className="flex items-center gap-2">
            <FileText className="size-4 text-ink-muted" aria-hidden="true" />
            <span className="min-w-0 flex-1 truncate text-body-sm text-ink">{file.name}</span>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                download.mutate(
                  { submissionId, fileId: file.id },
                  { onError: (error) => toast.error(t(errorLabel(errorCode(error)))) },
                );
              }}
            >
              <Download aria-hidden="true" />
              {file.name}
            </Button>
          </li>
        ))}
      </ul>
    </Card>
  );
}

function SubmitForm({ item }: { item: StudentAssignment }) {
  const t = useT();
  const submit = useSubmitAssignment(item.id);
  const [text, setText] = useState(item.submission?.text ?? "");
  const [files, setFiles] = useState<{ id: string; name: string }[]>(
    item.submission?.files.map((file) => ({ id: file.id, name: file.name })) ?? [],
  );
  const [uploadKey, setUploadKey] = useState(0);
  const [problem, setProblem] = useState<string | null>(null);

  return (
    <Card>
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault();
          setProblem(null);
          if (text.trim() === "" && files.length === 0) {
            setProblem("needContent");
            return;
          }
          submit.mutate(
            { text: text.trim() === "" ? null : text.trim(), fileIds: files.map((file) => file.id) },
            {
              onSuccess: () => toast.success(t(labels.sent)),
              onError: (error) => {
                setProblem(errorCode(error));
              },
            },
          );
        }}
      >
        <Field>
          <FieldLabel htmlFor="submission-text">{t(labels.yourText)}</FieldLabel>
          <Textarea
            id="submission-text"
            rows={6}
            maxLength={20_000}
            value={text}
            onChange={(event) => {
              setText(event.target.value);
            }}
          />
        </Field>
        <Field>
          <FieldLabel htmlFor="submission-file">{t(labels.files)}</FieldLabel>
          <ul className="flex flex-col gap-1">
            {files.map((file) => (
              <li key={file.id} className="flex items-center gap-2 text-body-sm text-ink">
                <FileText className="size-4 text-ink-muted" aria-hidden="true" />
                <span className="min-w-0 flex-1 truncate">{file.name}</span>
                <Button
                  variant="ghost"
                  size="icon"
                  type="button"
                  aria-label={t(labels.removeFile, { name: file.name })}
                  onClick={() => {
                    setFiles(files.filter((entry) => entry.id !== file.id));
                  }}
                >
                  <Trash2 aria-hidden="true" />
                </Button>
              </li>
            ))}
          </ul>
          {files.length < 10 && (
            <UploadField
              key={uploadKey}
              id="submission-file"
              purpose="submission"
              onUploaded={(fileId, name) => {
                setFiles((current) => [...current, { id: fileId, name }]);
                setUploadKey((value) => value + 1);
              }}
            />
          )}
        </Field>
        {problem && (
          <Alert tone="danger" icon={CircleAlert}>
            {problem === "needContent" ? t(labels.needContent) : t(errorLabel(problem))}
          </Alert>
        )}
        <Button variant="primary" type="submit" className="self-end" loading={submit.isPending}>
          {t(item.submission ? labels.resend : labels.send)}
        </Button>
      </form>
    </Card>
  );
}
