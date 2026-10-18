import { CircleAlert, CircleOff, Compass, ShieldX } from "lucide-react";
import { useEffect, useRef } from "react";
import { useAuth } from "react-oidc-context";
import { Link, Navigate, useSearchParams } from "react-router";
import { LoadingPage, PublicLayout, StatusPage } from "@/app/layout/PublicLayout";
import { roleHome } from "@/app/roles";
import { Logo } from "@/components/brand/Logo";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { useMe, type UserProfile } from "@/features/identity/api/identity";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { pages, welcome } from "@/i18n/labels/identity";
import { nav, roles } from "@/i18n/labels/nav";
import { useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { sanitizeReturnPath } from "@/lib/auth/oidc";

/** "/" — welcome page with the one red action "Masuk" (docs/07 §11 flow 1). Signed-in users go to their home. */
export function WelcomePage() {
  const auth = useAuth();
  const me = useMe();
  const t = useT();

  if (auth.isLoading) return <LoadingPage />;
  if (auth.isAuthenticated) {
    if (me.data) return <Navigate to={roleHome[me.data.role]} replace />;
    if (me.isError) return <ProfileErrorPage error={me.error} />;
    return <LoadingPage />;
  }

  return (
    <PublicLayout>
      <div className="flex w-full max-w-lg flex-col items-center gap-6 text-center">
        <Logo size="lg" />
        <h1 className="text-h1 font-extrabold text-ink sm:text-display">
          <Label value={welcome.title} companionClassName="mt-2 text-body font-medium" />
        </h1>
        <p className="text-body-lg text-ink-muted">{t(welcome.body)}</p>
        <Button
          variant="primary"
          size="lg"
          className="w-full sm:w-auto"
          onClick={() => void auth.signinRedirect()}
        >
          {t(nav.login)}
        </Button>
      </div>
    </PublicLayout>
  );
}

/** "/masuk/callback" — Keycloak returns here; react-oidc-context has already exchanged the code. */
export function CallbackPage() {
  const auth = useAuth();
  const me = useMe();
  const [params] = useSearchParams();
  const t = useT();

  if (auth.error) {
    return (
      <StatusPage
        icon={CircleAlert}
        title={welcome.signInFailed}
        actions={
          <Button variant="primary" onClick={() => void auth.signinRedirect()}>
            {t(nav.login)}
          </Button>
        }
      />
    );
  }
  if (!auth.isAuthenticated) {
    return auth.isLoading ? <LoadingPage label={welcome.signingIn} /> : <Navigate to="/" replace />;
  }
  if (me.isError) return <ProfileErrorPage error={me.error} />;
  if (!me.data) return <LoadingPage label={welcome.signingIn} />;

  const returnTo = sanitizeReturnPath(params.get("returnTo"));
  return (
    <Navigate to={returnTo?.startsWith(roleHome[me.data.role]) ? returnTo : roleHome[me.data.role]} replace />
  );
}

/** "/keluar" — ends the Keycloak session and comes back to "/". */
export function LogoutPage() {
  const auth = useAuth();
  const started = useRef(false);

  useEffect(() => {
    // On a full page load the session is still being restored; wait for it instead of giving up.
    if (started.current || auth.isLoading || !auth.isAuthenticated) return;
    started.current = true;
    void auth.signoutRedirect();
  }, [auth]);

  if (!auth.isAuthenticated && !auth.isLoading) return <Navigate to="/" replace />;
  return <LoadingPage />;
}

export function ForbiddenPage({ me }: { me: UserProfile }) {
  const t = useT();
  return (
    <StatusPage
      icon={ShieldX}
      title={pages.forbiddenTitle}
      body={t(pages.forbiddenBody, { role: t(roles[me.role]) })}
      actions={
        <Button asChild variant="primary">
          <Link to={roleHome[me.role]}>{t(common.goHome)}</Link>
        </Button>
      }
    />
  );
}

export function NotFoundPage() {
  const t = useT();
  return (
    <StatusPage
      icon={Compass}
      title={pages.notFoundTitle}
      body={t(pages.notFoundBody)}
      actions={
        <Button asChild variant="primary">
          <Link to="/">{t(common.goHome)}</Link>
        </Button>
      }
    />
  );
}

/** Profile could not be loaded: deactivated account (403 account_inactive) or another error. */
export function ProfileErrorPage({ error }: { error: unknown }) {
  const t = useT();
  const code = errorCode(error);
  const logout = (
    <Button asChild variant="outline">
      <Link to="/keluar">{t(nav.logout)}</Link>
    </Button>
  );

  if (code === "account_inactive") {
    return (
      <StatusPage
        icon={CircleOff}
        title={pages.inactiveTitle}
        body={t(pages.inactiveBody)}
        actions={logout}
      />
    );
  }

  return (
    <StatusPage
      icon={CircleAlert}
      title={pages.loadFailed}
      body={t(errorLabel(code))}
      actions={
        <>
          <Button
            variant="primary"
            onClick={() => {
              window.location.reload();
            }}
          >
            {t(common.retry)}
          </Button>
          {logout}
        </>
      }
    />
  );
}
