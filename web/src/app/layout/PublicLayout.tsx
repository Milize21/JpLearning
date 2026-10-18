import { LoaderCircle, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { Logo } from "@/components/brand/Logo";
import { Label } from "@/components/japanese/Label";
import { LanguageSwitcher } from "@/components/japanese/LanguageSwitcher";
import { ScriptToggle } from "@/components/japanese/ScriptToggle";
import { common } from "@/i18n/labels/common";
import type { UiLabel } from "@/i18n/tb";
import { BrandBar, SkipLink } from "./AppLayout";

/** Frame for pages outside the role layouts: welcome, callback, status pages. */
export function PublicLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col bg-canvas">
      <SkipLink />
      <BrandBar />
      <header className="flex h-[var(--nx-topbar-h)] items-center gap-2 border-b border-line bg-surface px-2 sm:px-4">
        <Logo />
        <div className="ml-auto flex items-center gap-1">
          <ScriptToggle />
          <LanguageSwitcher />
        </div>
      </header>
      <main
        id="main"
        tabIndex={-1}
        className="flex flex-1 items-center justify-center px-4 py-10 focus:outline-none"
      >
        {children}
      </main>
    </div>
  );
}

/** Centered message with optional actions; used for loading, 403, 404 and inactive accounts. */
export function StatusPage({
  icon: Icon = LoaderCircle,
  title,
  body,
  actions,
  busy = false,
}: {
  icon?: LucideIcon;
  title: UiLabel;
  body?: ReactNode;
  actions?: ReactNode;
  busy?: boolean;
}) {
  return (
    <PublicLayout>
      <div className="flex max-w-md flex-col items-center gap-4 text-center" aria-busy={busy || undefined}>
        <Icon
          className={busy ? "size-10 animate-spin text-ink-subtle" : "size-12 text-ink-subtle"}
          strokeWidth={1.75}
          aria-hidden="true"
        />
        <h1 className="text-h2 font-bold text-ink">
          <Label value={title} />
        </h1>
        {body && <div className="text-body text-ink-muted">{body}</div>}
        {actions && <div className="flex flex-wrap justify-center gap-2">{actions}</div>}
      </div>
    </PublicLayout>
  );
}

export function LoadingPage({ label = common.loading }: { label?: UiLabel }) {
  return <StatusPage title={label} busy />;
}
