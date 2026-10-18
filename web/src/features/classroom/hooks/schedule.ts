import { useEffect, useState } from "react";
import { schedule } from "@/i18n/labels/classroom";
import type { UiLabel } from "@/i18n/tb";
import { dayKey } from "@/lib/datetime";
import type { SessionDto } from "../api/classroom";

/** Re-renders every minute so "Join" switches on 15 minutes before the start. */
export function useNow(intervalMs = 60_000): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = window.setInterval(() => {
      setNow(Date.now());
    }, intervalMs);
    return () => {
      window.clearInterval(timer);
    };
  }, [intervalMs]);
  return now;
}

export type Group = "today" | "tomorrow" | "thisWeek" | "later" | "past";
export const groupOrder: readonly Group[] = ["today", "tomorrow", "thisWeek", "later"];
export const groupLabels: Record<Group, UiLabel> = {
  today: schedule.today,
  tomorrow: schedule.tomorrow,
  thisWeek: schedule.thisWeek,
  later: schedule.later,
  past: schedule.past,
};

/** Groups by calendar day in the viewer's time zone (BR-JADWAL-08). */
export function groupSessions(sessions: readonly SessionDto[], now: number, timeZone: string): Record<Group, SessionDto[]> {
  const groups: Record<Group, SessionDto[]> = { today: [], tomorrow: [], thisWeek: [], later: [], past: [] };
  const today = dayKey(new Date(now), timeZone);
  const tomorrow = dayKey(new Date(now + 86_400_000), timeZone);
  const weekEnd = dayKey(new Date(now + 7 * 86_400_000), timeZone);
  for (const session of sessions) {
    const ends = new Date(session.endsAt).getTime();
    if (ends < now) {
      groups.past.push(session);
      continue;
    }
    const day = dayKey(new Date(session.startsAt), timeZone);
    if (day <= today) groups.today.push(session);
    else if (day === tomorrow) groups.tomorrow.push(session);
    else if (day <= weekEnd) groups.thisWeek.push(session);
    else groups.later.push(session);
  }
  groups.past.reverse();
  return groups;
}
