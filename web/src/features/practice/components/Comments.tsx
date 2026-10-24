import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Button } from "@/components/ui/button";
import { Field, FieldHint, FieldLabel, Textarea } from "@/components/ui/form";
import { Skeleton } from "@/components/ui/misc";
import { errorLabel } from "@/i18n/labels/errors";
import { progress } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/datetime";
import { useCreateComment, type Comment } from "../api/practice";

const MAX = 2000;

/** Comments without a grade (BR-DRILL-01), newest first. */
export function CommentList({ comments, loading }: { comments: Comment[] | undefined; loading: boolean }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  if (loading) return <Skeleton className="h-16" />;
  if (!comments || comments.length === 0)
    return <p className="text-body-sm text-ink-muted">{t(progress.noComments)}</p>;
  return (
    <ul className="flex flex-col gap-2">
      {comments.map((comment) => (
        <li key={comment.id} className="rounded-lg border border-line bg-surface p-3">
          <p className="whitespace-pre-line text-body text-ink">{comment.text}</p>
          <p className="mt-1 text-caption text-ink-muted">
            {t(progress.fromTeacher, { name: comment.authorName })} ·{" "}
            {formatDateTime(comment.createdAt, uiLocale, me.timezone)}
          </p>
        </li>
      ))}
    </ul>
  );
}

/** Form for a teacher's comment on a drill session or on the student's progress in a class. */
export function CommentForm({
  studentId,
  targetType,
  targetId,
  label,
}: {
  studentId: string;
  targetType: "drill_session" | "progress";
  targetId: string;
  label?: string;
}) {
  const t = useT();
  const create = useCreateComment();
  const [text, setText] = useState("");
  const id = `comment-${targetType}-${targetId}`;

  return (
    <form
      noValidate
      className="flex flex-col gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (text.trim() === "") return;
        create.mutate(
          { studentId, targetType, targetId, text: text.trim() },
          {
            onSuccess: () => {
              toast.success(t(progress.sent));
              setText("");
            },
            onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
          },
        );
      }}
    >
      <Field>
        <FieldLabel htmlFor={id}>{label ?? t(progress.commentLabel)}</FieldLabel>
        <Textarea
          id={id}
          value={text}
          maxLength={MAX}
          rows={3}
          aria-describedby={`${id}-count`}
          onChange={(event) => {
            setText(event.target.value);
          }}
        />
        <FieldHint id={`${id}-count`}>
          {text.length}/{MAX}
        </FieldHint>
      </Field>
      <Button
        type="submit"
        variant="secondary"
        className="self-end"
        loading={create.isPending}
        disabled={text.trim() === ""}
      >
        {t(progress.send)}
      </Button>
    </form>
  );
}
