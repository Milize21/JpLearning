import { UserManager, WebStorageStateStore } from "oidc-client-ts";

/**
 * Keycloak login with Authorization Code + PKCE (docs/02 §8). Tokens live in sessionStorage, never localStorage
 * (AGENTS.md §10). Refresh tokens rotate on every renewal (docs/01 §8).
 */
const authority = import.meta.env.VITE_OIDC_AUTHORITY ?? "http://localhost:8081/realms/nomax";
const clientId = import.meta.env.VITE_OIDC_CLIENT_ID ?? "nomax-web";

export const CALLBACK_PATH = "/masuk/callback";

export const userManager = new UserManager({
  authority,
  client_id: clientId,
  redirect_uri: `${window.location.origin}${CALLBACK_PATH}`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  response_type: "code",
  scope: "openid profile email",
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  automaticSilentRenew: true,
  // The SPA reads roles from /api/identity/me, not from the ID token.
  loadUserInfo: false,
});

export async function currentAccessToken(): Promise<string | undefined> {
  const user = await userManager.getUser();
  return user && !user.expired ? user.access_token : undefined;
}

/** Where to return after login; only same-origin paths are accepted. */
export function sanitizeReturnPath(path: unknown): string | undefined {
  return typeof path === "string" && path.startsWith("/") && !path.startsWith("//") ? path : undefined;
}
