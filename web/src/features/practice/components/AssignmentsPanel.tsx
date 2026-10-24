import { Download, NotebookPen, Plus } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { ContentText } from "@/components/japanese/ContentText";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldLabel, Input, Textarea } from "@/components/ui/form";
import { Badge, Card, EmptyState, Skeleton } from "@/components/ui/misc";
import type { ClassDto } from "@/features/classroom/api/classroom";
import { TextBlockInput } from "@/features/content/components/TextBlockInput";
import { assignments as labels, publishLabels } from "@/i18n/labels/assessment";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { fieldsToTextBlock, textBlockToFields, type TextBlockFields } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { dayKey, formatDateTime, isoToZoned, zonedToIso } from "@/lib/datetime";
import {
  useAssignmentStatus,
  useClassAssignments,
  useDownloadSubmissionFile,
  useGradeSubmission,
  useSaveAssignment,
  useSubmissions,
  type AssignmentDto,
  type Submission,
} from "../api/assessment";

/** "Tugas" tab of a class (docs/07 §11 flow 5): assignments, submissions with files, grading with feedback (BR-TUGAS). */
export function AssignmentsPanel({ classInfo }: { classInfo: ClassDto }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const list = useClassAssignments(classInfo.id);
  const status = useAssignmentStatus();
  const [editing, setEditing] = useState<AssignmentDto | null | undefined>(undefined);
  const [viewing, setViewing] = useState<AssignmentDto | null>(null);
  const readOnly = classInfo.status === "archived";

  return (
    <div className="flex flex-col gap-4">
      {!readOnly && (
        <Button
          variant="primary"
          className="self-start"
          onClick={() => {
            setEditing(null);
          }}
        >
          <Plus aria-hidden="true" />
          {t(labels.create)}
        </Button>
      )}
      {list.isPending && <Skeleton className="h-40" />}
      {list.data?.length === 0 && <EmptyState icon={NotebookPen} title={<Label value={labels.empty} />} />}
      <ul className="grid gap-4 md:grid-cols-2">
        {list.data?.map((item) => (
          <li key={item.id}>
            <Card className="flex h-full flex-col gap-3">
              <Badge tone={item.status === "published" ? "success" : "neutral"} className="self-start">
                {t(publishLabels[item.status])}
              </Badge>
              <h3 className="text-h3 font-semibold text-ink">
                <ContentText value={item.title} />
              </h3>
              <p className="text-body-sm text-ink-muted">
                {t(labels.due, { date: formatDateTime(item.dueAt, uiLocale, me.timezone) })}
              </p>
              <p className="text-body-sm text-ink-muted">
                {t(labels.submissionCount, { n: item.submissionCount, graded: item.gradedCount })}
              </p>
              <div className="mt-auto flex flex-wrap gap-2">
                {!readOnly && item.status !== "archived" && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      setEditing(item);
                    }}
                  >
                    {t(common.edit)}
                  </Button>
                )}
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setViewing(item);
                  }}
                >
                  {t(labels.submissions)}
                </Button>
                {!readOnly && item.status !== "archived" && (
                  <Button
                    variant={item.status === "draft" ? "secondary" : "ghost"}
                    size="sm"
                    onClick={() => {
                      status.mutate(
                        { id: item.id, action: item.status === "draft" ? "publish" : "archive" },
                        {
                          onSuccess: () => toast.success(t(publishLabels.done)),
                          onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
                        },
                      );
                    }}
                  >
                    {t(item.status === "draft" ? publishLabels.publish : publishLabels.archive)}
                  </Button>
                )}
              </div>
            </Card>
          </li>
        ))}
      </ul>
      {editing !== undefined && (
        <AssignmentFormDialog
          classId={classInfo.id}
          assignment={editing ?? undefined}
          onClose={() => {
            setEditing(undefined);
          }}
        />
      )}
      {viewing && (
        <SubmissionsDialog
          assignment={viewing}
          onClose={() => {
            setViewing(null);
          }}
        />
      )}
    </div>
  );
}

