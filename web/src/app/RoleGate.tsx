import { useEffect } from "react";
import { useAuth } from "react-oidc-context";
import { useLocation } from "react-router";
import { AppLayout } from "@/app/layout/AppLayout";
import { LoadingPage } from "@/app/layout/PublicLayout";
import { ForbiddenPage, ProfileErrorPage } from "@/app/pages/PublicPages";
import { useMe, type UserRole } from "@/features/identity/api/identity";
import { welcome } from "@/i18n/labels/identity";

/**
 * Route guard per role (docs/07 §12): not signed in → Keycloak login; another role → friendly 403 page.
 * The API enforces the same rules; this only keeps users on pages they can use.
 */
export function RoleGate({ requiredRole }: { requiredRole: UserRole }) {
  const auth = useAuth();
  const location = useLocation();
  const me = useMe();

  const mustSignIn = !auth.isLoading && !auth.isAuthenticated && !auth.activeNavigator && !auth.error;
  useEffect(() => {
    if (mustSignIn) void auth.signinRedirect({ state: { returnTo: location.pathname + location.search } });
  }, [mustSignIn, auth, location.pathname, location.search]);

  if (!auth.isAuthenticated) return <LoadingPage label={welcome.redirecting} />;
  if (me.isError) return <ProfileErrorPage error={me.error} />;
  if (!me.data) return <LoadingPage />;
  if (me.data.role !== requiredRole) return <ForbiddenPage me={me.data} />;
  return <AppLayout me={me.data} />;
}
