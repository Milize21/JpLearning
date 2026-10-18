import { House, UserRound, Users, type LucideIcon } from "lucide-react";
import type { UserRole } from "@/features/identity/api/identity";
import { nav } from "@/i18n/labels/nav";
import type { UiLabel } from "@/i18n/tb";

export interface NavItem {
  to: string;
  label: UiLabel;
  icon: LucideIcon;
  /** Matches only the exact path (role homes). */
  end?: boolean;
}

/**
 * Navigation per role (docs/07 §12). Only pages that exist are listed; each phase adds its own entries.
 */
export const navigation: Readonly<Record<UserRole, readonly NavItem[]>> = {
  murid: [
    { to: "/murid", label: nav.home, icon: House, end: true },
    { to: "/murid/profil", label: nav.profile, icon: UserRound },
  ],
  sensei: [
    { to: "/sensei", label: nav.home, icon: House, end: true },
    { to: "/sensei/profil", label: nav.profile, icon: UserRound },
  ],
  admin: [
    { to: "/admin", label: nav.home, icon: House, end: true },
    { to: "/admin/pengguna", label: nav.users, icon: Users },
    { to: "/admin/profil", label: nav.profile, icon: UserRound },
  ],
};
