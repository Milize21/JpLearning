import { useNavigate } from "react-router";
import { toast } from "sonner";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/misc";
import { errorLabel } from "@/i18n/labels/errors";
import { categoryLabels, masteryStatusLabels, summary as labels } from "@/i18n/labels/practice";
import { useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { useStartDrill, type DrillSummary } from "../api/practice";
import { HankoStamp } from "./HankoStamp";
import { MasteryItemRow } from "./MasteryItemRow";

/** End of a session (docs/07 §11 flow 3): count, accuracy, mastered and weak items, hanko stamp, "practise weak spots". */
export function DrillSummaryView({ summary }: { summary: DrillSummary }) {
  const t = useT();
  const navigate = useNavigate();
  const startDrill = useStartDrill();
  const pct = Math.round(summary.accuracy * 100);

  const practiseWeak = () => {
    startDrill.mutate(
      { category: summary.category, mode: "weak_focus" },
      {
        onSuccess: (started) => {
          void navigate(`/murid/latihan/${started.sessionId}`, {
            replace: true,
            state: { batch: started.batch, fallback: started.fallback },
          });
        },
        onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
      },
    );
  };

  return (
    <div className="mx-auto flex min-h-dvh max-w-2xl flex-col gap-6 px-4 py-8">
      <h1 className="text-h2 font-bold text-ink">
        <Label value={labels.title} companionClassName="text-body font-medium" />
      </h1>
      <Card className="flex flex-col items-center gap-4 sm:flex-row sm:justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-body-sm text-ink-muted">{t(categoryLabels[summary.category])}</span>
          <span className="text-h1 font-bold text-ink tabular-nums">{t(labels.accuracy, { pct })}</span>
          <span className="text-body text-ink">
            {t(labels.answered, { n: summary.answerCount })} · {summary.correctCount}/{summary.answerCount}
          </span>
        </div>
        {summary.answerCount > 0 && <HankoStamp accuracy={summary.accuracy} />}
      </Card>

      <section className="flex flex-col gap-2" aria-labelledby="summary-mastered">
        <h2 id="summary-mastered" className="text-body font-bold text-ink">
          <Label
            value={masteryStatusLabels.mastered}
            companion="inline"
            companionClassName="text-body-sm font-medium"
          />
        </h2>
        {summary.masteredItems.length === 0 ? (
          <p className="text-body-sm text-ink-muted">{t(labels.none)}</p>
        ) : (
          <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
            {summary.masteredItems.map((item) => (
              <MasteryItemRow key={`${item.itemType}:${item.itemId}`} item={item} />
            ))}
          </ul>
        )}
      </section>

      <section className="flex flex-col gap-2" aria-labelledby="summary-weak">
        <h2 id="summary-weak" className="text-body font-bold text-ink">
          <Label value={labels.stillWeak} companion="inline" companionClassName="text-body-sm font-medium" />
        </h2>
        {summary.weakItems.length === 0 ? (
          <p className="text-body-sm text-ink-muted">{t(labels.none)}</p>
        ) : (
          <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
            {summary.weakItems.map((item) => (
              <MasteryItemRow key={`${item.itemType}:${item.itemId}`} item={item} />
            ))}
          </ul>
        )}
      </section>

      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button
          variant="outline"
          onClick={() => {
            void navigate("/murid");
          }}
        >
          {t(labels.home)}
        </Button>
        <Button
          variant="outline"
          onClick={() => {
            void navigate("/murid/latihan");
          }}
        >
          {t(labels.again)}
        </Button>
        <Button variant="primary" loading={startDrill.isPending} onClick={practiseWeak}>
          {t(labels.practiceWeak)}
        </Button>
      </div>
    </div>
  );
}
