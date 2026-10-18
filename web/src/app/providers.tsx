import { QueryCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { type ReactNode, useState } from "react";
import { AuthProvider } from "react-oidc-context";
import { Toaster } from "sonner";
import { PrefsSync } from "@/features/identity/components/PrefsSync";
import { PrefsProvider } from "@/i18n/PrefsProvider";
import { ApiError } from "@/lib/api/errors";
import { sanitizeReturnPath, userManager } from "@/lib/auth/oidc";

function createQueryClient() {
  return new QueryClient({
    queryCache: new QueryCache(),
    defaultOptions: {
      queries: {
        // Access and ownership errors will not change on a retry.
        retry: (failureCount, error) =>
          failureCount < 2 && !(error instanceof ApiError && error.status >= 400 && error.status < 500),
        refetchOnWindowFocus: false,
      },
    },
  });
}

export function AppProviders({ children }: { children: ReactNode }) {
  const [queryClient] = useState(createQueryClient);

  return (
    <AuthProvider
      userManager={userManager}
      onSigninCallback={(user) => {
        // Drop ?code&state from the address; the callback page then routes to the role's home or the saved page.
        const state = user?.state as { returnTo?: unknown } | undefined;
        const returnTo = sanitizeReturnPath(state?.returnTo);
        window.history.replaceState(
          {},
          document.title,
          returnTo ? `/masuk/callback?returnTo=${encodeURIComponent(returnTo)}` : "/masuk/callback",
        );
      }}
    >
      <QueryClientProvider client={queryClient}>
        <PrefsProvider>
          <PrefsSync />
          {children}
          <Toaster
            position="top-center"
            toastOptions={{
              classNames: {
                toast: "rounded-lg border border-line bg-surface text-ink shadow-md",
                error: "text-danger",
                success: "text-success",
              },
            }}
          />
        </PrefsProvider>
      </QueryClientProvider>
    </AuthProvider>
  );
}
