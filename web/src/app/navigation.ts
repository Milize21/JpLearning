import {
  BookMarked,
  BookOpen,
  ClipboardList,
  FileQuestion,
  GraduationCap,
  NotebookPen,
  Target,
  CalendarDays,
  Database,
  House,
  Layers,
  PencilLine,
  School,
  UserRound,
  Users,
  type LucideIcon,
} from "lucide-react";
import type { UserRole } from "@/features/identity/api/identity";
import { nav } from "@/i18n/labels/nav";
import type { UiLabel } from "@/i18n/tb";

export interface NavItem {
  to: string;
  label: UiLabel;
  icon: LucideIcon;
  /** Matches only the exact path (role homes). */
  end?: boolean;
  /** Left out of the student bottom nav on phones, which holds at most five items (docs/07 §12). */
  sidebarOnly?: boolean;
}

/**
 * Navigation per role (docs/07 §12). Only pages that exist are listed; each phase adds its own entries.
 */
export const navigation: Readonly<Record<UserRole, readonly NavItem[]>> = {
  murid: [
    { to: "/murid", label: nav.home, icon: House, end: true },
    { to: "/murid/kelas", label: nav.myClasses, icon: School, sidebarOnly: true },
    { to: "/murid/materi", label: nav.materials, icon: BookOpen },
    { to: "/murid/latihan", label: nav.practice, icon: PencilLine },
    { to: "/murid/kelemahan", label: nav.weaknesses, icon: Target, sidebarOnly: true },
    { to: "/murid/kuis", label: nav.quizzes, icon: ClipboardList, sidebarOnly: true },
    { to: "/murid/tugas", label: nav.assignments, icon: NotebookPen, sidebarOnly: true },
    { to: "/murid/nilai", label: nav.grades, icon: GraduationCap, sidebarOnly: true },
    { to: "/murid/jadwal", label: nav.schedule, icon: CalendarDays },
    { to: "/murid/profil", label: nav.profile, icon: UserRound },
  ],
  sensei: [
    { to: "/sensei", label: nav.home, icon: House, end: true },
    { to: "/sensei/kelas", label: nav.classes, icon: School },
    { to: "/sensei/bank-soal", label: nav.questionBank, icon: FileQuestion },
    { to: "/sensei/jadwal", label: nav.schedule, icon: CalendarDays },
    { to: "/sensei/profil", label: nav.profile, icon: UserRound },
  ],
  admin: [
    { to: "/admin", label: nav.home, icon: House, end: true },
    { to: "/admin/pengguna", label: nav.users, icon: Users },
    { to: "/admin/konten", label: nav.content, icon: Layers },
    { to: "/admin/referensi", label: nav.reference, icon: Database },
    { to: "/admin/bank-soal", label: nav.questionBank, icon: FileQuestion },
    { to: "/admin/sumber-soal", label: nav.questionSources, icon: BookMarked },
    { to: "/admin/kelas", label: nav.classes, icon: School },
    { to: "/admin/profil", label: nav.profile, icon: UserRound },
  ],
};
