import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { practiceApi } from "@/lib/api/client";
import type { components } from "@/lib/api/schema/practice";

type Schemas = components["schemas"];

export type Category = Schemas["Category"];
export type QuestionKind = Schemas["QuestionKind"];
export type QuestionStatus = Schemas["QuestionStatus"];
export type QuestionScope = Schemas["QuestionScope"];
export type QuestionDto = Schemas["QuestionDto"];
export type SafeQuestion = Schemas["SafeQuestion"];
export type AnswerResult = Schemas["AnswerResultDto"];
export type DrillMode = Schemas["DrillMode"];
export type InputMode = Schemas["InputMode"];
export type DrillSummary = Schemas["DrillSummaryDto"];
export type DrillSessionSummary = Schemas["DrillSessionSummaryDto"];
export type MasteryItem = Schemas["MasteryItemDto"];
export type MasteryStatus = Schemas["MasteryStatus"];
export type Weaknesses = Schemas["WeaknessesDto"];
export type WeaknessGroup = Schemas["WeaknessGroupDto"];
export type ClassProgress = Schemas["ClassProgressDto"];
export type StudentProgress = Schemas["StudentProgressDto"];
export type Comment = Schemas["CommentDto"];
export type QuestionSource = Schemas["QuestionSourceDto"];
export type GeneratorTemplate = Schemas["GeneratorTemplate"];
export type CreateQuestionRequest = Schemas["CreateQuestionRequest"];
export type UpdateQuestionRequest = Schemas["UpdateQuestionRequest"];
export type StartDrillRequest = Schemas["StartDrillRequest"];

export const categories: readonly Category[] = [
  "hiragana",
  "katakana",
  "kanji",
  "vocab",
  "grammar",
  "sentence",
];

export const practiceKeys = {
  all: ["practice"] as const,
  questions: (filter: QuestionFilter) => ["practice", "questions", filter] as const,
  question: (id: string) => ["practice", "question", id] as const,
  studentView: (id: string) => ["practice", "question", id, "student-view"] as const,
  sources: ["practice", "sources"] as const,
  mySessions: ["practice", "me", "sessions"] as const,
  myWeaknesses: ["practice", "me", "weaknesses"] as const,
  myMastery: (chapterId?: string) => ["practice", "me", "mastery", chapterId ?? "all"] as const,
  classProgress: (classId: string) => ["practice", "class", classId, "progress"] as const,
  studentWeaknesses: (studentId: string, classId?: string) =>
    ["practice", "student", studentId, "weaknesses", classId ?? "all"] as const,
  studentSessions: (studentId: string) => ["practice", "student", studentId, "sessions"] as const,
  comments: (filter: CommentFilter) => ["practice", "comments", filter] as const,
};

export interface QuestionFilter {
  levelId?: string;
  chapterId?: string;
  category?: Category;
  kind?: QuestionKind;
  status?: QuestionStatus;
  scope?: QuestionScope;
  q?: string;
  page: number;
  pageSize: number;
}

export function useQuestions(filter: QuestionFilter) {
  return useQuery({
    queryKey: practiceKeys.questions(filter),
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data } = await practiceApi.GET("/api/practice/questions", {
        params: { query: { ...filter, q: filter.q?.trim() ? filter.q.trim() : undefined } },
      });
      return data as Schemas["PagedResultOfQuestionDto"];
    },
  });
}

export function useQuestionStudentView(id: string | undefined) {
  return useQuery({
    queryKey: practiceKeys.studentView(id ?? ""),
    enabled: id !== undefined,
    queryFn: async () => {
      const { data } = await practiceApi.GET("/api/practice/questions/{id}/student-view", {
        params: { path: { id: id ?? "" } },
      });
      return data as SafeQuestion;
    },
  });
}

function useInvalidatePractice() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: practiceKeys.all });
}

