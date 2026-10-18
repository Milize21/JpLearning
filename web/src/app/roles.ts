import type { UserRole } from "@/features/identity/api/identity";

/** Home route of each role (docs/07 §12). */
export const roleHome: Readonly<Record<UserRole, string>> = {
  murid: "/murid",
  sensei: "/sensei",
  admin: "/admin",
};
