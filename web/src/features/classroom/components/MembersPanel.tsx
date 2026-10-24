import { CircleAlert, Plus, UserMinus, Users } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldError, FieldHint, FieldLabel, Input, Textarea } from "@/components/ui/form";
import { Alert, Badge, Card, EmptyState, Skeleton } from "@/components/ui/misc";
import { members } from "@/i18n/labels/classroom";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { fieldErrors } from "@/i18n/labels/identity";
import { usePrefs, useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { formatDate } from "@/lib/datetime";
import {
  useAddStudents,
  useAddTeacher,
  useClassStudents,
  useClassTeachers,
  useRemoveStudent,
  useRemoveTeacher,
  type ClassDto,
  type ClassStudentDto,
  type TeacherDto,
} from "../api/classroom";

/** Usernames typed one per line or separated by commas, without duplicates. */
function parseUsernames(text: string): string[] {
  return [
    ...new Set(
      text
        .split(/[\n,;]/u)
        .map((name) => name.trim())
        .filter(Boolean),
    ),
  ];
}

/** "Anggota" tab: students (BR-AKSES-01 membership) and teachers (BR-AKSES-03) of a class. */
export function MembersPanel({ classInfo }: { classInfo: ClassDto }) {
  const t = useT();
  const me = useCurrentUser();
  const readOnly = classInfo.status === "archived";
  const isPrimary = classInfo.teachers.some((teacher) => teacher.id === me.id && teacher.isPrimary);
  const canManageTeachers = !readOnly && (me.role === "admin" || isPrimary);

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <StudentsSection classInfo={classInfo} readOnly={readOnly} />
      <section className="flex flex-col gap-3" aria-labelledby="teachers-heading">
        <h2 id="teachers-heading" className="text-body font-bold text-ink">
          <Label value={members.teachers} companion="inline" companionClassName="text-body-sm font-medium" />
        </h2>
        <TeachersSection classInfo={classInfo} canManage={canManageTeachers} />
        {!canManageTeachers && !readOnly && (
          <p className="text-caption text-ink-muted">{t(members.onlyPrimary)}</p>
        )}
      </section>
    </div>
  );
}

