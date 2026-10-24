import { Archive, Trophy } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { FieldLabel, Select } from "@/components/ui/form";
import { Card, EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { common } from "@/i18n/labels/common";
import { leaderboard } from "@/i18n/labels/insight";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { formatDate } from "@/lib/datetime";
import { useLeaderboards, useSeasons, useStandings, type Season } from "../api/insight";
import { LiveIndicator, NoPointsHint, Podium, RankingList } from "../components/Board";
import { useHubConnected, useLiveSeason } from "../hooks/leaderboardHub";

/**
 * /murid|/sensei|/admin/peringkat (docs/07 §11 flow 11): choose a level → podium of three + list, own row sticky, "Live"
 * indicator, and the archive of closed seasons. Students only see the levels of their classes (Q-06).
 */
export function LeaderboardPage() {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const [levelId, setLevelId] = useState<string | undefined>(undefined);
  const data = useLeaderboards(levelId);
  const board = data.data?.board ?? null;
  const connected = useHubConnected();
  const [archived, setArchived] = useState<Season | null>(null);

  useLiveSeason(board?.season.id, levelId, () => {
    toast.info(t(leaderboard.seasonClosedToast));
  });

  if (data.isPending) return <Skeleton className="h-96" />;
  const levels = data.data?.levels ?? [];
  const isStudent = me.role === "murid";

  return (
    <div className="flex flex-col gap-6 pb-4">
      <PageHeader
        title={<Label value={leaderboard.title} companionClassName="text-body font-medium" />}
        description={t(leaderboard.description)}
        actions={board ? <LiveIndicator connected={connected} /> : undefined}
      />

      {levels.length === 0 && <EmptyState icon={Trophy} title={<Label value={leaderboard.noLevels} />} />}

      {levels.length > 1 && (
        <div className="flex max-w-xs flex-col gap-1.5">
          <FieldLabel htmlFor="leaderboard-level">{t(leaderboard.level)}</FieldLabel>
          <Select
            id="leaderboard-level"
            value={levelId ?? board?.season.levelId ?? ""}
            onChange={(event) => {
              setLevelId(event.target.value);
            }}
          >
            {levels.map((level) => (
              <option key={level.levelId} value={level.levelId}>
                {textIn(level.name, uiLocale)}
              </option>
            ))}
          </Select>
        </div>
      )}

      {board && (
        <>
          <div className="flex flex-col gap-1">
            <h2 className="text-h3 font-semibold text-ink">{board.season.name}</h2>
            <p className="text-body-sm text-ink-muted">
              {formatDate(board.season.startsAt, uiLocale, me.timezone)}
            </p>
          </div>
          {board.top.length === 0 ? (
            <EmptyState icon={Trophy} title={<Label value={leaderboard.emptyBoard} />} />
          ) : (
            <>
              <Podium entries={board.top} myKey={board.me?.entryKey} />
              <RankingList entries={board.top} me={board.me} myName={me.displayName} />
            </>
          )}
          {isStudent && !board.me && <NoPointsHint />}
          <SeasonArchive levelId={board.season.levelId} onOpen={setArchived} />
        </>
      )}

      {archived && (
        <StandingsDialog
          season={archived}
          onClose={() => {
            setArchived(null);
          }}
        />
      )}
    </div>
  );
}

function SeasonArchive({ levelId, onOpen }: { levelId: string; onOpen: (season: Season) => void }) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const seasons = useSeasons(levelId);
  const closed = (seasons.data ?? []).filter((season) => season.status === "closed");

  return (
    <Card className="flex flex-col gap-3">
      <h2 className="flex items-center gap-2 text-h3 font-semibold text-ink">
        <Archive className="size-5" aria-hidden="true" />
        {t(leaderboard.archive)}
      </h2>
      {seasons.isPending && <Skeleton className="h-10" />}
      {!seasons.isPending && closed.length === 0 && (
        <p className="text-body-sm text-ink-muted">{t(leaderboard.archiveEmpty)}</p>
      )}
      <ul className="flex flex-col divide-y divide-line">
        {closed.map((season) => (
          <li key={season.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
            <span className="text-ink">
              {season.name}
              <span className="ml-2 text-body-sm text-ink-muted">
                {formatDate(season.startsAt, uiLocale, me.timezone)}
                {season.endsAt && ` – ${formatDate(season.endsAt, uiLocale, me.timezone)}`}
              </span>
            </span>
            <Button
              size="sm"
              onClick={() => {
                onOpen(season);
              }}
            >
              {t(leaderboard.viewStandings)}
            </Button>
          </li>
        ))}
      </ul>
    </Card>
  );
}

function StandingsDialog({ season, onClose }: { season: Season; onClose: () => void }) {
  const t = useT();
  const me = useCurrentUser();
  const [page, setPage] = useState(1);
  const standings = useStandings(season.id, page);
  const data = standings.data;
  const pages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent closeLabel={t(common.close)}>
        <DialogHeader>
          <DialogTitle>{t(leaderboard.standingsOf, { name: season.name })}</DialogTitle>
        </DialogHeader>
        {standings.isPending && <Skeleton className="h-40" />}
        {data && data.items.length === 0 && (
          <p className="text-body-sm text-ink-muted">{t(leaderboard.emptyBoard)}</p>
        )}
        {data && data.items.length > 0 && (
          <div className="max-h-[60vh] overflow-y-auto">
            <RankingList entries={data.items} me={data.me} myName={me.displayName} skipPodium={false} />
          </div>
        )}
        {pages > 1 && (
          <div className="flex items-center justify-between gap-2">
            <Button
              size="sm"
              disabled={page <= 1}
              onClick={() => {
                setPage(page - 1);
              }}
            >
              {t(leaderboard.previous)}
            </Button>
            <span className="text-body-sm text-ink-muted tabular-nums">
              {page} / {pages}
            </span>
            <Button
              size="sm"
              disabled={page >= pages}
              onClick={() => {
                setPage(page + 1);
              }}
            >
              {t(leaderboard.next)}
            </Button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
