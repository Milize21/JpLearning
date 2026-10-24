import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { practiceApi } from "@/lib/api/client";
import type { components } from "@/lib/api/schema/practice";

type Schemas = components["schemas"];

export type QuizDto = Schemas["QuizDto"];
export type QuizKind = Schemas["QuizKind"];
export type StudentQuiz = Schemas["StudentQuizDto"];
export type Attempt = Schemas["AttemptDto"];
export type AttemptQuestion = Schemas["AttemptQuestionDto"];
export type AttemptSummary = Schemas["AttemptSummaryDto"];
export type QuizQuestion = Schemas["QuizQuestionDto"];
export type SaveQuizRequest = Schemas["SaveQuizRequest"];
export type AssignmentDto = Schemas["AssignmentDto"];
export type SaveAssignmentRequest = Schemas["SaveAssignmentRequest"];
export type Submission = Schemas["SubmissionDto"];
export type StudentAssignment = Schemas["StudentAssignmentDto"];
export type ClassGrade = Schemas["ClassGradeDto"];
export type GradeScheme = Schemas["GradeSchemeDto"];
export type ScaleStep = Schemas["ScaleStep"];

export const assessmentKeys = {
  all: ["practice", "assessment"] as const,
  classQuizzes: (classId: string) => ["practice", "assessment", "class", classId, "quizzes"] as const,
  quizQuestions: (quizId: string) => ["practice", "assessment", "quiz", quizId, "questions"] as const,
  quizAttempts: (quizId: string) => ["practice", "assessment", "quiz", quizId, "attempts"] as const,
  attempt: (id: string) => ["practice", "assessment", "attempt", id] as const,
  myQuizzes: ["practice", "assessment", "me", "quizzes"] as const,
  classAssignments: (classId: string) => ["practice", "assessment", "class", classId, "assignments"] as const,
  submissions: (assignmentId: string) =>
    ["practice", "assessment", "assignment", assignmentId, "submissions"] as const,
  myAssignments: ["practice", "assessment", "me", "assignments"] as const,
  classGrades: (classId: string) => ["practice", "assessment", "class", classId, "grades"] as const,
  scheme: (classId: string) => ["practice", "assessment", "class", classId, "scheme"] as const,
  myGrades: ["practice", "assessment", "me", "grades"] as const,
};

function useInvalidate() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: assessmentKeys.all });
}

// Quizzes (staff)

export function useClassQuizzes(classId: string) {
  return useQuery({
    queryKey: assessmentKeys.classQuizzes(classId),
    queryFn: async () =>
      (await practiceApi.GET("/api/practice/classes/{id}/quizzes", { params: { path: { id: classId } } }))
        .data as QuizDto[],
  });
}

export function useSaveQuiz(classId: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: async ({ quiz, body }: { quiz?: QuizDto; body: SaveQuizRequest }) => {
      if (quiz) {
        return (
          await practiceApi.PATCH("/api/practice/quizzes/{id}", {
            params: { path: { id: quiz.id } },
            body: { ...body, version: quiz.version },
          })
        ).data as QuizDto;
      }
      return (
        await practiceApi.POST("/api/practice/classes/{id}/quizzes", {
          params: { path: { id: classId } },
          body,
        })
      ).data as QuizDto;
    },
    onSuccess: invalidate,
  });
}

export function useQuizQuestions(quizId: string | undefined) {
  return useQuery({
    queryKey: assessmentKeys.quizQuestions(quizId ?? ""),
    enabled: quizId !== undefined,
    queryFn: async () =>
      (
        await practiceApi.GET("/api/practice/quizzes/{id}/questions", {
          params: { path: { id: quizId ?? "" } },
        })
      ).data as QuizQuestion[],
  });
}

export function useSetQuizQuestions() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: async ({
      quiz,
      questions,
    }: {
      quiz: QuizDto;
      questions: { questionId: string; points: number }[];
    }) =>
      (
        await practiceApi.PUT("/api/practice/quizzes/{id}/questions", {
          params: { path: { id: quiz.id } },
          body: { version: quiz.version, questions },
        })
      ).data as QuizQuestion[],
    onSuccess: invalidate,
  });
}

export function useQuizStatus() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: async ({ id, action }: { id: string; action: "publish" | "archive" }) => {
      const params = { params: { path: { id } } };
      return (
        action === "publish"
          ? await practiceApi.POST("/api/practice/quizzes/{id}/publish", params)
          : await practiceApi.POST("/api/practice/quizzes/{id}/archive", params)
      ).data as QuizDto;
    },
    onSuccess: invalidate,
  });
}

export function useQuizAttempts(quizId: string | undefined) {
  return useQuery({
    queryKey: assessmentKeys.quizAttempts(quizId ?? ""),
    enabled: quizId !== undefined,
    queryFn: async () =>
      (
        await practiceApi.GET("/api/practice/quizzes/{id}/attempts", {
          params: { path: { id: quizId ?? "" } },
        })
      ).data as AttemptSummary[],
  });
}

export function useGradeAnswer(attemptId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ questionId, points }: { questionId: string; points: number }) =>
      (
        await practiceApi.POST("/api/practice/attempts/{id}/answers/{questionId}/grade", {
          params: { path: { id: attemptId, questionId } },
          body: { points },
        })
      ).data as Attempt,
    onSuccess: (attempt) => {
      queryClient.setQueryData(assessmentKeys.attempt(attemptId), attempt);
      void queryClient.invalidateQueries({ queryKey: assessmentKeys.all });
    },
  });
}

// Attempts

export function useAttempt(id: string) {
  return useQuery({
    queryKey: assessmentKeys.attempt(id),
    retry: false,
    queryFn: async () =>
      (await practiceApi.GET("/api/practice/attempts/{id}", { params: { path: { id } } })).data as Attempt,
  });
}

