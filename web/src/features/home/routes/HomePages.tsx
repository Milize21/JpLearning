import { Users } from "lucide-react";
import { Link } from "react-router";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Card, Skeleton } from "@/components/ui/misc";
import { MyClassesCard, NextSessionCard } from "@/features/classroom/components/HomeWidgets";
import { useActiveAccountCount } from "@/features/identity/api/identity";
import { home } from "@/i18n/labels/identity";
import { useT } from "@/i18n/prefs";

function Greeting() {
  const me = useCurrentUser();
  return (
    <h1 className="text-h2 font-bold text-ink sm:text-h1">
      <Label
        value={home.greeting}
        vars={{ name: me.displayName }}
        companionClassName="text-body font-medium"
      />
    </h1>
  );
}

/** /murid — the student's dashboard; classes, schedule and practice arrive with Phases 1–4. */
export function MuridHomePage() {
  return (
    <div className="flex flex-col gap-6">
      <Greeting />
      <div className="grid gap-6 lg:grid-cols-2">
        <NextSessionCard />
        <MyClassesCard />
      </div>
    </div>
  );
}

/** /sensei — teacher dashboard: next session and the classes taught. */
export function SenseiHomePage() {
  return (
    <div className="flex flex-col gap-6">
      <Greeting />
      <div className="grid gap-6 lg:grid-cols-2">
        <NextSessionCard />
        <MyClassesCard />
      </div>
    </div>
  );
}

/** /admin — overview with the number of active accounts and the way to account management. */
export function AdminHomePage() {
  const t = useT();
  const activeAccounts = useActiveAccountCount();

  return (
    <div className="flex flex-col gap-6">
      <Greeting />
      <div className="grid gap-4 md:grid-cols-2">
        <Card className="flex flex-col gap-4">
          <div className="flex items-center gap-3">
            <Users className="size-6 text-ink-muted" strokeWidth={1.75} aria-hidden="true" />
            <h2 className="text-h3 font-semibold">
              <Label value={home.adminTitle} companion="inline" />
            </h2>
          </div>
          <p className="text-body-sm text-ink-muted">{t(home.adminUsersCard)}</p>
          <p className="flex items-baseline gap-2">
            {activeAccounts.isPending ? (
              <Skeleton className="h-9 w-16" />
            ) : (
              <span className="text-h1 font-bold tabular-nums">{activeAccounts.data ?? "–"}</span>
            )}
            <span className="text-body-sm text-ink-muted">{t(home.activeAccounts)}</span>
          </p>
          <Button asChild variant="secondary" className="self-start">
            <Link to="/admin/pengguna">{t(home.openUsers)}</Link>
          </Button>
        </Card>
      </div>
    </div>
  );
}