function AssignmentFormDialog({
  classId,
  assignment,
  onClose,
}: {
  classId: string;
  assignment?: AssignmentDto;
  onClose: () => void;
}) {
  const t = useT();
  const me = useCurrentUser();
  const save = useSaveAssignment(classId);
  // Default due time: a week from now at 23:59 in the teacher's zone, computed once when the form opens.
  const [initialDue] = useState(() =>
    assignment
      ? isoToZoned(assignment.dueAt, me.timezone)
      : { date: dayKey(new Date(Date.now() + 7 * 86_400_000), me.timezone), time: "23:59" },
  );
  const [title, setTitle] = useState<TextBlockFields>(() => textBlockToFields(assignment?.title));
  const [instructions, setInstructions] = useState(assignment?.instructions.id ?? "");
  const [date, setDate] = useState(initialDue.date);
  const [time, setTime] = useState(initialDue.time);
  const [allowResubmit, setAllowResubmit] = useState(assignment?.allowResubmit ?? false);
  const [submitted, setSubmitted] = useState(false);
  const invalid = title.id.trim() === "" || instructions.trim() === "";

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent closeLabel={t(common.close)} aria-describedby={undefined} className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>
            <Label value={assignment ? labels.edit : labels.create} companion="inline" />
          </DialogTitle>
        </DialogHeader>
        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            setSubmitted(true);
            if (invalid) return;
            save.mutate(
              {
                assignment,
                body: {
                  title: fieldsToTextBlock(title),
                  instructions: { format: "md", id: instructions.trim(), en: null },
                  dueAt: zonedToIso(date, time, me.timezone),
                  allowResubmit,
                },
              },
              {
                onSuccess: () => {
                  toast.success(t(common.saved));
                  onClose();
                },
                onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
              },
            );
          }}
        >
          <TextBlockInput
            id="assignment-title"
            label={labels.name}
            value={title}
            errors={submitted && title.id.trim() === "" ? { id: "required" } : undefined}
            onChange={setTitle}
          />
          <Field>
            <FieldLabel htmlFor="assignment-instructions">{t(labels.instructions)}</FieldLabel>
            <Textarea
              id="assignment-instructions"
              rows={6}
              value={instructions}
              aria-invalid={submitted && instructions.trim() === "" ? true : undefined}
              onChange={(event) => {
                setInstructions(event.target.value);
              }}
            />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field>
              <FieldLabel htmlFor="assignment-date">{t(labels.dueDate)}</FieldLabel>
              <Input
                id="assignment-date"
                type="date"
                value={date}
                onChange={(event) => {
                  setDate(event.target.value);
                }}
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="assignment-time">{t(labels.dueTime)}</FieldLabel>
              <Input
                id="assignment-time"
                type="time"
                value={time}
                onChange={(event) => {
                  setTime(event.target.value);
                }}
              />
            </Field>
          </div>
          <label className="flex items-center gap-2 text-body text-ink">
            <input
              type="checkbox"
              className="size-4 accent-sumi-900"
              checked={allowResubmit}
              onChange={(event) => {
                setAllowResubmit(event.target.checked);
              }}
            />
            {t(labels.allowResubmit)}
          </label>
          <DialogFooter>
            <Button variant="outline" type="button" onClick={onClose}>
              {t(common.cancel)}
            </Button>
            <Button variant="primary" type="submit" loading={save.isPending}>
              {t(common.save)}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function SubmissionsDialog({ assignment, onClose }: { assignment: AssignmentDto; onClose: () => void }) {
  const t = useT();
  const submissions = useSubmissions(assignment.id);
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent closeLabel={t(common.close)} aria-describedby={undefined} className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>
            <Label value={labels.submissions} companion="inline" />
          </DialogTitle>
        </DialogHeader>
        {submissions.isPending && <Skeleton className="h-32" />}
        {submissions.data?.length === 0 && (
          <p className="text-body-sm text-ink-muted">{t(labels.noSubmissions)}</p>
        )}
        <ul className="flex flex-col gap-3">
          {submissions.data?.map((submission) => (
            <SubmissionRow key={submission.id} submission={submission} />
          ))}
        </ul>
      </DialogContent>
    </Dialog>
  );
}

function SubmissionRow({ submission }: { submission: Submission }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const download = useDownloadSubmissionFile();
  const grade = useGradeSubmission();
  const [pct, setPct] = useState(submission.gradePct?.toString() ?? "");
  const [feedback, setFeedback] = useState(submission.feedback ?? "");
  const gradeId = `grade-${submission.id}`;
  const feedbackId = `feedback-${submission.id}`;

  return (
    <li className="flex flex-col gap-2 rounded-md border border-line p-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="min-w-0 flex-1 font-semibold text-ink">{submission.studentName}</span>
        <span className="text-caption text-ink-muted">
          {formatDateTime(submission.submittedAt, uiLocale, me.timezone)}
        </span>
        {submission.isLate && <Badge tone="warning">{t(labels.late)}</Badge>}
        {submission.gradePct !== null && <Badge tone="solid">{submission.gradePct}</Badge>}
      </div>
      {submission.text && <p className="whitespace-pre-line text-body-sm text-ink">{submission.text}</p>}
      {submission.files.length > 0 && (
        <ul className="flex flex-wrap gap-2">
          {submission.files.map((file) => (
            <li key={file.id}>
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  download.mutate(
                    { submissionId: submission.id, fileId: file.id },
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
      )}
      <form
        noValidate
        className="grid gap-2 sm:grid-cols-[8rem_1fr_auto] sm:items-end"
        onSubmit={(event) => {
          event.preventDefault();
          const value = Number(pct);
          if (pct === "" || Number.isNaN(value) || value < 0 || value > 100) return;
          grade.mutate(
            { id: submission.id, gradePct: value, feedback: feedback.trim() || null },
            {
              onSuccess: () => toast.success(t(labels.gradeSaved)),
              onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
            },
          );
        }}
      >
        <Field>
          <FieldLabel htmlFor={gradeId}>{t(labels.grade)}</FieldLabel>
          <Input
            id={gradeId}
            type="number"
            min={0}
            max={100}
            step={0.5}
            value={pct}
            onChange={(event) => {
              setPct(event.target.value);
            }}
          />
        </Field>
        <Field>
          <FieldLabel htmlFor={feedbackId}>{t(labels.feedbackField)}</FieldLabel>
          <Input
            id={feedbackId}
            maxLength={5000}
            value={feedback}
            onChange={(event) => {
              setFeedback(event.target.value);
            }}
          />
        </Field>
        <Button type="submit" variant="secondary" loading={grade.isPending}>
          {t(labels.saveGrade)}
        </Button>
      </form>
    </li>
  );
}
