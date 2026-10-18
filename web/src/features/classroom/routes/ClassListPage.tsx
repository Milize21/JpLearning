import { ChevronRight, Plus, School } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";
import { useCurrentUser } from "@/app/current-user";
import { roleHome } from "@/app/roles";
import { ContentText } from "@/components/japanese/ContentText";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Badge, Card, EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { useLevels } from "@/features/content/api/content";
import { classes } from "@/i18n/labels/classroom";
import { useT } from "@/i18n/prefs";
import { useClasses, type ClassDto } from "../api/classroom";
import { ClassFormDialog } from "../components/ClassFormDialog";

export function ClassStatusBadge({ status }: { status: ClassDto["status"] }) {
  const t = useT();
  if (status === "active") return <Badge tone="success">{t(classes.active)}</Badge>;
  if (status === "draft") return <Badge tone="warning">{t(classes.draft)}</Badge>;
  return <Badge>{t(classes.archived)}</Badge>;
}

/** Level and book names of a class, from the cached level list. */
export function ClassBookLine({ item }: { item: ClassDto }) {
  const levels = useLevels();
  const level = levels.data?.find((entry) => entry.id === item.levelId);
  const book = level?.books.find((entry) => entry.id === item.bookId);
  if (!level || !book) return null;
  return (
    <span className="flex flex-wrap items-center gap-1 text-body-sm text-ink-muted">
      <ContentText value={level.name} companion="none" />
      <span aria-hidden="true">·</span>
      <ContentText value={book.title} companion="none" />
    </span>
  );
}

/** /sensei/kelas, /admin/kelas and /murid/kelas — the classes the user teaches, manages or follows. */
export function ClassListPage() {
  const t = useT();
  const me = useCurrentUser();
  const list = useClasses();
  const [creating, setCreating] = useState(false);
  const isStaff = me.role !== "murid";
  const base = roleHome[me.role];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={isStaff ? classes.title : classes.myClasses} companionClassName="text-body font-medium" />}
        description={t(classes.description)}
        actions={
          isStaff && (
            <Button
              variant="primary"
              onClick={() => {
                setCreating(true);
              }}
            >
              <Plus aria-hidden="true" />
              {t(classes.create)}
            </Button>
          )
        }
      />
      {list.isPending && <Skeleton className="h-32" />}
      {list.data?.length === 0 && (
        <EmptyState icon={School} title={<Label value={classes.empty} />}>
          {isStaff && t(classes.emptyStaff)}
        </EmptyState>
      )}
      <ul className="grid gap-4 md:grid-cols-2">
        {list.data?.map((item) => (
          <li key={item.id}>
            <Link
              to={isStaff ? `${base}/kelas/${item.id}/bab` : `${base}/kelas/${item.id}`}
              className="block rounded-lg focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
            >
              <Card className="flex items-start gap-3 transition-shadow hover:shadow-sm">
                <div className="flex min-w-0 flex-1 flex-col gap-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="text-h3 font-semibold text-ink">{item.name}</h2>
                    {item.status !== "active" && <ClassStatusBadge status={item.status} />}
                  </div>
                  <ClassBookLine item={item} />
                  <p className="text-body-sm text-ink-muted">
                    {t(classes.students, { n: item.studentCount })} ·{" "}
                    {t(classes.teachersOf, { names: item.teachers.map((teacher) => teacher.displayName).join(", ") })}
                  </p>
                </div>
                <ChevronRight className="mt-1 size-5 shrink-0 text-ink-subtle" aria-hidden="true" />
              </Card>
            </Link>
          </li>
        ))}
      </ul>
      <ClassFormDialog open={creating} onOpenChange={setCreating} />
    </div>
  );
}
