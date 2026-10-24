import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useSyncExternalStore } from "react";
import { currentAccessToken } from "@/lib/auth/oidc";
import {
  insightKeys,
  type Leaderboard,
  type LeaderboardEntry,
  type LeaderboardsResponse,
  type MyRank,
} from "../api/insight";

/**
 * One shared connection to /hubs/leaderboard (docs/05 §9) for every component that listens, started on first use and
 * stopped when nobody listens any more. The access token goes in the query string, which the API accepts only on /hubs.
 */
let connection: HubConnection | null = null;
let starting: Promise<void> | null = null;
let listeners = 0;
const stateSubscribers = new Set<() => void>();

const notify = () => {
  stateSubscribers.forEach((callback) => {
    callback();
  });
};

function hub(): HubConnection {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl("/hubs/leaderboard", { accessTokenFactory: async () => (await currentAccessToken()) ?? "" })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.onreconnecting(notify);
    connection.onreconnected(notify);
    connection.onclose(notify);
    // Messages to the user arrive on every page; pages that do not show them listen through these no-ops.
    connection.on("PointsAwarded", () => undefined);
    connection.on("MyRankUpdated", () => undefined);
  }
  return connection;
}

// Stopping waits a moment: navigating between pages (and React's development double mount) releases and re-acquires
// at once, and stopping during the negotiation would kill the connection the next page is waiting for.
const stopDelayMs = 3000;
let stopTimer: number | undefined;
let stopping: Promise<void> | null = null;

async function acquire(): Promise<HubConnection> {
  listeners += 1;
  window.clearTimeout(stopTimer);
  if (stopping) await stopping;
  const current = hub();
  if (current.state === HubConnectionState.Disconnected) {
    starting ??= current.start().finally(() => {
      starting = null;
      notify();
    });
  }
  if (starting) await starting;
  return current;
}

function release() {
  listeners -= 1;
  if (listeners > 0) return;
  window.clearTimeout(stopTimer);
  stopTimer = window.setTimeout(() => {
    if (listeners > 0 || !connection) return;
    stopping = connection.stop().finally(() => {
      stopping = null;
    });
  }, stopDelayMs);
}

function subscribeState(callback: () => void) {
  stateSubscribers.add(callback);
  return () => {
    stateSubscribers.delete(callback);
  };
}

/** True while the hub is connected; drives the "Live" indicator. */
export function useHubConnected(): boolean {
  return useSyncExternalStore(subscribeState, () => connection?.state === HubConnectionState.Connected);
}

/** PointsAwarded of docs/05 §9, sent to the student alone. */
export interface PointsAwarded {
  points: number;
  rule: string;
  sourceType: string;
}

/** Per-user hub messages that components can listen to. */
interface UserMessages {
  PointsAwarded: PointsAwarded;
}

/** Listens to one hub message for as long as the component is mounted. */
export function useHubEvent<K extends keyof UserMessages>(
  name: K,
  handler: (payload: UserMessages[K]) => void,
) {
  const handlerRef = useRef(handler);
  useEffect(() => {
    handlerRef.current = handler;
  });

  useEffect(() => {
    let active = true;
    const listener = (payload: UserMessages[K]) => {
      handlerRef.current(payload);
    };
    acquire()
      .then((current) => {
        if (active) current.on(name, listener);
      })
      .catch(() => {
        // Offline: the page still shows the board from the REST API; the indicator says "Tidak tersambung".
      });
    return () => {
      active = false;
      connection?.off(name, listener);
      release();
    };
  }, [name]);
}

interface BoardUpdate {
  seasonId: string;
  top: LeaderboardEntry[];
  updatedAt: string;
}

interface SeasonClosed {
  seasonId: string;
  newSeasonId: string;
}

/**
 * Joins the group of the shown season and writes every LeaderboardUpdated / MyRankUpdated into the cached board, so the
 * page renders live data through the same query. Re-joins after a reconnect.
 */
export function useLiveSeason(
  seasonId: string | undefined,
  levelKey: string | undefined,
  onClosed: () => void,
) {
  const queryClient = useQueryClient();
  const onClosedRef = useRef(onClosed);
  useEffect(() => {
    onClosedRef.current = onClosed;
  });

  useEffect(() => {
    if (!seasonId) return undefined;
    let active = true;
    const key = insightKeys.leaderboards(levelKey);
    const patch = (change: (board: Leaderboard) => Leaderboard) => {
      queryClient.setQueryData<LeaderboardsResponse>(key, (data) =>
        data?.board?.season.id === seasonId ? { ...data, board: change(data.board) } : data,
      );
    };
    const onBoard = (update: BoardUpdate) => {
      if (update.seasonId === seasonId)
        patch((board) => ({ ...board, top: update.top, updatedAt: update.updatedAt }));
    };
    const onMine = (rank: MyRank) => {
      if (rank.seasonId === seasonId) patch((board) => ({ ...board, me: rank }));
    };
    const onSeasonClosed = (closed: SeasonClosed) => {
      if (closed.seasonId !== seasonId) return;
      void queryClient.invalidateQueries({ queryKey: insightKeys.all });
      onClosedRef.current();
    };
    const join = async (current: HubConnection) => {
      const snapshot = await current.invoke<Leaderboard>("JoinSeason", seasonId);
      if (active) patch(() => snapshot);
    };

    let joined: HubConnection | null = null;
    const rejoin = () => {
      if (active && joined) void join(joined).catch(() => undefined);
    };
    acquire()
      .then(async (current) => {
        if (!active) return;
        joined = current;
        current.on("LeaderboardUpdated", onBoard);
        current.on("MyRankUpdated", onMine);
        current.on("SeasonClosed", onSeasonClosed);
        current.onreconnected(rejoin);
        await join(current);
      })
      .catch(() => undefined);

    return () => {
      active = false;
      if (joined) {
        joined.off("LeaderboardUpdated", onBoard);
        joined.off("MyRankUpdated", onMine);
        joined.off("SeasonClosed", onSeasonClosed);
        if (joined.state === HubConnectionState.Connected) {
          void joined.invoke("LeaveSeason", seasonId).catch(() => undefined);
        }
      }
      release();
    };
  }, [seasonId, levelKey, queryClient]);
}
