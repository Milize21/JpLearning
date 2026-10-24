import { BellOff, BellRing, CalendarSync, Copy, Link2Off, RefreshCw } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { FieldHint, Input } from "@/components/ui/form";
import { Alert, Card } from "@/components/ui/misc";
import { calendarFeed, reminders } from "@/i18n/labels/classroom";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { formatDate } from "@/lib/datetime";
import { useCalendarTokenStatus, useCreateCalendarToken, useRevokeCalendarToken } from "../api/classroom";
import { usePushReminders } from "../hooks/usePushReminders";

/** "Langganan kalender" card of docs/05 §12: the personal ICS URL is shown once (BR-JADWAL-04). */
export function CalendarFeedCard() {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const status = useCalendarTokenStatus();
  const create = useCreateCalendarToken();
  const revoke = useRevokeCalendarToken();
  const [url, setUrl] = useState<string | null>(null);
  const [confirmRevoke, setConfirmRevoke] = useState(false);
  const active = status.data?.active === true;

  const makeLink = () => {
    create.mutate(undefined, {
      onSuccess: (created) => {
        setUrl(created);
      },
      onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
    });
  };

  return (
    <Card className="flex flex-col gap-3">
      <h2 className="flex items-center gap-2 text-h3 font-semibold text-ink">
        <CalendarSync className="size-5" aria-hidden="true" />
        <Label value={calendarFeed.title} companion="inline" companionClassName="text-body-sm font-medium" />
      </h2>
      <p className="text-body-sm text-ink-muted">{t(calendarFeed.body)}</p>
      {url && (
        <div className="flex flex-col gap-2">
          <div className="flex gap-2">
            <Input
              readOnly
              value={url}
              aria-label={t(calendarFeed.title)}
              className="font-mono text-body-sm"
              onFocus={(event) => {
                event.target.select();
              }}
            />
            <Button
              variant="secondary"
              onClick={() => {
                void navigator.clipboard.writeText(url).then(() => toast.success(t(common.copied)));
              }}
            >
              <Copy aria-hidden="true" />
              {t(common.copy)}
            </Button>
          </div>
          <Alert tone="warning" icon={CalendarSync}>
            {t(calendarFeed.shownOnce)}
          </Alert>
          <FieldHint>{t(calendarFeed.guide)}</FieldHint>
        </div>
      )}
      {!url && active && status.data?.createdAt && (
        <p className="text-body-sm text-ink">
          {t(calendarFeed.active, { date: formatDate(status.data.createdAt, uiLocale, me.timezone) })}
        </p>
      )}
      <FieldHint>{t(calendarFeed.refreshNote)}</FieldHint>
      <div className="flex flex-wrap gap-2">
        <Button variant={active ? "outline" : "secondary"} loading={create.isPending} onClick={makeLink}>
          {active ? <RefreshCw aria-hidden="true" /> : <CalendarSync aria-hidden="true" />}
          {t(active ? calendarFeed.rotate : calendarFeed.create)}
        </Button>
        {active && (
          <Button
            variant="ghost"
            onClick={() => {
              setConfirmRevoke(true);
            }}
          >
            <Link2Off aria-hidden="true" />
            {t(calendarFeed.revoke)}
          </Button>
        )}
      </div>
      <ConfirmDialog
        open={confirmRevoke}
        onOpenChange={setConfirmRevoke}
        title={t(calendarFeed.revoke)}
        confirmLabel={t(calendarFeed.revoke)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        destructive
        busy={revoke.isPending}
        onConfirm={() => {
          revoke.mutate(undefined, {
            onSuccess: () => {
              setUrl(null);
              setConfirmRevoke(false);
              toast.success(t(calendarFeed.revoked));
            },
          });
        }}
      />
    </Card>
  );
}

/** "Aktifkan pengingat" card: web push on this device; email reminders need no action. */
export function RemindersCard() {
  const t = useT();
  const push = usePushReminders();

  return (
    <Card className="flex flex-col gap-3">
      <h2 className="flex items-center gap-2 text-h3 font-semibold text-ink">
        <BellRing className="size-5" aria-hidden="true" />
        <Label value={reminders.title} companion="inline" companionClassName="text-body-sm font-medium" />
      </h2>
      <p className="text-body-sm text-ink-muted">{t(reminders.body)}</p>
      {push.state === "unsupported" && <Alert icon={BellOff}>{t(reminders.unsupported)}</Alert>}
      {push.state === "denied" && (
        <Alert tone="warning" icon={BellOff}>
          {t(reminders.denied)}
        </Alert>
      )}
      {push.state === "unavailable" && <Alert icon={BellOff}>{t(reminders.unavailable)}</Alert>}
      {push.state === "on" && (
        <Alert tone="success" icon={BellRing}>
          {t(reminders.enabled)}
        </Alert>
      )}
      <div className="flex flex-wrap gap-2">
        {push.state === "off" && (
          <Button
            variant="secondary"
            loading={push.busy}
            onClick={() => {
              void push
                .enable()
                .then(() => toast.success(t(reminders.turnedOn)))
                .catch((error: unknown) => toast.error(t(errorLabel(errorCode(error)))));
            }}
          >
            <BellRing aria-hidden="true" />
            {t(reminders.enable)}
          </Button>
        )}
        {push.state === "on" && (
          <Button
            variant="ghost"
            loading={push.busy}
            onClick={() => {
              void push.disable().then(() => toast.success(t(reminders.turnedOff)));
            }}
          >
            <BellOff aria-hidden="true" />
            {t(reminders.disable)}
          </Button>
        )}
      </div>
      <FieldHint>{t(reminders.emailNote)}</FieldHint>
    </Card>
  );
}
