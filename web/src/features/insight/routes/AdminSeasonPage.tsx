import { Trophy } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Badge, EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { Table, TD, TH, THead, TR } from "@/components/ui/table";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { seasons as labels } from "@/i18n/labels/insight";
import { usePrefs, useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/datetime";
import { useCloseSeason, useSeasons, type Season } from "../api/insight";

/** /admin/season — one active season per level; closing archives the ranking and opens the next (BR-SEASON-02). */
export function AdminSeasonPage() {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const list = useSeasons();
  const close = useCloseSeason();
  const [pending, setPending] = useState<Season | null>(null);

  const confirmClose = (season: Season) => {
    close.mutate(season.id, {
      onSuccess: (next) => {
        toast.success(t(labels.closedToast, { name: next.name }));
        setPending(null);
      },
      onError: (error) => {
        toast.error(t(errorLabel(errorCode(error))));
      },
    });
  };

  // Active seasons first, then the archive, newest first within each.
  const rows = [...(list.data ?? [])].sort(
    (a, b) =>
      Number(a.status !== "active") - Number(b.status !== "active") || b.startsAt.localeCompare(a.startsAt),
  );

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={labels.title} companionClassName="text-body font-medium" />}
        description={t(labels.description)}
      />
      {list.isPending && <Skeleton className="h-48" />}
      {list.data?.length === 0 && <EmptyState icon={Trophy} title={<Label value={labels.empty} />} />}
      {rows.length > 0 && (
        <Table>
          <THead>
            <TR>
              <TH>{t(labels.season)}</TH>
              <TH>{t(labels.status)}</TH>
              <TH>{t(labels.started)}</TH>
              <TH>{t(labels.ended)}</TH>
              <TH>
                <span className="sr-only">{t(labels.closeAction)}</span>
              </TH>
            </TR>
          </THead>
          <tbody>
            {rows.map((season) => (
              <TR key={season.id}>
                <TD className="font-semibold text-ink">{season.name}</TD>
                <TD>
                  <Badge tone={season.status === "active" ? "success" : "neutral"}>
                    {t(season.status === "active" ? labels.active : labels.closed)}
                  </Badge>
                </TD>
                <TD className="text-body-sm text-ink-muted">
                  {formatDateTime(season.startsAt, uiLocale, me.timezone)}
                </TD>
                <TD className="text-body-sm text-ink-muted">
                  {season.endsAt ? formatDateTime(season.endsAt, uiLocale, me.timezone) : "–"}
                </TD>
                <TD className="text-right">
                  {season.status === "active" && (
                    <Button
                      size="sm"
                      variant="destructive-outline"
                      onClick={() => {
                        setPending(season);
                      }}
                    >
                      {t(labels.closeAction)}
                    </Button>
                  )}
                </TD>
              </TR>
            ))}
          </tbody>
        </Table>
      )}

      <ConfirmDialog
        open={pending !== null}
        onOpenChange={(open) => {
          if (!open) setPending(null);
        }}
        title={t(labels.closeTitle, { name: pending?.name ?? "" })}
        description={t(labels.closeDescription)}
        confirmLabel={t(labels.closeConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        busy={close.isPending}
        destructive
        onConfirm={() => {
          if (pending) confirmClose(pending);
        }}
      />
    </div>
  );
}
