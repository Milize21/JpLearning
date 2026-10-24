import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { classroomApi } from "@/lib/api/client";
import type { components } from "@/lib/api/schema/classroom";

type Schemas = components["schemas"];

export type ClassDto = Schemas["ClassDto"];
export type ClassStatus = Schemas["ClassStatus"];
export type ClassStudentDto = Schemas["ClassStudentDto"];
export type TeacherDto = Schemas["TeacherDto"];
export type ClassUnlockDto = Schemas["ClassUnlockDto"];
export type SessionDto = Schemas["SessionDto"];
export type SessionPlatform = Schemas["SessionPlatform"];
export type CreateSessionRequest = Schemas["CreateSessionRequest"];
export type UpdateSessionRequest = Schemas["UpdateSessionRequest"];
export type AddStudentsResponse = Schemas["AddStudentsResponse"];
export type PushSubscriptionDto = Schemas["PushSubscriptionDto"];

export const classroomKeys = {
  all: ["classroom"] as const,
  classes: ["classroom", "classes"] as const,
  class: (id: string) => ["classroom", "class", id] as const,
  students: (id: string) => ["classroom", "class", id, "students"] as const,
  teachers: (id: string) => ["classroom", "class", id, "teachers"] as const,
  unlocks: (id: string) => ["classroom", "class", id, "unlocks"] as const,
  sessions: (id: string) => ["classroom", "class", id, "sessions"] as const,
  schedule: ["classroom", "schedule"] as const,
  calendarToken: ["classroom", "calendar-token"] as const,
  push: ["classroom", "push"] as const,
};

/** Classes the caller may see (admin all, sensei taught, murid joined). One page of 100 is plenty for a community. */
export function useClasses() {
  return useQuery({
    queryKey: classroomKeys.classes,
    queryFn: async () => {
      const { data } = await classroomApi.GET("/api/classroom/classes", {
        params: { query: { pageSize: 100 } },
      });
      return (data as Schemas["PagedResultOfClassDto"]).items;
    },
  });
}

export function useClass(id: string | undefined) {
  return useQuery({
    queryKey: classroomKeys.class(id ?? ""),
    enabled: id !== undefined,
    retry: false,
    queryFn: async () => {
      const { data } = await classroomApi.GET("/api/classroom/classes/{id}", {
        params: { path: { id: id ?? "" } },
      });
      return data as ClassDto;
    },
  });
}

export function useClassStudents(id: string, enabled = true) {
  return useQuery({
    queryKey: classroomKeys.students(id),
    enabled,
    queryFn: async () => {
      const { data } = await classroomApi.GET("/api/classroom/classes/{id}/students", {
        params: { path: { id } },
      });
      return data as ClassStudentDto[];
    },
  });
}

export function useClassTeachers(id: string, enabled = true) {
  return useQuery({
    queryKey: classroomKeys.teachers(id),
    enabled,
    retry: false,
    queryFn: async () => {
      const { data } = await classroomApi.GET("/api/classroom/classes/{id}/teachers", {
        params: { path: { id } },
      });
      return data as TeacherDto[];
    },
  });
}

export function useClassUnlocks(id: string | undefined) {
  return useQuery({
    queryKey: classroomKeys.unlocks(id ?? ""),
    enabled: id !== undefined,
    queryFn: async () => {
      const { data } = await classroomApi.GET("/api/classroom/classes/{id}/unlocks", {
        params: { path: { id: id ?? "" } },
      });
      return data as ClassUnlockDto[];
    },
  });
}

/** Sessions from 30 days ago to 180 days ahead, like the calendar feed. */
function scheduleWindow() {
  const now = Date.now();
  return {
    from: new Date(now - 30 * 86_400_000).toISOString(),
    to: new Date(now + 180 * 86_400_000).toISOString(),
  };
}

export function useClassSessions(id: string) {
  return useQuery({
    queryKey: classroomKeys.sessions(id),
    queryFn: async () => {
      const { data } = await classroomApi.GET("/api/classroom/classes/{id}/sessions", {
        params: { path: { id }, query: scheduleWindow() },
      });
      return data as SessionDto[];
    },
  });
}

export function useMySchedule() {
  return useQuery({
    queryKey: classroomKeys.schedule,
    queryFn: async () => {
      const { data } = await classroomApi.GET("/api/classroom/me/schedule", {
        params: { query: scheduleWindow() },
      });
      return data as SessionDto[];
    },
  });
}

function useInvalidateClassroom() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: classroomKeys.all });
}

export function useCreateClass() {
  const invalidate = useInvalidateClassroom();
  return useMutation({
    mutationFn: async (body: Schemas["CreateClassRequest"]) => {
      const { data } = await classroomApi.POST("/api/classroom/classes", { body });
      return data as ClassDto;
    },
    onSuccess: invalidate,
  });
}

export function useUpdateClass(id: string) {
  const invalidate = useInvalidateClassroom();
  return useMutation({
    mutationFn: async (body: Schemas["UpdateClassRequest"]) => {
      const { data } = await classroomApi.PATCH("/api/classroom/classes/{id}", {
        params: { path: { id } },
        body,
      });
      return data as ClassDto;
    },
    onSuccess: invalidate,
  });
}

