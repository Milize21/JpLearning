import { Crown, Radio, WifiOff } from "lucide-react";
import { Label } from "@/components/japanese/Label";
import { leaderboard } from "@/i18n/labels/insight";
import { useT } from "@/i18n/prefs";
import { cn } from "@/lib/utils";
import type { LeaderboardEntry, MyRank } from "../api/insight";

const pointsFormat = (points: number) => (Number.isInteger(points) ? String(points) : points.toFixed(1));

/** "Live" when the hub is connected (docs/07 §11 flow 11); text and icon, never color alone. */
export function LiveIndicator({ connected }: { connected: boolean }) {
  const t = useT();
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-caption font-semibold",
        connected ? "border-aka-700 text-aka-700" : "border-line text-ink-muted",
      )}
      title={connected ? t(leaderboard.liveHint) : undefined}
    >
      {connected ? (
        <Radio className="size-3.5 motion-safe:animate-pulse" aria-hidden="true" />
      ) : (
        <WifiOff className="size-3.5" aria-hidden="true" />
      )}
      {t(connected ? leaderboard.live : leaderboard.offline)}
    </span>
  );
}

/** Top three as dark cards with red numbers (docs/07 §10 Podium). */
export function Podium({ entries, myKey }: { entries: LeaderboardEntry[]; myKey: string | undefined }) {
  const t = useT();
  const top = entries.filter((entry) => entry.rank <= 3).slice(0, 3);
  if (top.length === 0) return null;

  return (
    <section aria-label={t(leaderboard.podium)}>
      <ol className="grid gap-3 sm:grid-cols-3">
        {top.map((entry) => (
          <li
            key={entry.entryKey}
            className={cn(
              "flex flex-col gap-1 rounded-lg bg-sumi-950 p-4 text-ink-inverse",
              entry.entryKey === myKey && "ring-2 ring-aka-400 ring-offset-2 ring-offset-canvas",
            )}
          >
            <span className="flex items-center gap-2 text-h2 font-bold text-aka-400 tabular-nums">
              {entry.rank === 1 && <Crown className="size-5" aria-hidden="true" />}
              {t(leaderboard.rankValue, { n: entry.rank })}
            </span>
            <span className="truncate text-body font-semibold">
              {entry.displayName}
              {entry.entryKey === myKey && ` (${t(leaderboard.you)})`}
            </span>
            <span className="text-body-sm text-sumi-300 tabular-nums">
              {t(leaderboard.pointsValue, { n: pointsFormat(entry.points) })}
            </span>
          </li>
        ))}
      </ol>
    </section>
  );
}

function Row({ entry, mine }: { entry: LeaderboardEntry; mine: boolean }) {
  const t = useT();
  return (
    <li
      className={cn(
        "grid grid-cols-[3.5rem_minmax(0,1fr)_auto] items-center gap-3 px-4 py-3",
        mine && "border-l-4 border-aka-700 bg-aka-50",
      )}
      aria-current={mine ? "true" : undefined}
    >
      <span className="font-semibold text-ink tabular-nums">
        {t(leaderboard.rankValue, { n: entry.rank })}
      </span>
      <span className="truncate text-ink">
        {entry.displayName}
        {mine && <span className="ml-2 text-body-sm font-semibold text-aka-700">({t(leaderboard.you)})</span>}
      </span>
      <span className="text-body-sm font-semibold text-ink tabular-nums">
        {t(leaderboard.pointsValue, { n: pointsFormat(entry.points) })}
      </span>
    </li>
  );
}

/**
 * The ranking list below the podium. The caller's own row is marked; when it is outside the list (or scrolled away) a
 * copy sticks to the bottom of the viewport (docs/07 §10 LeaderboardRow).
 */
export function RankingList({
  entries,
  me,
  myName,
  skipPodium = true,
}: {
  entries: LeaderboardEntry[];
  me: MyRank | null | undefined;
  myName?: string;
  skipPodium?: boolean;
}) {
  const t = useT();
  const rows = skipPodium ? entries.filter((entry) => entry.rank > 3) : entries;
  const mineListed = me ? entries.some((entry) => entry.entryKey === me.entryKey) : false;

  return (
    <section aria-label={t(leaderboard.list)} className="flex flex-col gap-2">
      {rows.length > 0 && (
        <ol className="flex flex-col divide-y divide-line rounded-lg border border-line bg-surface">
          {rows.map((entry) => (
            <Row key={entry.entryKey} entry={entry} mine={entry.entryKey === me?.entryKey} />
          ))}
        </ol>
      )}
      {me && !mineListed && (
        <div className="sticky bottom-[calc(var(--nx-bottomnav-h)+0.5rem)] rounded-lg border border-line bg-surface shadow-md lg:bottom-4">
          <p className="px-4 pt-2 text-caption font-semibold text-ink-muted">{t(leaderboard.myPosition)}</p>
          <ol>
            <Row
              entry={{
                rank: me.rank,
                entryKey: me.entryKey,
                displayName: myName ?? t(leaderboard.you),
                points: me.points,
              }}
              mine
            />
          </ol>
        </div>
      )}
    </section>
  );
}

export function NoPointsHint() {
  return (
    <p className="rounded-lg border border-dashed border-line px-4 py-3 text-body-sm text-ink-muted">
      <Label value={leaderboard.noPointsYet} companion="none" />
    </p>
  );
}
