import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { insightApi } from "@/lib/api/client";
import type { components } from "@/lib/api/schema/insight";

type Schemas = components["schemas"];

export type LeaderboardsResponse = Schemas["LeaderboardsResponse"];
export type Leaderboard = Schemas["LeaderboardDto"];
export type LeaderboardEntry = Schemas["LeaderboardEntryDto"];
export type MyRank = Schemas["MyRankDto"];
export type Season = Schemas["SeasonDto"];
export type StandingsPage = Schemas["StandingsPage"];
export type ActivitySummary = Schemas["ActivitySummaryDto"];

export const insightKeys = {
  all: ["insight"] as const,
  leaderboards: (levelId: string | undefined) => ["insight", "leaderboards", levelId ?? "default"] as const,
  seasons: (levelId: string | undefined) => ["insight", "seasons", levelId ?? "all"] as const,
  standings: (seasonId: string, page: number) => ["insight", "standings", seasonId, page] as const,
  classActivity: (classId: string) => ["insight", "class", classId, "activity"] as const,
};

/** Visible levels and the live board of one level (the first visible one when none is chosen). */
export function useLeaderboards(levelId: string | undefined) {
  return useQuery({
    queryKey: insightKeys.leaderboards(levelId),
    queryFn: async () =>
      (await insightApi.GET("/api/insight/leaderboards", { params: { query: { levelId } } }))
        .data as LeaderboardsResponse,
    placeholderData: keepPreviousData,
  });
}

export function useSeasons(levelId?: string) {
  return useQuery({
    queryKey: insightKeys.seasons(levelId),
    queryFn: async () =>
      (await insightApi.GET("/api/insight/seasons", { params: { query: { levelId } } })).data as Season[],
  });
}

export function useStandings(seasonId: string, page: number) {
  return useQuery({
    queryKey: insightKeys.standings(seasonId, page),
    queryFn: async () =>
      (
        await insightApi.GET("/api/insight/seasons/{id}/standings", {
          params: { path: { id: seasonId }, query: { page, pageSize: 20 } },
        })
      ).data as StandingsPage,
    placeholderData: keepPreviousData,
  });
}

export function useCloseSeason() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (seasonId: string) =>
      (await insightApi.POST("/api/insight/seasons/{id}/close", { params: { path: { id: seasonId } } }))
        .data as Season,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: insightKeys.all }),
  });
}

/** "Terakhir aktif" per student of a class (docs/05 §11). */
export function useClassActivitySummary(classId: string) {
  return useQuery({
    queryKey: insightKeys.classActivity(classId),
    queryFn: async () =>
      (
        await insightApi.GET("/api/insight/classes/{id}/activity-summary", {
          params: { path: { id: classId } },
        })
      ).data as ActivitySummary[],
  });
}
