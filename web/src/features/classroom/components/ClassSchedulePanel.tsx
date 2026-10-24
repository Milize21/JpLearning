import { CalendarDays, Plus } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { EmptyState, Skeleton } from "@/components/ui/misc";
import { schedule } from "@/i18n/labels/classroom";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { useCancelSession, useClassSessions, type ClassDto, type SessionDto } from "../api/classroom";
import { groupLabels, groupOrder, groupSessions, useNow } from "../hooks/schedule";
import { ScheduleItem } from "./ScheduleItem";
import { SessionFormDialog } from "./SessionFormDialog";

/**
 * "Jadwal" tab of a class. Teachers schedule, change and cancel sessions (BR-JADWAL-01/04); students only read them.
 */
export function ClassSchedulePanel({
  classInfo,
  manage,
  headingLevel = "h3",
}: {
  classInfo: ClassDto;
  manage: boolean;
  /** Day group headings sit one level below the surrounding heading. */
  headingLevel?: "h2" | "h3";
}) {
  const Heading = headingLevel;
  const t = useT();
  const me = useCurrentUser();
  const sessions = useClassSessions(classInfo.id);
  const cancel = useCancelSession();
  const now = useNow();
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<SessionDto | undefined>(undefined);
  const [cancelling, setCancelling] = useState<SessionDto | null>(null);
  const [showPast, setShowPast] = useState(false);
  const canManage = manage && classInfo.status !== "archived";
  const groups = groupSessions(sessions.data ?? [], now, me.timezone);
  const upcoming = groupOrder.some((group) => groups[group].length > 0);

  const edit = canManage
    ? (session: SessionDto) => {
        setEditing(session);
        setFormOpen(true);
      }
    : undefined;

  return (
    <div className="flex flex-col gap-4">
      {canManage && (
        <Button
          variant="primary"
          className="self-start"
          onClick={() => {
            setEditing(undefined);
            setFormOpen(true);
          }}
        >
          <Plus aria-hidden="true" />
          {t(schedule.add)}
        </Button>
      )}
      {sessions.isPending && <Skeleton className="h-32" />}
      {sessions.data && !upcoming && (
        <EmptyState icon={CalendarDays} title={<Label value={schedule.empty} />}>
          {!canManage && t(schedule.emptyBody)}
        </EmptyState>
      )}
      {groupOrder.map((group) =>
        groups[group].length === 0 ? null : (
          <section key={group} className="flex flex-col gap-3" aria-label={t(groupLabels[group])}>
            <Heading className="text-body font-bold text-ink">
              <Label
                value={groupLabels[group]}
                companion="inline"
                companionClassName="text-body-sm font-medium"
              />
            </Heading>
            {groups[group].map((session) => (
              <ScheduleItem
                key={session.id}
                session={session}
                now={now}
                onEdit={session.status === "scheduled" ? edit : undefined}
                onCancel={canManage && session.status === "scheduled" ? setCancelling : undefined}
              />
            ))}
          </section>
        ),
      )}
      {groups.past.length > 0 && (
        <section className="flex flex-col gap-3">
          <Button
            variant="ghost"
            className="self-start"
            aria-expanded={showPast}
            onClick={() => {
              setShowPast(!showPast);
            }}
          >
            {t(schedule.showPast)}
          </Button>
          {showPast &&
            groups.past.map((session) => <ScheduleItem key={session.id} session={session} now={now} />)}
        </section>
      )}
      {canManage && (
        <SessionFormDialog
          classInfo={classInfo}
          session={editing}
          open={formOpen}
          onOpenChange={setFormOpen}
        />
      )}
      <ConfirmDialog
        open={cancelling !== null}
        onOpenChange={(open) => {
          if (!open) setCancelling(null);
        }}
        title={t(schedule.cancelTitle, { topic: cancelling?.topic ?? "" })}
        description={t(schedule.cancelBody)}
        confirmLabel={t(schedule.cancelConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        destructive
        busy={cancel.isPending}
        onConfirm={() => {
          if (!cancelling) return;
          cancel.mutate(cancelling.id, {
            onSuccess: () => {
              toast.success(t(schedule.cancelled));
              setCancelling(null);
            },
            onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
          });
        }}
      />
    </div>
  );
}
