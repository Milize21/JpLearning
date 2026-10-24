import { CalendarPlus, Pencil, Video, XCircle } from "lucide-react";
import { useCurrentUser } from "@/app/current-user";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { Badge, Card } from "@/components/ui/misc";
import { schedule } from "@/i18n/labels/classroom";
import { usePrefs, useT } from "@/i18n/prefs";
import { formatRange } from "@/lib/datetime";
import { cn } from "@/lib/utils";
import type { SessionDto } from "../api/classroom";

const joinOpensMs = 15 * 60_000;

/** Google Calendar "add event" template link (docs/05 §12), times in UTC. */
function googleCalendarUrl(session: SessionDto, details: string): string {
  const stamp = (iso: string) => iso.replace(/[-:]/gu, "").replace(/\.\d{3}/u, "");
  const params = new URLSearchParams({
    action: "TEMPLATE",
    text: `[${session.className}] ${session.topic}`,
    dates: `${stamp(new Date(session.startsAt).toISOString())}/${stamp(new Date(session.endsAt).toISOString())}`,
    details,
  });
  return `https://calendar.google.com/calendar/render?${params.toString()}`;
}

type SessionPhase = "upcoming" | "ongoing" | "finished" | "cancelled";

function sessionPhase(session: SessionDto, now: number): SessionPhase {
  if (session.status === "cancelled") return "cancelled";
  if (now >= new Date(session.endsAt).getTime()) return "finished";
  if (now >= new Date(session.startsAt).getTime()) return "ongoing";
  return "upcoming";
}

/**
 * ScheduleItem of docs/07 §10: date and time in the viewer's zone (BR-JADWAL-08), class, topic, platform; "Join"
 * from 15 minutes before the start until the end, and an "Add to Google Calendar" link.
 */
export function ScheduleItem({
  session,
  now,
  onEdit,
  onCancel,
}: {
  session: SessionDto;
  now: number;
  onEdit?: (session: SessionDto) => void;
  onCancel?: (session: SessionDto) => void;
}) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const phase = sessionPhase(session, now);
  const canJoin =
    session.meetingUrl !== null &&
    (phase === "ongoing" ||
      (phase === "upcoming" && new Date(session.startsAt).getTime() - now <= joinOpensMs));
  const platform = t(schedule[session.platform]);
  const details = [
    `${t(schedule.platform)}: ${platform}`,
    session.meetingUrl ? `${t(schedule.link)}: ${session.meetingUrl}` : null,
    session.note,
  ]
    .filter(Boolean)
    .join("\n");

  return (
    <Card
      className={cn("flex flex-col gap-3 p-4", phase === "cancelled" && "bg-canvas-subtle text-ink-muted")}
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex min-w-0 flex-col gap-1">
          <p className="text-body-sm font-semibold text-ink-muted">
            {formatRange(session.startsAt, session.endsAt, uiLocale, me.timezone)}
          </p>
          <h3 className={cn("font-semibold text-ink", phase === "cancelled" && "line-through")}>
            {session.topic}
          </h3>
          <p className="text-body-sm text-ink-muted">
            {session.className} · {platform}
          </p>
          {session.note && <p className="text-body-sm text-ink">{session.note}</p>}
        </div>
        <div className="flex flex-wrap items-center gap-1">
          {phase === "cancelled" && <Badge tone="danger">{t(schedule.cancelled)}</Badge>}
          {phase === "ongoing" && <Badge tone="brand">{t(schedule.ongoing)}</Badge>}
          {phase === "finished" && <Badge>{t(schedule.finished)}</Badge>}
        </div>
      </div>
      {phase !== "cancelled" && phase !== "finished" && (
        <div className="flex flex-wrap items-center gap-2">
          {session.meetingUrl &&
            (canJoin ? (
              <a
                href={session.meetingUrl}
                target="_blank"
                rel="noopener noreferrer"
                className={buttonVariants({ variant: "primary", size: "sm" })}
              >
                <Video aria-hidden="true" />
                {t(schedule.join)}
              </a>
            ) : (
              <span className="text-caption text-ink-muted">{t(schedule.joinHint)}</span>
            ))}
          <a
            href={googleCalendarUrl(session, details)}
            target="_blank"
            rel="noopener noreferrer"
            className={buttonVariants({ variant: "outline", size: "sm" })}
          >
            <CalendarPlus aria-hidden="true" />
            {t(schedule.addToGoogle)}
          </a>
          {onEdit && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                onEdit(session);
              }}
            >
              <Pencil aria-hidden="true" />
              {t(schedule.edit)}
            </Button>
          )}
          {onCancel && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                onCancel(session);
              }}
            >
              <XCircle aria-hidden="true" />
              {t(schedule.cancel)}
            </Button>
          )}
        </div>
      )}
    </Card>
  );
}
