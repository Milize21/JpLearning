import { CircleAlert, GraduationCap, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { FieldHint, FieldLabel, Input } from "@/components/ui/form";
import { Alert, Card, EmptyState, Skeleton } from "@/components/ui/misc";
import type { ClassDto } from "@/features/classroom/api/classroom";
import { grades as labels } from "@/i18n/labels/assessment";
import { errorLabel } from "@/i18n/labels/errors";
import { useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { useClassGrades, useGradeScheme, useSaveGradeScheme, type GradeScheme } from "../api/assessment";
import { GradeBadge } from "./GradeParts";

const pct = (value: number | null) => (value === null ? "–" : `${value.toFixed(2)}%`);

/** "Nilai" tab of a class (docs/07 §11): grades 0–4 per student with components, and the grade scheme (BR-NILAI-06). */
export function GradesPanel({ classInfo }: { classInfo: ClassDto }) {
  const t = useT();
  const list = useClassGrades(classInfo.id);
  const scheme = useGradeScheme(classInfo.id);

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <section className="flex flex-col gap-3" aria-labelledby="grades-heading">
        <h2 id="grades-heading" className="text-body font-bold text-ink">
          <Label value={labels.title} companion="inline" companionClassName="text-body-sm font-medium" />
        </h2>
        <p className="text-body-sm text-ink-muted">{t(labels.description)}</p>
        {list.isPending && <Skeleton className="h-40" />}
        {list.data?.length === 0 && (
          <EmptyState icon={GraduationCap} title={<Label value={labels.empty} />} />
        )}
        {list.data && list.data.length > 0 && (
          <div className="overflow-x-auto rounded-lg border border-line bg-surface">
            <table className="w-full text-body-sm">
              <thead className="bg-canvas-subtle text-left text-ink-muted">
                <tr>
                  <th scope="col" className="px-3 py-2 font-semibold">
                    {t(labels.student)}
                  </th>
                  <th scope="col" className="px-3 py-2 text-right font-semibold">
                    {t(labels.quizAvg)}
                  </th>
                  <th scope="col" className="px-3 py-2 text-right font-semibold">
                    {t(labels.examAvg)}
                  </th>
                  <th scope="col" className="px-3 py-2 text-right font-semibold">
                    {t(labels.assignmentAvg)}
                  </th>
                  <th scope="col" className="px-3 py-2 text-right font-semibold">
                    {t(labels.final)}
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {list.data.map((row) => (
                  <tr key={row.studentId}>
                    <th scope="row" className="px-3 py-2 text-left font-semibold text-ink">
                      {row.studentName}
                    </th>
                    <td className="px-3 py-2 text-right tabular-nums">{pct(row.breakdown.quizAvg)}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{pct(row.breakdown.examAvg)}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{pct(row.breakdown.assignmentAvg)}</td>
                    <td className="px-3 py-2 text-right">
                      <GradeBadge gpa={row.gpa} finalPct={row.finalPct} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      {scheme.data && classInfo.status !== "archived" && (
        <SchemeEditor key={scheme.data.version} classId={classInfo.id} scheme={scheme.data} />
      )}
    </div>
  );
}

function SchemeEditor({ classId, scheme }: { classId: string; scheme: GradeScheme }) {
  const t = useT();
  const save = useSaveGradeScheme(classId);
  const [weights, setWeights] = useState({
    quiz: scheme.wQuiz,
    exam: scheme.wExam,
    assignment: scheme.wAssignment,
  });
  const [scale, setScale] = useState(scheme.scaleMap.map((step) => ({ min: step.min, gpa: step.gpa })));
  const [error, setError] = useState<string | null>(null);
  const sum = weights.quiz + weights.exam + weights.assignment;
  const weightsOk = sum === 100 && weights.quiz >= 0 && weights.exam >= 0 && weights.assignment >= 0;

  const number = (value: string) => (value === "" ? 0 : Number(value));

  return (
    <Card className="flex flex-col gap-4 self-start">
      <h2 className="text-body font-bold text-ink">
        <Label value={labels.scheme} companion="inline" companionClassName="text-body-sm font-medium" />
      </h2>
      {scheme.isDefault && <p className="text-caption text-ink-muted">{t(labels.defaultScheme)}</p>}
      <FieldHint>{t(labels.schemeHint)}</FieldHint>
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault();
          setError(null);
          if (!weightsOk) {
            setError("weights");
            return;
          }
          save.mutate(
            {
              wQuiz: weights.quiz,
              wExam: weights.exam,
              wAssignment: weights.assignment,
              scaleMap: scale,
              version: scheme.version,
            },
            {
              onSuccess: () => toast.success(t(labels.schemeSaved)),
              onError: (failure) => {
                setError(errorCode(failure));
              },
            },
          );
        }}
      >
        <div className="grid grid-cols-3 gap-2">
          {(
            [
              ["quiz", labels.wQuiz],
              ["exam", labels.wExam],
              ["assignment", labels.wAssignment],
            ] as const
          ).map(([key, label]) => (
            <div key={key} className="flex flex-col gap-1.5">
              <FieldLabel htmlFor={`weight-${key}`}>{t(label)}</FieldLabel>
              <Input
                id={`weight-${key}`}
                type="number"
                min={0}
                max={100}
                value={weights[key]}
                aria-invalid={!weightsOk ? true : undefined}
                onChange={(event) => {
                  setWeights({ ...weights, [key]: number(event.target.value) });
                }}
              />
            </div>
          ))}
        </div>
        <table className="w-full text-body-sm">
          <thead className="text-left text-ink-muted">
            <tr>
              <th scope="col" className="pb-1 font-semibold">
                {t(labels.scaleMin)}
              </th>
              <th scope="col" className="pb-1 font-semibold">
                {t(labels.scaleGpa)}
              </th>
              <th scope="col" className="pb-1">
                <span className="sr-only">{t(labels.addStep)}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {scale.map((step, index) => (
              <tr key={index}>
                <td className="py-1 pr-2">
                  <Input
                    aria-label={`${t(labels.scaleMin)} ${String(index + 1)}`}
                    type="number"
                    min={0}
                    max={100}
                    value={step.min}
                    onChange={(event) => {
                      setScale(
                        scale.map((entry, position) =>
                          position === index ? { ...entry, min: number(event.target.value) } : entry,
                        ),
                      );
                    }}
                  />
                </td>
                <td className="py-1 pr-2">
                  <Input
                    aria-label={`${t(labels.scaleGpa)} ${String(index + 1)}`}
                    type="number"
                    min={0}
                    max={4}
                    step={0.5}
                    value={step.gpa}
                    onChange={(event) => {
                      setScale(
                        scale.map((entry, position) =>
                          position === index ? { ...entry, gpa: number(event.target.value) } : entry,
                        ),
                      );
                    }}
                  />
                </td>
                <td className="py-1">
                  <Button
                    variant="ghost"
                    size="icon"
                    type="button"
                    aria-label={`${t(labels.scaleMin)} ${String(index + 1)} ×`}
                    disabled={scale.length <= 1}
                    onClick={() => {
                      setScale(scale.filter((_, position) => position !== index));
                    }}
                  >
                    <Trash2 aria-hidden="true" />
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <Button
          variant="outline"
          size="sm"
          type="button"
          className="self-start"
          onClick={() => {
            setScale([...scale, { min: 0, gpa: 0 }]);
          }}
        >
          <Plus aria-hidden="true" />
          {t(labels.addStep)}
        </Button>
        {error && (
          <Alert tone="danger" icon={CircleAlert}>
            {error === "weights" ? t(labels.weightsInvalid) : t(errorLabel(error))}
          </Alert>
        )}
        <Button variant="secondary" type="submit" loading={save.isPending}>
          {t(labels.saveScheme)}
        </Button>
      </form>
    </Card>
  );
}
