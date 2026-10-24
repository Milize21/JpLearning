import { ChevronRight, Trophy } from "lucide-react";
import { Link } from "react-router";
import { Label } from "@/components/japanese/Label";
import { Skeleton } from "@/components/ui/misc";
import { leaderboard } from "@/i18n/labels/insight";
import { useT } from "@/i18n/prefs";
import { useLeaderboards } from "../api/insight";

/** Student home (docs/07 §11 flow 2): own position on the board of the first level of their classes. */
export function MyRankCard() {
  const t = useT();
  const data = useLeaderboards(undefined);
  const board = data.data?.board;
  if (data.isPending) return <Skeleton className="h-20" />;
  if (!board) return null;

  return (
    <section className="flex flex-col gap-3" aria-labelledby="my-rank">
      <h2 id="my-rank" className="text-body font-bold text-ink">
        <Label value={leaderboard.title} companion="inline" companionClassName="text-body-sm font-medium" />
      </h2>
      <Link
        to="/murid/peringkat"
        className="flex items-center gap-3 rounded-lg border border-line bg-surface px-4 py-3 hover:bg-canvas-subtle focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
      >
        <Trophy className="size-6 shrink-0 text-action" strokeWidth={1.75} aria-hidden="true" />
        <span className="flex min-w-0 flex-1 flex-col">
          <span className="truncate font-semibold text-ink">{board.season.name}</span>
          <span className="text-body-sm text-ink-muted tabular-nums">
            {board.me
              ? `${t(leaderboard.rankValue, { n: board.me.rank })} · ${t(leaderboard.pointsValue, { n: board.me.points })}`
              : t(leaderboard.emptyMine)}
          </span>
        </span>
        <ChevronRight className="size-4 text-ink-subtle" aria-hidden="true" />
      </Link>
    </section>
  );
}
