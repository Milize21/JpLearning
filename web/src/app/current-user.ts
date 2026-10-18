import { useMe, type UserProfile } from "@/features/identity/api/identity";

/** The signed-in profile inside a RoleGate, where it is always loaded. */
export function useCurrentUser(): UserProfile {
  const { data } = useMe();
  if (!data) throw new Error("useCurrentUser must be used below <RoleGate>.");
  return data;
}