export function useMyQuizzes() {
  return useQuery({
    queryKey: assessmentKeys.myQuizzes,
    queryFn: async () => (await practiceApi.GET("/api/practice/me/quizzes")).data as StudentQuiz[],
  });
}

export function useStartAttempt() {
  return useMutation({
    mutationFn: async (quizId: string) =>
      (await practiceApi.POST("/api/practice/quizzes/{id}/attempts", { params: { path: { id: quizId } } }))
        .data as Attempt,
  });
}

/** Autosave (BR-KUIS-06); the last save wins. */
export async function saveAttemptAnswer(attemptId: string, questionId: string, answer: unknown) {
  await practiceApi.PUT("/api/practice/attempts/{id}/answers/{questionId}", {
    params: { path: { id: attemptId, questionId } },
    body: { answer },
  });
}

export function useSubmitAttempt(attemptId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () =>
      (await practiceApi.POST("/api/practice/attempts/{id}/submit", { params: { path: { id: attemptId } } }))
        .data as Attempt,
    onSuccess: (attempt) => {
      queryClient.setQueryData(assessmentKeys.attempt(attemptId), attempt);
      void queryClient.invalidateQueries({ queryKey: assessmentKeys.myQuizzes });
    },
  });
}

// Assignments

export function useClassAssignments(classId: string) {
  return useQuery({
    queryKey: assessmentKeys.classAssignments(classId),
    queryFn: async () =>
      (await practiceApi.GET("/api/practice/classes/{id}/assignments", { params: { path: { id: classId } } }))
        .data as AssignmentDto[],
  });
}

export function useSaveAssignment(classId: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: async ({ assignment, body }: { assignment?: AssignmentDto; body: SaveAssignmentRequest }) => {
      if (assignment) {
        return (
          await practiceApi.PATCH("/api/practice/assignments/{id}", {
            params: { path: { id: assignment.id } },
            body: { ...body, version: assignment.version },
          })
        ).data as AssignmentDto;
      }
      return (
        await practiceApi.POST("/api/practice/classes/{id}/assignments", {
          params: { path: { id: classId } },
          body,
        })
      ).data as AssignmentDto;
    },
    onSuccess: invalidate,
  });
}

export function useAssignmentStatus() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: async ({ id, action }: { id: string; action: "publish" | "archive" }) => {
      const params = { params: { path: { id } } };
      return (
        action === "publish"
          ? await practiceApi.POST("/api/practice/assignments/{id}/publish", params)
          : await practiceApi.POST("/api/practice/assignments/{id}/archive", params)
      ).data as AssignmentDto;
    },
    onSuccess: invalidate,
  });
}

export function useSubmissions(assignmentId: string | undefined) {
  return useQuery({
    queryKey: assessmentKeys.submissions(assignmentId ?? ""),
    enabled: assignmentId !== undefined,
    queryFn: async () =>
      (
        await practiceApi.GET("/api/practice/assignments/{id}/submissions", {
          params: { path: { id: assignmentId ?? "" } },
        })
      ).data as Submission[],
  });
}

export function useGradeSubmission() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: async ({
      id,
      gradePct,
      feedback,
    }: {
      id: string;
      gradePct: number;
      feedback: string | null;
    }) =>
      (
        await practiceApi.POST("/api/practice/submissions/{id}/grade", {
          params: { path: { id } },
          body: { gradePct, feedback },
        })
      ).data as Submission,
    onSuccess: invalidate,
  });
}

/** BR-FILE-04: a signed link valid for a few minutes; the browser downloads it as an attachment. */
export function useDownloadSubmissionFile() {
  return useMutation({
    mutationFn: async ({ submissionId, fileId }: { submissionId: string; fileId: string }) => {
      const { data } = await practiceApi.GET("/api/practice/submissions/{id}/files/{fileId}/download", {
        params: { path: { id: submissionId, fileId }, query: { redirect: false } },
      });
      window.location.assign((data as Schemas["SubmissionDownloadDto"]).url);
    },
  });
}

export function useMyAssignments() {
  return useQuery({
    queryKey: assessmentKeys.myAssignments,
    queryFn: async () => (await practiceApi.GET("/api/practice/me/assignments")).data as StudentAssignment[],
  });
}

export function useSubmitAssignment(assignmentId: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: async (body: { text: string | null; fileIds: string[] }) =>
      (
        await practiceApi.PUT("/api/practice/assignments/{id}/submission", {
          params: { path: { id: assignmentId } },
          body,
        })
      ).data as Submission,
    onSuccess: invalidate,
  });
}

// Grades

export function useClassGrades(classId: string) {
  return useQuery({
    queryKey: assessmentKeys.classGrades(classId),
    queryFn: async () =>
      (await practiceApi.GET("/api/practice/classes/{id}/grades", { params: { path: { id: classId } } }))
        .data as ClassGrade[],
  });
}

export function useGradeScheme(classId: string) {
  return useQuery({
    queryKey: assessmentKeys.scheme(classId),
    queryFn: async () =>
      (
        await practiceApi.GET("/api/practice/classes/{id}/grade-scheme", {
          params: { path: { id: classId } },
        })
      ).data as GradeScheme,
  });
}

export function useSaveGradeScheme(classId: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: async (body: Schemas["SaveGradeSchemeRequest"]) =>
      (
        await practiceApi.PUT("/api/practice/classes/{id}/grade-scheme", {
          params: { path: { id: classId } },
          body,
        })
      ).data as GradeScheme,
    onSuccess: invalidate,
  });
}

export function useMyGrades() {
  return useQuery({
    queryKey: assessmentKeys.myGrades,
    queryFn: async () => (await practiceApi.GET("/api/practice/me/grades")).data as ClassGrade[],
  });
}
