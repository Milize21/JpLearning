import createClient, { type Middleware } from "openapi-fetch";
import { currentAccessToken, userManager } from "@/lib/auth/oidc";
import { toApiError } from "./errors";
import type { paths as ClassroomPaths } from "./schema/classroom";
import type { paths as ContentPaths } from "./schema/content";
import type { paths as IdentityPaths } from "./schema/identity";
import type { paths as PracticePaths } from "./schema/practice";

/**
 * Typed API clients per module, generated from OpenAPI (docs/05 §14). Requests go to the same origin
 * (Vite proxy in development, Gateway in production).
 */
const auth: Middleware = {
  async onRequest({ request }) {
    const token = await currentAccessToken();
    if (token) request.headers.set("Authorization", `Bearer ${token}`);
    return request;
  },
  async onResponse({ request, response }) {
    if (response.ok) return response;

    // One silent renewal on 401, then back to the login page (docs/05 §14). Only reads are replayed: a request
    // body is consumed by the first attempt, and the user can simply repeat a write.
    if (response.status === 401 && !request.headers.has("X-Nomax-Retried")) {
      let renewed = false;
      try {
        renewed = (await userManager.signinSilent()) !== null;
      } catch {
        renewed = false;
      }

      if (!renewed) {
        await userManager.signinRedirect({
          state: { returnTo: window.location.pathname + window.location.search },
        });
      } else if (request.method === "GET" || request.method === "HEAD") {
        const retry = new Request(request, { headers: new Headers(request.headers) });
        const token = await currentAccessToken();
        if (token) retry.headers.set("Authorization", `Bearer ${token}`);
        retry.headers.set("X-Nomax-Retried", "1");
        const retried = await globalThis.fetch(retry);
        if (retried.ok) return retried;
        throw await toApiError(retried);
      }
    }

    throw await toApiError(response);
  },
};

export const identityApi = createClient<IdentityPaths>({ baseUrl: window.location.origin });
identityApi.use(auth);

export const contentApi = createClient<ContentPaths>({ baseUrl: window.location.origin });
contentApi.use(auth);

export const classroomApi = createClient<ClassroomPaths>({ baseUrl: window.location.origin });
classroomApi.use(auth);

export const practiceApi = createClient<PracticePaths>({ baseUrl: window.location.origin });
practiceApi.use(auth);