export function useSaveQuestion() {
  const invalidate = useInvalidatePractice();
  return useMutation({
    mutationFn: async ({
      question,
      create,
      update,
    }: {
      question?: QuestionDto;
      create?: CreateQuestionRequest;
      update?: UpdateQuestionRequest;
    }) => {
      if (question && update) {
        const { data } = await practiceApi.PATCH("/api/practice/questions/{id}", {
          params: { path: { id: question.id } },
          body: update,
        });
        return data as QuestionDto;
      }
      const { data } = await practiceApi.POST("/api/practice/questions", {
        body: create as CreateQuestionRequest,
      });
      return data as QuestionDto;
    },
    onSuccess: invalidate,
  });
}

export type QuestionAction = "submit-review" | "publish" | "archive" | "duplicate";

export function useQuestionAction() {
  const invalidate = useInvalidatePractice();
  return useMutation({
    mutationFn: async ({ id, action }: { id: string; action: QuestionAction }) => {
      const params = { params: { path: { id } } };
      switch (action) {
        case "submit-review":
          return (await practiceApi.POST("/api/practice/questions/{id}/submit-review", params))
            .data as QuestionDto;
        case "publish":
          return (await practiceApi.POST("/api/practice/questions/{id}/publish", params)).data as QuestionDto;
        case "archive":
          return (await practiceApi.POST("/api/practice/questions/{id}/archive", params)).data as QuestionDto;
        default:
          return (await practiceApi.POST("/api/practice/questions/{id}/duplicate", params))
            .data as QuestionDto;
      }
    },
    onSuccess: invalidate,
  });
}

export function usePublishMany() {
  const invalidate = useInvalidatePractice();
  return useMutation({
    mutationFn: async (questionIds: string[]) => {
      const { data } = await practiceApi.POST("/api/practice/questions/publish", { body: { questionIds } });
      return data as Schemas["BulkPublishResponse"];
    },
    onSuccess: invalidate,
  });
}

/** Evaluates an answer without storing anything (staff preview, BR-AKSES-05). */
export function usePreviewAnswer() {
  return useMutation({
    mutationFn: async ({ id, answer }: { id: string; answer: unknown }) => {
      const { data } = await practiceApi.POST("/api/practice/questions/{id}/preview-answer", {
        params: { path: { id } },
        body: { answer },
      });
      return data as AnswerResult;
    },
  });
}

export function usePreviewDrill() {
  return useMutation({
    mutationFn: async (body: Schemas["PreviewDrillRequest"]) => {
      const { data } = await practiceApi.POST("/api/practice/preview/drill", { body });
      return data as SafeQuestion[];
    },
  });
}

export function useSources(enabled = true) {
  return useQuery({
    queryKey: practiceKeys.sources,
    enabled,
    queryFn: async () => {
      const { data } = await practiceApi.GET("/api/practice/sources");
      return data as QuestionSource[];
    },
  });
}

export function useSaveSource() {
  const invalidate = useInvalidatePractice();
  return useMutation({
    mutationFn: async ({
      id,
      name,
      license,
      url,
      markReviewed,
    }: {
      id?: string;
      name: string;
      license: string;
      url: string | null;
      markReviewed: boolean;
    }) => {
      if (id) {
        const { data } = await practiceApi.PATCH("/api/practice/sources/{id}", {
          params: { path: { id } },
          body: { name, license, url, markReviewed },
        });
        return data as QuestionSource;
      }
      const { data } = await practiceApi.POST("/api/practice/sources", { body: { name, license, url } });
      return data as QuestionSource;
    },
    onSuccess: invalidate,
  });
}

export function useRunGenerator() {
  const invalidate = useInvalidatePractice();
  return useMutation({
    mutationFn: async (body: Schemas["GeneratorRunRequest"]) => {
      const { data } = await practiceApi.POST("/api/practice/generator/runs", { body });
      return data as Schemas["GeneratorRunResponse"];
    },
    onSuccess: invalidate,
  });
}

