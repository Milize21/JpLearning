import { GraduationCap } from "lucide-react";
import { Label } from "@/components/japanese/Label";
import { Card, EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { grades as labels } from "@/i18n/labels/assessment";
import { useT } from "@/i18n/prefs";
import { useMyGrades } from "../api/assessment";
import { GradeBadge, GradeBreakdownList } from "../components/GradeParts";

/** /murid/nilai — the 0–4 grade per class with its components (BR-NILAI). */
export function MyGradesPage() {
  const t = useT();
  const list = useMyGrades();

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={labels.myTitle} companionClassName="text-body font-medium" />}
        description={t(labels.description)}
      />
      {list.isPending && <Skeleton className="h-40" />}
      {list.data?.length === 0 && <EmptyState icon={GraduationCap} title={<Label value={labels.empty} />} />}
      <ul className="grid gap-4 md:grid-cols-2">
        {list.data?.map((grade) => (
          <li key={grade.classId}>
            <Card className="flex flex-col gap-4">
              <div className="flex items-start justify-between gap-3">
                <h2 className="text-h3 font-semibold text-ink">{grade.className}</h2>
                <GradeBadge gpa={grade.gpa} finalPct={grade.finalPct} />
              </div>
              <GradeBreakdownList breakdown={grade.breakdown} />
            </Card>
          </li>
        ))}
      </ul>
    </div>
  );
}
