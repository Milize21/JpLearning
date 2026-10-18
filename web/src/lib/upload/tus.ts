import { Upload } from "tus-js-client";
import { currentAccessToken } from "@/lib/auth/oidc";

/** docs/05 §13: purposes of an upload; the server checks them against the role (BR-FILE-06). */
export type UploadPurpose = "material" | "book_cover" | "submission" | "question_import";

/** BR-FILE-01. */
export const maxUploadBytes = 1_073_741_824;

export interface UploadCallbacks {
  onProgress: (sent: number, total: number) => void;
  onSuccess: (fileId: string) => void;
  onError: (status: number | undefined) => void;
}

/**
 * Resumable upload to /api/content/uploads (docs/05 §13): 8 MB chunks, retries after lost connections, and the token is
 * refreshed before every request. A previous unfinished upload of the same file is continued instead of restarted.
 */
export function startUpload(file: File, purpose: UploadPurpose, callbacks: UploadCallbacks): Upload {
  const upload = new Upload(file, {
    endpoint: "/api/content/uploads",
    chunkSize: 8 * 1024 * 1024,
    retryDelays: [0, 1000, 3000, 5000, 10000],
    metadata: { filename: file.name, filetype: file.type || "application/octet-stream", purpose },
    removeFingerprintOnSuccess: true,
    async onBeforeRequest(request) {
      const token = await currentAccessToken();
      if (token) request.setHeader("Authorization", `Bearer ${token}`);
    },
    onProgress: callbacks.onProgress,
    onSuccess() {
      const url = upload.url ?? "";
      callbacks.onSuccess(url.slice(url.lastIndexOf("/") + 1));
    },
    onError(error) {
      const status = "originalResponse" in error ? error.originalResponse?.getStatus() : undefined;
      callbacks.onError(status);
    },
  });

  void upload.findPreviousUploads().then((previous) => {
    const last = previous.at(-1);
    if (last) upload.resumeFromPreviousUpload(last);
    upload.start();
  });
  return upload;
}