export function useStartDrill() {
  return useMutation({
    mutationFn: async (body: StartDrillRequest) => {
      const { data } = await practiceApi.POST("/api/practice/drill-sessions", { body });
      return data as Schemas["StartDrillResponse"];
    },
  });
}

export async function fetchNextBatch(sessionId: string, count?: number) {
  const { data } = await practiceApi.GET("/api/practice/drill-sessions/{id}/next", {
    params: { path: { id: sessionId }, query: { count } },
  });
  return data as Schemas["DrillBatchDto"];
}

export function useSubmitAnswer(sessionId: string) {
  return useMutation({
    mutationFn: async (body: Schemas["SubmitAnswerRequest"]) => {
      const { data } = await practiceApi.POST("/api/practice/drill-sessions/{id}/answers", {
        params: { path: { id: sessionId } },
        body,
      });
      return data as AnswerResult;
    },
  });
}

export function useEndDrill(sessionId: string) {
  const invalidate = useInvalidatePractice();
  return useMutation({
    mutationFn: async () => {
      const { data } = await practiceApi.POST("/api/practice/drill-sessions/{id}/end", {
        params: { path: { id: sessionId } },
      });
      return data as DrillSummary;
    },
    onSuccess: invalidate,
  });
}

export function useMyWeaknesses() {
  return useQuery({
    queryKey: practiceKeys.myWeaknesses,
    queryFn: async () => (await practiceApi.GET("/api/practice/me/weaknesses")).data as Weaknesses,
  });
}

export function useMyMastery(chapterId?: string) {
  return useQuery({
    queryKey: practiceKeys.myMastery(chapterId),
    queryFn: async () =>
      (await practiceApi.GET("/api/practice/me/mastery", { params: { query: { chapterId } } }))
        .data as MasteryItem[],
  });
}

export function useMyDrillSessions() {
  return useQuery({
    queryKey: practiceKeys.mySessions,
    queryFn: async () =>
      (await practiceApi.GET("/api/practice/me/drill-sessions", { params: { query: { pageSize: 10 } } }))
        .data as Schemas["PagedResultOfDrillSessionSummaryDto"],
  });
}

export function useClassProgress(classId: string) {
  return useQuery({
    queryKey: practiceKeys.classProgress(classId),
    queryFn: async () =>
      (
        await practiceApi.GET("/api/practice/classes/{id}/progress", {
          params: { path: { id: classId } },
        })
      ).data as ClassProgress,
  });
}

export function useStudentWeaknesses(studentId: string, classId?: string) {
  return useQuery({
    queryKey: practiceKeys.studentWeaknesses(studentId, classId),
    retry: false,
    queryFn: async () =>
      (
        await practiceApi.GET("/api/practice/students/{id}/weaknesses", {
          params: { path: { id: studentId }, query: { classId } },
        })
      ).data as Weaknesses,
  });
}

export function useStudentSessions(studentId: string) {
  return useQuery({
    queryKey: practiceKeys.studentSessions(studentId),
    retry: false,
    queryFn: async () =>
      (
        await practiceApi.GET("/api/practice/students/{id}/drill-sessions", {
          params: { path: { id: studentId }, query: { pageSize: 10 } },
        })
      ).data as Schemas["PagedResultOfDrillSessionSummaryDto"],
  });
}

export interface CommentFilter {
  studentId?: string;
  targetType?: Schemas["CommentTarget"];
  targetId?: string;
}

export function useComments(filter: CommentFilter, enabled = true) {
  return useQuery({
    queryKey: practiceKeys.comments(filter),
    enabled,
    queryFn: async () =>
      (await practiceApi.GET("/api/practice/comments", { params: { query: filter } })).data as Comment[],
  });
}

export function useCreateComment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["CreateCommentRequest"]) => {
      const { data } = await practiceApi.POST("/api/practice/comments", { body });
      return data as Comment;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["practice", "comments"] }),
  });
}
