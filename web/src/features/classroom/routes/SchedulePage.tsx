import { CalendarDays } from "lucide-react";
import { useState } from "react";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { schedule } from "@/i18n/labels/classroom";
import { useT } from "@/i18n/prefs";
import { useMySchedule } from "../api/classroom";
import { CalendarFeedCard, RemindersCard } from "../components/CalendarCards";
import { ScheduleItem } from "../components/ScheduleItem";
import { groupLabels, groupOrder, groupSessions, useNow } from "../hooks/schedule";

/** /murid/jadwal and /sensei/jadwal — all sessions of the user's classes, ICS subscription and reminders. */
export function SchedulePage() {
  const t = useT();
  const me = useCurrentUser();
  const sessions = useMySchedule();
  const now = useNow();
  const [showPast, setShowPast] = useState(false);
  const groups = groupSessions(sessions.data ?? [], now, me.timezone);
  const upcoming = groupOrder.some((group) => groups[group].length > 0);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={schedule.title} companionClassName="text-body font-medium" />}
        description={t(schedule.description)}
      />
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="flex flex-col gap-6">
          {sessions.isPending && <Skeleton className="h-32" />}
          {sessions.data && !upcoming && (
            <EmptyState icon={CalendarDays} title={<Label value={schedule.empty} />}>
              {t(schedule.emptyBody)}
            </EmptyState>
          )}
          {groupOrder.map((group) =>
            groups[group].length === 0 ? null : (
              <section key={group} className="flex flex-col gap-3" aria-label={t(groupLabels[group])}>
                <h2 className="text-body font-bold text-ink">
                  <Label
                    value={groupLabels[group]}
                    companion="inline"
                    companionClassName="text-body-sm font-medium"
                  />
                </h2>
                {groups[group].map((session) => (
                  <ScheduleItem key={session.id} session={session} now={now} />
                ))}
              </section>
            ),
          )}
          {groups.past.length > 0 && (
            <section className="flex flex-col gap-3">
              <Button
                variant="ghost"
                className="self-start"
                aria-expanded={showPast}
                onClick={() => {
                  setShowPast(!showPast);
                }}
              >
                {t(schedule.showPast)}
              </Button>
              {showPast &&
                groups.past.map((session) => <ScheduleItem key={session.id} session={session} now={now} />)}
            </section>
          )}
        </div>
        <aside className="flex flex-col gap-4">
          <RemindersCard />
          <CalendarFeedCard />
        </aside>
      </div>
    </div>
  );
}
