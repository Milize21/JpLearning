import type { UiLocale } from "@/i18n/types";

/** Indonesian zones by their usual names (BR-JADWAL-07/08), elsewhere the UTC offset. Same rule as the server. */
export function zoneAbbreviation(timeZone: string, at: Date): string {
  if (timeZone === "Asia/Jakarta" || timeZone === "Asia/Pontianak") return "WIB";
  if (timeZone === "Asia/Makassar") return "WITA";
  if (timeZone === "Asia/Jayapura") return "WIT";
  const part = new Intl.DateTimeFormat("en-US", { timeZone, timeZoneName: "shortOffset" })
    .formatToParts(at)
    .find((item) => item.type === "timeZoneName");
  return part?.value.replace("GMT", "UTC") ?? "UTC";
}

const intlLocale = (locale: UiLocale) => (locale === "en" ? "en-GB" : "id-ID");

/** "Rab, 21 Okt" / "Wed 21 Oct" in the viewer's time zone (BR-JADWAL-08). */
export function formatDay(iso: string, locale: UiLocale, timeZone: string): string {
  return new Intl.DateTimeFormat(intlLocale(locale), {
    timeZone,
    weekday: "short",
    day: "numeric",
    month: "short",
  }).format(new Date(iso));
}

export function formatTime(iso: string, locale: UiLocale, timeZone: string): string {
  return new Intl.DateTimeFormat(intlLocale(locale), {
    timeZone,
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(new Date(iso));
}

/** "Rab, 21 Okt · 19.00–20.30 WIB". */
export function formatRange(startIso: string, endIso: string, locale: UiLocale, timeZone: string): string {
  const start = new Date(startIso);
  return `${formatDay(startIso, locale, timeZone)} · ${formatTime(startIso, locale, timeZone)}–${formatTime(endIso, locale, timeZone)} ${zoneAbbreviation(timeZone, start)}`;
}

export function formatDate(iso: string, locale: UiLocale, timeZone: string): string {
  return new Intl.DateTimeFormat(intlLocale(locale), {
    timeZone,
    day: "numeric",
    month: "short",
    year: "numeric",
  }).format(new Date(iso));
}

export function formatDateTime(iso: string, locale: UiLocale, timeZone: string): string {
  const date = new Date(iso);
  return `${formatDate(iso, locale, timeZone)} ${formatTime(iso, locale, timeZone)} ${zoneAbbreviation(timeZone, date)}`;
}

/** Calendar date (yyyy-mm-dd) of an instant in a time zone, for grouping by day. */
export function dayKey(date: Date, timeZone: string): string {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(date);
}

/** Offset of a time zone at an instant, in minutes east of UTC. */
function offsetMinutes(timeZone: string, at: Date): number {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone,
    hourCycle: "h23",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).formatToParts(at);
  const value = (type: string) => Number(parts.find((part) => part.type === type)?.value ?? 0);
  const asUtc = Date.UTC(
    value("year"),
    value("month") - 1,
    value("day"),
    value("hour"),
    value("minute"),
    value("second"),
  );
  return Math.round((asUtc - at.getTime()) / 60_000);
}

/** A wall-clock date and time in a time zone ("2026-10-21", "19:00") as an ISO instant. */
export function zonedToIso(date: string, time: string, timeZone: string): string {
  const [year, month, day] = date.split("-").map(Number);
  const [hour, minute] = time.split(":").map(Number);
  const guess = Date.UTC(year ?? 1970, (month ?? 1) - 1, day ?? 1, hour ?? 0, minute ?? 0);
  const offset = offsetMinutes(timeZone, new Date(guess));
  return new Date(guess - offset * 60_000).toISOString();
}

/** The wall-clock date and time of an instant in a time zone, for form fields. */
export function isoToZoned(iso: string, timeZone: string): { date: string; time: string } {
  const at = new Date(iso);
  const time = new Intl.DateTimeFormat("en-GB", {
    timeZone,
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).format(at);
  return { date: dayKey(at, timeZone), time };
}
