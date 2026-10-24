import { Archive, ChevronLeft, CircleAlert, Power } from "lucide-react";
import { useState } from "react";
import { Link, Navigate, useParams } from "react-router";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { roleHome } from "@/app/roles";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Alert, EmptyState, Skeleton } from "@/components/ui/misc";
import { TabNav } from "@/components/ui/tab-nav";
import { AssignmentsPanel } from "@/features/practice/components/AssignmentsPanel";
import { ClassProgressPanel } from "@/features/practice/components/ClassProgressPanel";
import { GradesPanel } from "@/features/practice/components/GradesPanel";
import { QuizzesPanel } from "@/features/practice/components/QuizzesPanel";
import { nav } from "@/i18n/labels/nav";
import { classes, classTabs } from "@/i18n/labels/classroom";
import { progress } from "@/i18n/labels/practice";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { useArchiveClass, useClass, useUpdateClass, type ClassDto } from "../api/classroom";
import { ChaptersPanel } from "../components/ChaptersPanel";
import { ClassSchedulePanel } from "../components/ClassSchedulePanel";
import { MaterialsPanel } from "../components/MaterialsPanel";
import { MembersPanel } from "../components/MembersPanel";
import { ClassBookLine, ClassStatusBadge } from "./ClassListPage";

const tabs = ["bab", "materi", "anggota", "jadwal", "kuis", "tugas", "nilai", "progres"] as const;
type Tab = (typeof tabs)[number];
const tabLabels = {
  bab: classTabs.chapters,
  materi: classTabs.materials,
  anggota: classTabs.members,
  jadwal: classTabs.schedule,
  kuis: nav.quizzes,
  tugas: nav.assignments,
  nilai: nav.grades,
  progres: progress.tab,
} as const;

const isTab = (value: string | undefined): value is Tab => tabs.includes(value as Tab);

/** /sensei/kelas/:classId/:tab and /admin/kelas/:classId/:tab (docs/07 §12). */
export function ClassDetailPage() {
  const t = useT();
  const me = useCurrentUser();
  const { classId = "", tab } = useParams();
  const query = useClass(classId);
  const base = `${roleHome[me.role]}/kelas`;

  if (!isTab(tab)) return <Navigate to={`${base}/${classId}/bab`} replace />;
  if (query.isPending) return <Skeleton className="h-64" />;
  if (query.isError) {
    return (
      <EmptyState
        icon={CircleAlert}
        title={t(errorLabel(errorCode(query.error)))}
        action={
          <Link to={base} className="text-body-sm font-semibold text-ink underline underline-offset-4">
            {t(classes.backToClasses)}
          </Link>
        }
      />
    );
  }

  const classInfo = query.data;
  return (
    <div className="flex flex-col gap-6">
      <ClassHeader classInfo={classInfo} base={base} />
      {classInfo.status === "archived" && (
        <Alert icon={Archive} tone="info">
          {t(classes.archivedNote)}
        </Alert>
      )}
      <TabNav
        label={classInfo.name}
        items={tabs.map((item) => ({ to: `${base}/${classInfo.id}/${item}`, label: t(tabLabels[item]) }))}
      />
      {tab === "bab" && <ChaptersPanel classInfo={classInfo} />}
      {tab === "materi" && <MaterialsPanel classInfo={classInfo} />}
      {tab === "anggota" && <MembersPanel classInfo={classInfo} />}
      {tab === "jadwal" && <ClassSchedulePanel classInfo={classInfo} manage headingLevel="h2" />}
      {tab === "kuis" && <QuizzesPanel classInfo={classInfo} />}
      {tab === "tugas" && <AssignmentsPanel classInfo={classInfo} />}
      {tab === "nilai" && <GradesPanel classInfo={classInfo} />}
      {tab === "progres" && <ClassProgressPanel classId={classInfo.id} />}
    </div>
  );
}

function ClassHeader({ classInfo, base }: { classInfo: ClassDto; base: string }) {
  const t = useT();
  const archive = useArchiveClass(classInfo.id);
  const update = useUpdateClass(classInfo.id);
  const [archiving, setArchiving] = useState(false);

  return (
    <div className="flex flex-col gap-3">
      <Link
        to={base}
        className="inline-flex items-center gap-1 self-start rounded-sm text-body-sm text-ink-muted hover:text-ink focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
      >
        <ChevronLeft className="size-4" aria-hidden="true" />
        {t(classes.backToClasses)}
      </Link>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-h2 font-bold text-ink sm:text-h1">{classInfo.name}</h1>
            {classInfo.status !== "active" && <ClassStatusBadge status={classInfo.status} />}
          </div>
          <ClassBookLine item={classInfo} />
        </div>
        <div className="flex flex-wrap gap-2">
          {classInfo.status === "draft" && (
            <Button
              variant="secondary"
              loading={update.isPending}
              onClick={() => {
                update.mutate(
                  { status: "active" },
                  {
                    onSuccess: () => toast.success(t(common.saved)),
                    onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
                  },
                );
              }}
            >
              <Power aria-hidden="true" />
              {t(classes.activate)}
            </Button>
          )}
          {classInfo.status !== "archived" && (
            <Button
              variant="destructive-outline"
              onClick={() => {
                setArchiving(true);
              }}
            >
              <Archive aria-hidden="true" />
              {t(classes.archive)}
            </Button>
          )}
        </div>
      </div>
      <ConfirmDialog
        open={archiving}
        onOpenChange={setArchiving}
        title={t(classes.archiveTitle, { name: classInfo.name })}
        description={t(classes.archiveBody)}
        confirmLabel={t(classes.archiveConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        destructive
        busy={archive.isPending}
        onConfirm={() => {
          archive.mutate(undefined, {
            onSuccess: () => {
              toast.success(t(classes.archived));
              setArchiving(false);
            },
            onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
          });
        }}
      />
    </div>
  );
}
