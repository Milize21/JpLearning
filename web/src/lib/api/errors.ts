/**
 * RFC 9457 Problem Details from the API (docs/05 §2) as a typed error. `code` is stable and maps to an errors.* label;
 * `detail` is English for developers and never shown raw to users.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
  readonly traceId: string | undefined;

  constructor(
    status: number,
    code: string,
    detail?: string,
    fieldErrors?: Record<string, string[]>,
    traceId?: string,
  ) {
    super(detail ?? code);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
    this.fieldErrors = fieldErrors ?? {};
    this.traceId = traceId;
  }
}

interface ProblemBody {
  code?: unknown;
  detail?: unknown;
  errors?: unknown;
  traceId?: unknown;
}

export async function toApiError(response: Response): Promise<ApiError> {
  let body: ProblemBody = {};
  try {
    body = (await response.clone().json()) as ProblemBody;
  } catch {
    // Not JSON (e.g. a proxy error page): fall back to the status.
  }
  const code = typeof body.code === "string" ? body.code : fallbackCode(response.status);
  return new ApiError(
    response.status,
    code,
    typeof body.detail === "string" ? body.detail : undefined,
    isFieldErrors(body.errors) ? body.errors : undefined,
    typeof body.traceId === "string" ? body.traceId : undefined,
  );
}

function fallbackCode(status: number): string {
  if (status === 401) return "unauthorized";
  if (status === 403) return "forbidden";
  if (status === 404) return "not_found";
  if (status === 429) return "rate_limited";
  return "internal_error";
}

function isFieldErrors(value: unknown): value is Record<string, string[]> {
  return (
    typeof value === "object" &&
    value !== null &&
    Object.values(value).every((messages) => Array.isArray(messages))
  );
}

/** Code of any error: API errors keep theirs, failed requests (offline, DNS) become `network`. */
export function errorCode(error: unknown): string {
  if (error instanceof ApiError) return error.code;
  if (error instanceof TypeError) return "network";
  return "internal_error";
}