function StudentsSection({ classInfo, readOnly }: { classInfo: ClassDto; readOnly: boolean }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const students = useClassStudents(classInfo.id);
  const remove = useRemoveStudent(classInfo.id);
  const [adding, setAdding] = useState(false);
  const [removing, setRemoving] = useState<ClassStudentDto | null>(null);
  const current = (students.data ?? []).filter((student) => student.status === "active");
  const former = (students.data ?? []).filter((student) => student.status !== "active");

  return (
    <section className="flex flex-col gap-3" aria-labelledby="students-heading">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="students-heading" className="text-body font-bold text-ink">
          <Label value={members.students} companion="inline" companionClassName="text-body-sm font-medium" />
        </h2>
        {!readOnly && (
          <Button
            variant="primary"
            size="sm"
            onClick={() => {
              setAdding(true);
            }}
          >
            <Plus aria-hidden="true" />
            {t(members.addStudents)}
          </Button>
        )}
      </div>
      {students.isPending && <Skeleton className="h-32" />}
      {students.data && current.length === 0 && (
        <EmptyState icon={Users} title={<Label value={members.empty} />} />
      )}
      {current.length > 0 && (
        <ul className="flex flex-col divide-y divide-line rounded-lg border border-line bg-surface">
          {current.map((student) => (
            <li key={student.studentId} className="flex items-center gap-3 px-4 py-3">
              <span className="flex min-w-0 flex-1 flex-col">
                <span className="truncate font-semibold text-ink">{student.displayName}</span>
                <span className="truncate text-caption text-ink-muted">
                  @{student.username} ·{" "}
                  {t(members.joined, { date: formatDate(student.joinedAt, uiLocale, me.timezone) })}
                </span>
              </span>
              {!student.isActive && <Badge tone="warning">{t(members.accountInactive)}</Badge>}
              {!readOnly && (
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={t(members.removeTitle, { name: student.displayName })}
                  onClick={() => {
                    setRemoving(student);
                  }}
                >
                  <UserMinus aria-hidden="true" />
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
      {former.length > 0 && (
        <details className="rounded-lg border border-line bg-surface px-4 py-3">
          <summary className="cursor-pointer text-body-sm font-semibold text-ink-muted">
            {t(members.statusRemoved)} ({former.length})
          </summary>
          <ul className="mt-2 flex flex-col gap-1">
            {former.map((student) => (
              <li key={student.studentId} className="text-body-sm text-ink-muted">
                {student.displayName} · @{student.username}
              </li>
            ))}
          </ul>
        </details>
      )}
      <AddStudentsDialog classId={classInfo.id} open={adding} onOpenChange={setAdding} />
      <ConfirmDialog
        open={removing !== null}
        onOpenChange={(open) => {
          if (!open) setRemoving(null);
        }}
        title={t(members.removeTitle, { name: removing?.displayName ?? "" })}
        description={t(members.removeBody)}
        confirmLabel={t(members.removeConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        destructive
        busy={remove.isPending}
        onConfirm={() => {
          if (!removing) return;
          remove.mutate(removing.studentId, {
            onSuccess: () => {
              toast.success(t(members.removed));
              setRemoving(null);
            },
            onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
          });
        }}
      />
    </section>
  );
}

function AddStudentsDialog({
  classId,
  open,
  onOpenChange,
}: {
  classId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useT();
  const add = useAddStudents(classId);
  const [text, setText] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const [notFound, setNotFound] = useState<string[]>([]);
  const [formError, setFormError] = useState<string | null>(null);
  const usernames = parseUsernames(text);
  const invalid = usernames.length === 0 || usernames.length > 200;

  const close = (next: boolean) => {
    if (!next) {
      setText("");
      setSubmitted(false);
      setNotFound([]);
      setFormError(null);
    }
    onOpenChange(next);
  };

  const submit = async () => {
    setSubmitted(true);
    setFormError(null);
    if (invalid) return;
    try {
      const result = await add.mutateAsync({ usernames });
      if (result.added.length > 0) toast.success(t(members.added, { n: result.added.length }));
      if (result.notFound.length === 0) {
        close(false);
      } else {
        // Keep the names that failed so the teacher can fix typos and try again.
        setNotFound(result.notFound);
        setText(result.notFound.join("\n"));
        setSubmitted(false);
      }
    } catch (error) {
      setFormError(errorCode(error));
    }
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent closeLabel={t(common.close)} aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>
            <Label value={members.addStudents} companion="inline" />
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
            <FieldLabel htmlFor="student-usernames">{t(members.usernames)}</FieldLabel>
            <Textarea
              id="student-usernames"
              value={text}
              rows={6}
              autoCapitalize="none"
              spellCheck={false}
              aria-invalid={submitted && invalid ? true : undefined}
              aria-describedby="student-usernames-hint student-usernames-error"
              onChange={(event) => {
                setText(event.target.value);
              }}
            />
            <FieldHint id="student-usernames-hint">{t(members.usernamesHint)}</FieldHint>
            {submitted && invalid && (
              <FieldError id="student-usernames-error">{t(fieldErrors.required)}</FieldError>
            )}
          </Field>
          {notFound.length > 0 && (
            <Alert tone="warning" icon={CircleAlert}>
              {t(members.notFound, { names: notFound.join(", ") })}
            </Alert>
          )}
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
            <Button variant="primary" type="submit" loading={add.isPending}>
              {t(members.addStudents)}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function TeachersSection({ classInfo, canManage }: { classInfo: ClassDto; canManage: boolean }) {
  const t = useT();
  const teachers = useClassTeachers(classInfo.id);
  const remove = useRemoveTeacher(classInfo.id);
  const [adding, setAdding] = useState(false);
  const [removing, setRemoving] = useState<TeacherDto | null>(null);
  const list = teachers.data ?? classInfo.teachers;

  return (
    <>
      <Card className="flex flex-col gap-2 p-4">
        <ul className="flex flex-col gap-2">
          {list.map((teacher) => (
            <li key={teacher.id} className="flex items-center gap-2">
              <span className="min-w-0 flex-1 truncate text-body text-ink">{teacher.displayName}</span>
              {teacher.isPrimary && <Badge tone="solid">{t(members.primary)}</Badge>}
              {canManage && list.length > 1 && (
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`${t(members.removeTeacher)} ${teacher.displayName}`}
                  onClick={() => {
                    setRemoving(teacher);
                  }}
                >
                  <UserMinus aria-hidden="true" />
                </Button>
              )}
            </li>
          ))}
        </ul>
        {canManage && (
          <Button
            variant="outline"
            size="sm"
            className="self-start"
            onClick={() => {
              setAdding(true);
            }}
          >
            <Plus aria-hidden="true" />
            {t(members.addTeacher)}
          </Button>
        )}
      </Card>
      {canManage && <AddTeacherDialog classId={classInfo.id} open={adding} onOpenChange={setAdding} />}
      <ConfirmDialog
        open={removing !== null}
        onOpenChange={(open) => {
          if (!open) setRemoving(null);
        }}
        title={t(members.removeTeacherTitle, { name: removing?.displayName ?? "" })}
        description={t(members.removeTeacherBody)}
        confirmLabel={t(members.removeConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        destructive
        busy={remove.isPending}
        onConfirm={() => {
          if (!removing) return;
          remove.mutate(removing.id, {
            onSuccess: () => {
              toast.success(t(members.removed));
              setRemoving(null);
            },
            onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
          });
        }}
      />
    </>
  );
}

function AddTeacherDialog({
  classId,
  open,
  onOpenChange,
}: {
  classId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useT();
  const add = useAddTeacher(classId);
  const [username, setUsername] = useState("");
  const [isPrimary, setIsPrimary] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const invalid = username.trim().length === 0;

  const close = (next: boolean) => {
    if (!next) {
      setUsername("");
      setIsPrimary(false);
      setSubmitted(false);
      setFormError(null);
    }
    onOpenChange(next);
  };

  const submit = async () => {
    setSubmitted(true);
    setFormError(null);
    if (invalid) return;
    try {
      await add.mutateAsync({ username: username.trim(), isPrimary });
      toast.success(t(members.teacherAdded));
      close(false);
    } catch (error) {
      setFormError(errorCode(error));
    }
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent closeLabel={t(common.close)} aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>
            <Label value={members.addTeacher} companion="inline" />
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
            <FieldLabel htmlFor="teacher-username">{t(members.teacherUsername)}</FieldLabel>
            <Input
              id="teacher-username"
              value={username}
              autoCapitalize="none"
              spellCheck={false}
              aria-invalid={submitted && invalid ? true : undefined}
              aria-describedby="teacher-username-error"
              onChange={(event) => {
                setUsername(event.target.value);
              }}
            />
            {submitted && invalid && (
              <FieldError id="teacher-username-error">{t(fieldErrors.required)}</FieldError>
            )}
          </Field>
          <label className="flex items-center gap-2 text-body text-ink">
            <input
              type="checkbox"
              className="size-4 accent-sumi-900"
              checked={isPrimary}
              onChange={(event) => {
                setIsPrimary(event.target.checked);
              }}
            />
            {t(members.makePrimary)}
          </label>
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
            <Button variant="primary" type="submit" loading={add.isPending}>
              {t(members.addTeacher)}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