export function useArchiveClass(id: string) {
  const invalidate = useInvalidateClassroom();
  return useMutation({
    mutationFn: async () => {
      await classroomApi.POST("/api/classroom/classes/{id}/archive", { params: { path: { id } } });
    },
    onSuccess: invalidate,
  });
}

export function useAddStudents(id: string) {
  const invalidate = useInvalidateClassroom();
  return useMutation({
    mutationFn: async (body: Schemas["AddStudentsRequest"]) => {
      const { data } = await classroomApi.POST("/api/classroom/classes/{id}/students", {
        params: { path: { id } },
        body,
      });
      return data as AddStudentsResponse;
    },
    onSuccess: invalidate,
  });
}

export function useRemoveStudent(id: string) {
  const invalidate = useInvalidateClassroom();
  return useMutation({
    mutationFn: async (studentId: string) => {
      await classroomApi.DELETE("/api/classroom/classes/{id}/students/{studentId}", {
        params: { path: { id, studentId } },
      });
    },
    onSuccess: invalidate,
  });
}

export function useAddTeacher(id: string) {
  const invalidate = useInvalidateClassroom();
  return useMutation({
    mutationFn: async (body: Schemas["AddTeacherRequest"]) => {
      await classroomApi.POST("/api/classroom/classes/{id}/teachers", { params: { path: { id } }, body });
    },
    onSuccess: invalidate,
  });
}

export function useRemoveTeacher(id: string) {
  const invalidate = useInvalidateClassroom();
  return useMutation({
    mutationFn: async (teacherId: string) => {
      await classroomApi.DELETE("/api/classroom/classes/{id}/teachers/{teacherId}", {
        params: { path: { id, teacherId } },
      });
    },
    onSuccess: invalidate,
  });
}

export function useSetUnlock(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ chapterId, unlocked }: { chapterId: string; unlocked: boolean }) => {
      const params = { params: { path: { id, chapterId } } };
      if (unlocked) await classroomApi.PUT("/api/classroom/classes/{id}/unlocks/{chapterId}", params);
      else await classroomApi.DELETE("/api/classroom/classes/{id}/unlocks/{chapterId}", params);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: classroomKeys.unlocks(id) });
      await queryClient.invalidateQueries({ queryKey: ["content"] });
    },
  });
}

export function useCreateSession(classId: string) {
  const invalidate = useInvalidateClassroom();
  return useMutation({
    mutationFn: async (body: CreateSessionRequest) => {
      const { data } = await classroomApi.POST("/api/classroom/classes/{id}/sessions", {
        params: { path: { id: classId } },
        body,
      });
      return data as SessionDto;
    },
    onSuccess: invalidate,
  });
}

export function useUpdateSession() {
  const invalidate = useInvalidateClassroom();
  return useMutation({
    mutationFn: async ({ id, changes }: { id: string; changes: UpdateSessionRequest }) => {
      const { data } = await classroomApi.PATCH("/api/classroom/sessions/{id}", {
        params: { path: { id } },
        body: changes,
      });
      return data as SessionDto;
    },
    onSuccess: invalidate,
  });
}

export function useCancelSession() {
  const invalidate = useInvalidateClassroom();
  return useMutation({
    mutationFn: async (id: string) => {
      await classroomApi.POST("/api/classroom/sessions/{id}/cancel", { params: { path: { id } } });
    },
    onSuccess: invalidate,
  });
}

export function useCalendarTokenStatus() {
  return useQuery({
    queryKey: classroomKeys.calendarToken,
    queryFn: async () => {
      const { data } = await classroomApi.GET("/api/classroom/me/calendar-token");
      return data as Schemas["CalendarTokenStatusDto"];
    },
  });
}

export function useCreateCalendarToken() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await classroomApi.POST("/api/classroom/me/calendar-token");
      return (data as Schemas["CalendarTokenDto"]).url;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: classroomKeys.calendarToken }),
  });
}

export function useRevokeCalendarToken() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await classroomApi.DELETE("/api/classroom/me/calendar-token");
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: classroomKeys.calendarToken }),
  });
}

export function usePushSubscriptions() {
  return useQuery({
    queryKey: classroomKeys.push,
    queryFn: async () => {
      const { data } = await classroomApi.GET("/api/classroom/me/push-subscriptions");
      return data as PushSubscriptionDto[];
    },
  });
}

export async function fetchPushPublicKey(): Promise<string | null> {
  const { data } = await classroomApi.GET("/api/classroom/push/public-key");
  return data?.publicKey ?? null;
}

export function useSavePushSubscription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (subscription: PushSubscriptionJSON) => {
      const { data } = await classroomApi.POST("/api/classroom/me/push-subscriptions", {
        body: {
          endpoint: subscription.endpoint ?? "",
          keys: { p256dh: subscription.keys?.p256dh ?? "", auth: subscription.keys?.auth ?? "" },
          userAgent: navigator.userAgent.slice(0, 300),
        },
      });
      return data as PushSubscriptionDto;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: classroomKeys.push }),
  });
}

export function useDeletePushSubscription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await classroomApi.DELETE("/api/classroom/me/push-subscriptions/{id}", { params: { path: { id } } });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: classroomKeys.push }),
  });
}
