import { CircleAlert, CircleCheck, Pause, Play, Upload as UploadIcon, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import type { Upload } from "tus-js-client";
import { Button } from "@/components/ui/button";
import { FieldHint } from "@/components/ui/form";
import { common } from "@/i18n/labels/common";
import { upload as labels } from "@/i18n/labels/content";
import { errors as errorLabels } from "@/i18n/labels/errors";
import { useT } from "@/i18n/prefs";
import { maxUploadBytes, startUpload, type UploadPurpose } from "@/lib/upload/tus";

type State =
  | { phase: "idle" }
  | { phase: "uploading" | "paused"; name: string; percent: number }
  | { phase: "done"; name: string }
  | { phase: "error"; code: "file_too_large" | "rate_limited" | "forbidden" | "failed" };

/**
 * UploadDropzone of docs/07 §10: choose a file, see progress, pause and resume (BR-FILE-03). Calls onUploaded with the
 * stored file id once the server marked it ready.
 */
export function UploadField({
  id,
  purpose,
  onUploaded,
  onCleared,
}: {
  id: string;
  purpose: UploadPurpose;
  onUploaded: (fileId: string, name: string) => void;
  onCleared?: () => void;
}) {
  const t = useT();
  const [state, setState] = useState<State>({ phase: "idle" });
  const upload = useRef<Upload | null>(null);

  useEffect(
    () => () => {
      void upload.current?.abort();
    },
    [],
  );

  const choose = (file: File) => {
    if (file.size > maxUploadBytes) {
      setState({ phase: "error", code: "file_too_large" });
      return;
    }
    setState({ phase: "uploading", name: file.name, percent: 0 });
    upload.current = startUpload(file, purpose, {
      onProgress: (sent, total) => {
        setState((current) =>
          current.phase === "paused"
            ? current
            : { phase: "uploading", name: file.name, percent: Math.floor((sent / total) * 100) },
        );
      },
      onSuccess: (fileId) => {
        setState({ phase: "done", name: file.name });
        onUploaded(fileId, file.name);
      },
      onError: (status) => {
        setState({
          phase: "error",
          code:
            status === 413
              ? "file_too_large"
              : status === 429
                ? "rate_limited"
                : status === 403
                  ? "forbidden"
                  : "failed",
        });
      },
    });
  };

  const reset = () => {
    void upload.current?.abort();
    upload.current = null;
    setState({ phase: "idle" });
    onCleared?.();
  };

  if (state.phase === "idle" || state.phase === "error") {
    return (
      <div className="flex flex-col gap-2">
        <label
          htmlFor={id}
          className="flex cursor-pointer flex-col items-center gap-2 rounded-lg border border-dashed border-line-strong px-4 py-6 text-center text-body-sm text-ink-muted hover:bg-canvas-subtle focus-within:ring-2 focus-within:ring-ring"
        >
          <UploadIcon className="size-6 text-ink-subtle" aria-hidden="true" />
          <span className="font-semibold text-ink">{t(labels.choose)}</span>
          <input
            id={id}
            type="file"
            className="sr-only"
            onChange={(event) => {
              const file = event.target.files?.[0];
              if (file) choose(file);
              event.target.value = "";
            }}
          />
        </label>
        <FieldHint>{t(labels.maxSize)}</FieldHint>
        {state.phase === "error" && (
          <p role="alert" className="flex items-center gap-1 text-body-sm text-danger">
            <CircleAlert className="size-4 shrink-0" aria-hidden="true" />
            {state.code === "failed" ? t(labels.failed) : t(errorLabels[state.code])}
          </p>
        )}
      </div>
    );
  }

  if (state.phase === "done") {
    return (
      <div className="flex items-center justify-between gap-3 rounded-lg border border-line px-4 py-3 text-body-sm">
        <span className="flex min-w-0 items-center gap-2 text-ink">
          <CircleCheck className="size-5 shrink-0 text-success" aria-hidden="true" />
          <span className="truncate">{t(labels.done, { name: state.name })}</span>
        </span>
        <Button variant="ghost" size="sm" onClick={reset}>
          {t(labels.remove)}
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-line px-4 py-3" aria-live="polite">
      <div className="flex items-center justify-between gap-2 text-body-sm">
        <span className="truncate text-ink">
          {state.phase === "paused"
            ? t(labels.paused, { percent: state.percent })
            : t(labels.progress, { name: state.name, percent: state.percent })}
        </span>
        <span className="flex shrink-0 gap-1">
          {state.phase === "uploading" ? (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                void upload.current?.abort();
                setState({ ...state, phase: "paused" });
              }}
            >
              <Pause aria-hidden="true" />
              {t(labels.pause)}
            </Button>
          ) : (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                upload.current?.start();
                setState({ ...state, phase: "uploading" });
              }}
            >
              <Play aria-hidden="true" />
              {t(labels.resume)}
            </Button>
          )}
          <Button variant="ghost" size="sm" onClick={reset} aria-label={t(common.cancel)}>
            <X aria-hidden="true" />
          </Button>
        </span>
      </div>
      <div
        className="h-2 overflow-hidden rounded-full bg-sumi-200"
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={state.percent}
        aria-label={state.name}
      >
        <div className="h-full bg-action transition-all" style={{ width: `${state.percent}%` }} />
      </div>
    </div>
  );
}
