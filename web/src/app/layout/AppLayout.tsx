import { LogOut, Menu, UserRound } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Link, NavLink, Outlet } from "react-router";
import { roleHome } from "@/app/roles";
import { navigation, type NavItem } from "@/app/navigation";
import { Logo } from "@/components/brand/Logo";
import { Label } from "@/components/japanese/Label";
import { LanguageSwitcher } from "@/components/japanese/LanguageSwitcher";
import { ScriptToggle } from "@/components/japanese/ScriptToggle";
import { buttonVariants } from "@/components/ui/button-variants";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import type { UserProfile } from "@/features/identity/api/identity";
import { OnboardingDialog } from "@/features/identity/components/OnboardingDialog";
import { common } from "@/i18n/labels/common";
import { nav, roles } from "@/i18n/labels/nav";
import { useT } from "@/i18n/prefs";
import { cn } from "@/lib/utils";

/** Red brand line at the very top of the app (docs/07 §6). */
export function BrandBar() {
  return <div aria-hidden="true" className="h-[var(--nx-brandbar-h)] bg-brand" />;
}

export function SkipLink() {
  const t = useT();
  return (
    <a
      href="#main"
      className="sr-only z-50 rounded-md bg-sumi-900 px-4 py-2 text-ink-inverse focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
    >
      {t(common.skipToContent)}
    </a>
  );
}

function ProfileMenu({ me }: { me: UserProfile }) {
  const t = useT();
  const initial = me.displayName.trim().charAt(0).toUpperCase();
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        className={cn(buttonVariants({ variant: "ghost", size: "icon" }), "rounded-full")}
        aria-label={t(nav.profileMenu)}
      >
        <span
          className="flex size-8 items-center justify-center rounded-full bg-sumi-900 text-body-sm font-bold text-ink-inverse"
          aria-hidden="true"
        >
          {initial}
        </span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel className="flex flex-col gap-0.5 text-body-sm text-ink">
          <span className="font-semibold">{me.displayName}</span>
          <span className="font-normal text-ink-muted">{t(roles[me.role])}</span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to={`${roleHome[me.role]}/profil`}>
            <UserRound aria-hidden="true" />
            {t(nav.profile)}
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <Link to="/keluar">
            <LogOut aria-hidden="true" />
            {t(nav.logout)}
          </Link>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function TopBar({ me, onOpenNav }: { me: UserProfile; onOpenNav?: () => void }) {
  const t = useT();
  return (
    <header className="sticky top-0 z-10 flex h-[var(--nx-topbar-h)] items-center gap-2 border-b border-line bg-surface px-2 sm:px-4">
      {onOpenNav && (
        <button
          type="button"
          onClick={onOpenNav}
          className={cn(buttonVariants({ variant: "ghost", size: "icon" }), "lg:hidden")}
          aria-label={t(common.menu)}
        >
          <Menu className="size-5" aria-hidden="true" />
        </button>
      )}
      <Link
        to={roleHome[me.role]}
        className="rounded-md focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
      >
        <Logo />
      </Link>
      <div className="ml-auto flex items-center gap-1">
        <ScriptToggle />
        <LanguageSwitcher />
        <ProfileMenu me={me} />
      </div>
    </header>
  );
}

function SideNav({ items, onNavigate }: { items: readonly NavItem[]; onNavigate?: () => void }) {
  const t = useT();
  return (
    <nav aria-label={t(nav.mainNavigation)}>
      <ul className="flex flex-col gap-1">
        {items.map((item) => (
          <li key={item.to}>
            <NavLink
              to={item.to}
              end={item.end ?? false}
              onClick={onNavigate}
              className={({ isActive }) =>
                cn(
                  "relative flex min-h-11 items-center gap-3 rounded-md px-3 py-2 text-body-sm text-ink hover:bg-canvas-muted",
                  "focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none",
                  isActive &&
                    "bg-action-subtle font-semibold text-aka-800 before:absolute before:inset-y-1 before:left-0 before:w-[3px] before:rounded-full before:bg-action hover:bg-action-subtle",
                )
              }
            >
              <item.icon className="size-5 shrink-0" strokeWidth={1.75} aria-hidden="true" />
              <Label value={item.label} companion="below" />
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}

/** Student bottom navigation on phones (docs/07 §6, §12). */
function BottomNav({ items }: { items: readonly NavItem[] }) {
  const t = useT();
  return (
    <nav
      aria-label={t(nav.mainNavigation)}
      className="pb-safe fixed inset-x-0 bottom-0 z-10 border-t border-line bg-surface lg:hidden"
    >
      <ul className="flex h-[var(--nx-bottomnav-h)]">
        {items.map((item) => (
          <li key={item.to} className="flex-1">
            <NavLink
              to={item.to}
              end={item.end ?? false}
              className={({ isActive }) =>
                cn(
                  "flex h-full flex-col items-center justify-center gap-0.5 text-caption text-ink-muted",
                  "focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none focus-visible:ring-inset",
                  isActive && "font-semibold text-action",
                )
              }
            >
              <item.icon className="size-5" strokeWidth={1.75} aria-hidden="true" />
              <Label value={item.label} companion="none" />
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}

/**
 * Frame of the signed-in app per role (docs/07 §6): students get a bottom nav on phones, staff a drawer;
 * from lg everyone gets the sidebar.
 */
export function AppLayout({ me, children }: { me: UserProfile; children?: ReactNode }) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const t = useT();
  const items = navigation[me.role];
  const isStudent = me.role === "murid";

  return (
    <div className="min-h-dvh bg-canvas">
      <SkipLink />
      <BrandBar />
      <TopBar
        me={me}
        onOpenNav={
          isStudent
            ? undefined
            : () => {
                setDrawerOpen(true);
              }
        }
      />
      <div className="mx-auto flex max-w-[calc(var(--nx-content-max)+var(--nx-sidebar-w))]">
        <aside className="hidden w-[var(--nx-sidebar-w)] shrink-0 border-r border-line bg-canvas-subtle p-4 lg:block">
          <SideNav items={items} />
        </aside>
        <main
          id="main"
          tabIndex={-1}
          className={cn(
            "min-w-0 flex-1 px-4 py-6 focus:outline-none md:px-6 lg:px-8",
            isStudent && "pb-24 lg:pb-8",
          )}
        >
          {children ?? <Outlet />}
        </main>
      </div>
      {isStudent && <BottomNav items={items.filter((item) => item.sidebarOnly !== true)} />}
      {!isStudent && (
        <Dialog open={drawerOpen} onOpenChange={setDrawerOpen}>
          <DialogContent closeLabel={t(common.close)} aria-describedby={undefined}>
            <DialogTitle>{t(common.menu)}</DialogTitle>
            <SideNav
              items={items}
              onNavigate={() => {
                setDrawerOpen(false);
              }}
            />
          </DialogContent>
        </Dialog>
      )}
      <OnboardingDialog me={me} />
    </div>
  );
}
