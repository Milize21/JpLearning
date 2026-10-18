import { CircleAlert } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldError, FieldHint, FieldLabel, Input, Select, Textarea } from "@/components/ui/form";
import { Alert } from "@/components/ui/misc";
import { useBook } from "@/features/content/api/content";
import { schedule } from "@/i18n/labels/classroom";
import { common } from "@/i18n/labels/common";
import { structure } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { fieldErrors } from "@/i18n/labels/identity";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { dayKey, isoToZoned, zonedToIso } from "@/lib/datetime";
import { useCreateSession, useUpdateSession, type ClassDto, type SessionDto, type SessionPlatform } from "../api/classroom";

/**
 * Schedule or change a session (BR-JADWAL-01). Date and times are entered in the teacher's own time zone; the server
 * stores UTC and notifies the members.
 */
export function SessionFormDialog({
  classInfo,
  session,
  open,
  onOpenChange,
}: {
  classInfo: ClassDto;
  session?: SessionDto;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      {open && (
        <SessionForm
          key={session?.id ?? "new"}
          classInfo={classInfo}
          session={session}
          onClose={() => {
            onOpenChange(false);
          }}
        />
      )}
    </Dialog>
  );
}

function SessionForm({ classInfo, session, onClose }: { classInfo: ClassDto; session?: SessionDto; onClose: () => void }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const book = useBook(classInfo.bookId);
  const create = useCreateSession(classInfo.id);
  const update = useUpdateSession();
  const start = session ? isoToZoned(session.startsAt, me.timezone) : { date: dayKey(new Date(Date.now() + 86_400_000), me.timezone), time: "19:00" };
  const end = session ? isoToZoned(session.endsAt, me.timezone) : { date: start.date, time: "20:30" };
  const [topic, setTopic] = useState(session?.topic ?? "");
  const [date, setDate] = useState(start.date);
  const [startTime, setStartTime] = useState(start.time);
  const [endTime, setEndTime] = useState(end.time);
  const [platform, setPlatform] = useState<SessionPlatform>(session?.platform ?? "zoom");
  const [meetingUrl, setMeetingUrl] = useState(session?.meetingUrl ?? "");
  const [note, setNote] = useState(session?.note ?? "");
  const [chapterId, setChapterId] = useState(session?.chapterId ?? "");
  const [submitted, setSubmitted] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const startsAt = zonedToIso(date, startTime, me.timezone);
  // A session that ends past midnight ends on the next day.
  const endsSameDay = zonedToIso(date, endTime, me.timezone);
  const endsAt = endsSameDay > startsAt ? endsSameDay : new Date(new Date(endsSameDay).getTime() + 86_400_000).toISOString();
  const durationOk = new Date(endsAt).getTime() - new Date(startsAt).getTime() <= 8 * 3_600_000;
  const topicInvalid = topic.trim().length === 0 || topic.trim().length > 200;
  const urlInvalid = meetingUrl.trim() !== "" && !meetingUrl.trim().startsWith("https://");

  const submit = async () => {
    setSubmitted(true);
    setFormError(null);
    if (topicInvalid || urlInvalid || !durationOk) return;
    try {
      if (session) {
        await update.mutateAsync({
          id: session.id,
          changes: {
            topic: topic.trim(),
            startsAt,
            endsAt,
            platform,
            meetingUrl: meetingUrl.trim(),
            note: note.trim(),
            chapterId: chapterId || null,
            clearChapter: chapterId === "",
          },
        });
        toast.success(t(schedule.updated));
      } else {
        await create.mutateAsync({
          topic: topic.trim(),
          startsAt,
          endsAt,
          platform,
          meetingUrl: meetingUrl.trim() === "" ? null : meetingUrl.trim(),
          note: note.trim() === "" ? null : note.trim(),
          chapterId: chapterId || null,
          timezone: me.timezone,
        });
        toast.success(t(schedule.created));
      }
      onClose();
    } catch (error) {
      setFormError(errorCode(error));
    }
  };

  return (
    <DialogContent closeLabel={t(common.close)} aria-describedby={undefined} className="max-h-[90dvh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>
          <Label value={session ? schedule.edit : schedule.add} companion="inline" />
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
          <FieldLabel htmlFor="session-topic">{t(schedule.topic)}</FieldLabel>
          <Input
            id="session-topic"
            value={topic}
            maxLength={200}
            aria-invalid={submitted && topicInvalid ? true : undefined}
            aria-describedby="session-topic-error"
            onChange={(event) => {
              setTopic(event.target.value);
            }}
          />
          {submitted && topicInvalid && <FieldError id="session-topic-error">{t(fieldErrors.required)}</FieldError>}
        </Field>
        <div className="grid gap-4 sm:grid-cols-3">
          <Field>
            <FieldLabel htmlFor="session-date">{t(schedule.date)}</FieldLabel>
            <Input
              id="session-date"
              type="date"
              value={date}
              onChange={(event) => {
                setDate(event.target.value);
              }}
            />
          </Field>
          <Field>
            <FieldLabel htmlFor="session-start">{t(schedule.startTime)}</FieldLabel>
            <Input
              id="session-start"
              type="time"
              value={startTime}
              onChange={(event) => {
                setStartTime(event.target.value);
              }}
            />
          </Field>
          <Field>
            <FieldLabel htmlFor="session-end">{t(schedule.endTime)}</FieldLabel>
            <Input
              id="session-end"
              type="time"
              value={endTime}
              aria-invalid={submitted && !durationOk ? true : undefined}
              aria-describedby="session-end-error"
              onChange={(event) => {
                setEndTime(event.target.value);
              }}
            />
          </Field>
        </div>
        {submitted && !durationOk && <FieldError id="session-end-error">{t(schedule.endAfterStart)}</FieldError>}
        <Field>
          <FieldLabel htmlFor="session-platform">{t(schedule.platform)}</FieldLabel>
          <Select
            id="session-platform"
            value={platform}
            onChange={(event) => {
              setPlatform(event.target.value as SessionPlatform);
            }}
          >
            <option value="zoom">{t(schedule.zoom)}</option>
            <option value="discord">{t(schedule.discord)}</option>
            <option value="other">{t(schedule.other)}</option>
          </Select>
        </Field>
        <Field>
          <FieldLabel htmlFor="session-url">
            {t(schedule.meetingUrl)} <span className="font-normal text-ink-muted">({t(common.optional)})</span>
          </FieldLabel>
          <Input
            id="session-url"
            type="url"
            inputMode="url"
            value={meetingUrl}
            aria-invalid={submitted && urlInvalid ? true : undefined}
            aria-describedby="session-url-hint session-url-error"
            onChange={(event) => {
              setMeetingUrl(event.target.value);
            }}
          />
          <FieldHint id="session-url-hint">{t(schedule.meetingUrlHint)}</FieldHint>
          {submitted && urlInvalid && <FieldError id="session-url-error">{t(fieldErrors.invalid)}</FieldError>}
        </Field>
        <Field>
          <FieldLabel htmlFor="session-chapter">{t(schedule.chapterOptional)}</FieldLabel>
          <Select
            id="session-chapter"
            value={chapterId}
            onChange={(event) => {
              setChapterId(event.target.value);
            }}
          >
            <option value="">{t(schedule.noChapter)}</option>
            {book.data?.chapters.map((chapter) => (
              <option key={chapter.id} value={chapter.id}>
                {t(structure.chapterNumber, { n: chapter.number })} · {textIn(chapter.title, uiLocale)}
              </option>
            ))}
          </Select>
        </Field>
        <Field>
          <FieldLabel htmlFor="session-note">
            {t(schedule.note)} <span className="font-normal text-ink-muted">({t(common.optional)})</span>
          </FieldLabel>
          <Textarea
            id="session-note"
            rows={3}
            maxLength={2000}
            value={note}
            onChange={(event) => {
              setNote(event.target.value);
            }}
          />
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
