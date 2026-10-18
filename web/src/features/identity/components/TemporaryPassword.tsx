import { Copy, KeyRound, TriangleAlert } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Alert } from "@/components/ui/misc";
import { common } from "@/i18n/labels/common";
import { users } from "@/i18n/labels/identity";
import { useT } from "@/i18n/prefs";

/** A temporary password shown exactly once (BR-AKUN-02 step 5, BR-AKUN-04). It is never stored by the app. */
export function TemporaryPassword({ username, password }: { username: string; password: string }) {
  const t = useT();
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(password);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-1 rounded-lg border border-line bg-canvas-subtle p-4">
        <span className="flex items-center gap-2 text-body-sm text-ink-muted">
          <KeyRound className="size-4" aria-hidden="true" />
          {t(users.tempPasswordTitle)} · {username}
        </span>
        <div className="flex items-center justify-between gap-2">
          <code
            className="font-mono text-h3 font-semibold tracking-wider break-all"
            data-testid="temporary-password"
          >
            {password}
          </code>
          <Button variant="outline" size="sm" onClick={() => void copy()}>
            <Copy aria-hidden="true" />
            {t(copied ? common.copied : common.copy)}
          </Button>
        </div>
      </div>
      <Alert tone="warning" icon={TriangleAlert}>
        {t(users.tempPasswordWarning)}
      </Alert>
    </div>
  );
}
