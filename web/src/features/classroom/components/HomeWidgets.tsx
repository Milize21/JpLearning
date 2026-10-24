import { CalendarDays, ChevronRight } from "lucide-react";
import { Link } from "react-router";
import { useCurrentUser } from "@/app/current-user";
import { roleHome } from "@/app/roles";
import { Label } from "@/components/japanese/Label";
import { Card, Skeleton } from "@/components/ui/misc";
import { classes, schedule } from "@/i18n/labels/classroom";
import { home } from "@/i18n/labels/identity";
import { useT } from "@/i18n/prefs";
import { useClasses, useMySchedule } from "../api/classroom";
import { useNow } from "../hooks/schedule";
import { ScheduleItem } from "./ScheduleItem";

/** "Berikutnya" on the home pages (docs/07 §11 flow 2): the nearest session that has not ended. */
export function NextSessionCard() {
  const t = useT();
  const me = useCurrentUser();
  const sessions = useMySchedule();
  const now = useNow();
  const next = (sessions.data ?? [])
    .filter((session) => session.status === "scheduled" && new Date(session.endsAt).getTime() > now)
    .sort((a, b) => a.startsAt.localeCompare(b.startsAt))[0];

  return (
    <section className="flex flex-col gap-3" aria-labelledby="next-session">
      <div className="flex items-center justify-between gap-2">
        <h2 id="next-session" className="text-body font-bold text-ink">
          <Label value={schedule.next} companion="inline" companionClassName="text-body-sm font-medium" />
        </h2>
        <Link
          to={`${roleHome[me.role]}/jadwal`}
          className="rounded-sm text-body-sm font-semibold text-ink underline-offset-4 hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
        >
          {t(schedule.title)}
        </Link>
      </div>
      {sessions.isPending && <Skeleton className="h-28" />}
      {sessions.data && !next && (
        <Card className="flex items-center gap-3 p-4 text-body-sm text-ink-muted">
          <CalendarDays className="size-5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
          {t(schedule.empty)}
        </Card>
      )}
      {next && <ScheduleItem session={next} now={now} />}
    </section>
  );
}

/** Short list of the user's classes linking to each class page. */
export function MyClassesCard() {
  const t = useT();
  const me = useCurrentUser();
  const list = useClasses();
  const base = `${roleHome[me.role]}/kelas`;
  const isStaff = me.role !== "murid";
  const visible = (list.data ?? []).filter((item) => item.status !== "archived");

  return (
    <section className="flex flex-col gap-3" aria-labelledby="my-classes">
      <div className="flex items-center justify-between gap-2">
        <h2 id="my-classes" className="text-body font-bold text-ink">
          <Label
            value={isStaff ? classes.title : classes.myClasses}
            companion="inline"
            companionClassName="text-body-sm font-medium"
          />
        </h2>
        <Link
          to={base}
          className="rounded-sm text-body-sm font-semibold text-ink underline-offset-4 hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
        >
          {t(classes.backToClasses)}
        </Link>
      </div>
      {list.isPending && <Skeleton className="h-20" />}
      {list.data && visible.length === 0 && (
        <Card className="flex flex-col gap-1 p-4 text-body-sm text-ink-muted">
          <span className="font-semibold text-ink">
            {t(isStaff ? home.senseiEmptyTitle : home.muridEmptyTitle)}
          </span>
          {t(isStaff ? classes.emptyStaff : home.muridEmptyBody)}
        </Card>
      )}
      {visible.length > 0 && (
        <ul className="flex flex-col divide-y divide-line rounded-lg border border-line bg-surface">
          {visible.map((item) => (
            <li key={item.id}>
              <Link
                to={isStaff ? `${base}/${item.id}/bab` : `${base}/${item.id}`}
                className="flex items-center gap-3 px-4 py-3 hover:bg-canvas-subtle focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none focus-visible:ring-inset"
              >
                <span className="min-w-0 flex-1 truncate font-semibold text-ink">{item.name}</span>
                <span className="text-caption text-ink-muted">
                  {t(classes.students, { n: item.studentCount })}
                </span>
                <ChevronRight className="size-4 shrink-0 text-ink-subtle" aria-hidden="true" />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
