import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "react-oidc-context";
import { identityApi } from "@/lib/api/client";
import type { components } from "@/lib/api/schema/identity";

export type UserProfile = components["schemas"]["UserProfileDto"];
export type UserRole = components["schemas"]["UserRole"];
export type CreateUserRequest = components["schemas"]["CreateUserRequest"];
export type CreateUserResponse = components["schemas"]["CreateUserResponse"];
export type UpdateUserRequest = components["schemas"]["UpdateUserRequest"];
export type UpdatePreferencesRequest = components["schemas"]["UpdatePreferencesRequest"];
export type ImportUsersResponse = components["schemas"]["ImportUsersResponse"];
export type CsvImportError = components["schemas"]["CsvImportError"];

export interface UserFilter {
  q?: string;
  role?: UserRole;
  isActive?: boolean;
  page: number;
  pageSize: number;
}

export const identityKeys = {
  me: ["identity", "me"] as const,
  users: ["identity", "users"] as const,
  userList: (filter: UserFilter) => ["identity", "users", filter] as const,
};

/** Own profile; the source of the role used for routing (docs/01 §9). */
export function useMe() {
  const auth = useAuth();
  return useQuery({
    queryKey: identityKeys.me,
    enabled: auth.isAuthenticated,
    staleTime: 60_000,
    queryFn: async () => {
      const { data } = await identityApi.GET("/api/identity/me");
      return data as UserProfile;
    },
  });
}

export function useUpdatePreferences() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (changes: UpdatePreferencesRequest) => {
      const { data } = await identityApi.PATCH("/api/identity/me/preferences", { body: changes });
      return data as UserProfile;
    },
    onSuccess: (profile) => {
      queryClient.setQueryData(identityKeys.me, profile);
    },
  });
}

export function useUsers(filter: UserFilter) {
  return useQuery({
    queryKey: identityKeys.userList(filter),
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data } = await identityApi.GET("/api/identity/users", {
        params: {
          query: {
            q: filter.q || undefined,
            role: filter.role,
            isActive: filter.isActive,
            page: filter.page,
            pageSize: filter.pageSize,
          },
        },
      });
      return data as components["schemas"]["PagedResultOfUserProfileDto"];
    },
  });
}

/** Number of active accounts, for the admin overview. */
export function useActiveAccountCount() {
  return useQuery({
    queryKey: [...identityKeys.users, "active-count"],
    queryFn: async () => {
      const { data } = await identityApi.GET("/api/identity/users", {
        params: { query: { isActive: true, pageSize: 1 } },
      });
      return data?.totalCount ?? 0;
    },
  });
}

function useInvalidateUsers() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: identityKeys.users });
}

export function useCreateUser() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: async (request: CreateUserRequest) => {
      const { data } = await identityApi.POST("/api/identity/users", { body: request });
      return data as CreateUserResponse;
    },
    onSuccess: invalidate,
  });
}

export function useUpdateUser() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: async ({ id, changes }: { id: string; changes: UpdateUserRequest }) => {
      const { data } = await identityApi.PATCH("/api/identity/users/{id}", {
        params: { path: { id } },
        body: changes,
      });
      return data as UserProfile;
    },
    onSuccess: invalidate,
  });
}

export function useResetPassword() {
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await identityApi.POST("/api/identity/users/{id}/reset-password", {
        params: { path: { id } },
      });
      return data as components["schemas"]["ResetPasswordResponse"];
    },
  });
}

export function useSetUserActive() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: async ({ id, active }: { id: string; active: boolean }) => {
      const path = active ? "/api/identity/users/{id}/activate" : "/api/identity/users/{id}/deactivate";
      await identityApi.POST(path, { params: { path: { id } } });
    },
    onSuccess: invalidate,
  });
}

export function useImportUsers() {
  const invalidate = useInvalidateUsers();
  return useMutation({
    mutationFn: async ({ file, dryRun }: { file: File; dryRun: boolean }) => {
      const { data } = await identityApi.POST("/api/identity/users/import", {
        params: { query: { dryRun } },
        body: { file } as never,
        bodySerializer: () => {
          const form = new FormData();
          form.append("file", file);
          return form;
        },
      });
      return data as ImportUsersResponse;
    },
    onSuccess: (result) => {
      if (!result.dryRun) void invalidate();
    },
  });
}
