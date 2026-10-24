import { ChevronLeft, CircleAlert } from "lucide-react";
import { Link, useParams } from "react-router";
import { Label } from "@/components/japanese/Label";
import { EmptyState, Skeleton } from "@/components/ui/misc";
import { useBook } from "@/features/content/api/content";
import { ChapterCard } from "@/features/content/components/ChapterCard";
import { classes, classTabs } from "@/i18n/labels/classroom";
import { errorLabel } from "@/i18n/labels/errors";
import { useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { useClass } from "../api/classroom";
import { ClassSchedulePanel } from "../components/ClassSchedulePanel";
import { ClassBookLine } from "./ClassListPage";

/** /murid/kelas/:classId — chapters of the class (open or locked) and its sessions. */
export function StudentClassPage() {
  const t = useT();
  const { classId } = useParams();
  const query = useClass(classId);
  const book = useBook(query.data?.bookId);

  const back = (
    <Link
      to="/murid/kelas"
      className="inline-flex items-center gap-1 self-start rounded-sm text-body-sm text-ink-muted hover:text-ink focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
    >
      <ChevronLeft className="size-4" aria-hidden="true" />
      {t(classes.backToClasses)}
    </Link>
  );

  if (query.isPending) return <Skeleton className="h-64" />;
  if (query.isError) {
    return (
      <div className="flex flex-col gap-6">
        {back}
        <EmptyState icon={CircleAlert} title={t(errorLabel(errorCode(query.error)))} />
      </div>
    );
  }

  const classInfo = query.data;
  return (
    <div className="flex flex-col gap-6">
      {back}
      <div className="flex flex-col gap-1">
        <h1 className="text-h2 font-bold text-ink sm:text-h1">{classInfo.name}</h1>
        <ClassBookLine item={classInfo} />
        <p className="text-body-sm text-ink-muted">
          {t(classes.teachersOf, {
            names: classInfo.teachers.map((teacher) => teacher.displayName).join(", "),
          })}
        </p>
      </div>
      <div className="grid gap-6 lg:grid-cols-2">
        <section className="flex flex-col gap-3" aria-labelledby="class-chapters">
          <h2 id="class-chapters" className="text-body font-bold text-ink">
            <Label
              value={classTabs.chapters}
              companion="inline"
              companionClassName="text-body-sm font-medium"
            />
          </h2>
          {book.isPending && <Skeleton className="h-40" />}
          <ol className="flex flex-col gap-2">
            {book.data?.chapters.map((chapter) => (
              <li key={chapter.id}>
                <ChapterCard chapter={chapter} />
              </li>
            ))}
          </ol>
        </section>
        <section className="flex flex-col gap-3" aria-labelledby="class-schedule">
          <h2 id="class-schedule" className="text-body font-bold text-ink">
            <Label
              value={classTabs.schedule}
              companion="inline"
              companionClassName="text-body-sm font-medium"
            />
          </h2>
          <ClassSchedulePanel classInfo={classInfo} manage={false} />
        </section>
      </div>
    </div>
  );
}
