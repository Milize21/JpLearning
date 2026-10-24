import { Info } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Label } from "@/components/japanese/Label";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Alert, Skeleton } from "@/components/ui/misc";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { bank } from "@/i18n/labels/practice";
import { useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { usePreviewAnswer, useQuestionStudentView, type AnswerResult } from "../api/practice";
import { AnswerControls, type DrillAnswer } from "./AnswerControls";
import { AnswerFeedback } from "./AnswerFeedback";
import { QuestionPrompt, QuestionText } from "./QuestionText";

/**
 * The student version of a question (docs/03 §8.3), answerable without saving anything (BR-AKSES-05), so authors can
 * check that nothing leaks before publishing.
 */
export function QuestionPreviewDialog({
  questionId,
  onClose,
}: {
  questionId: string | null;
  onClose: () => void;
}) {
  const t = useT();
  return (
    <Dialog
      open={questionId !== null}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      {questionId !== null && (
        <DialogContent closeLabel={t(common.close)} aria-describedby={undefined} className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              <Label value={bank.preview} companion="inline" />
            </DialogTitle>
          </DialogHeader>
          <PreviewBody key={questionId} questionId={questionId} />
        </DialogContent>
      )}
    </Dialog>
  );
}

function PreviewBody({ questionId }: { questionId: string }) {
  const t = useT();
  const view = useQuestionStudentView(questionId);
  const preview = usePreviewAnswer();
  const [result, setResult] = useState<AnswerResult | null>(null);
  const [round, setRound] = useState(0);

  if (view.isPending) return <Skeleton className="h-48" />;
  if (!view.data) return null;
  const question = view.data;

  const answer = (value: DrillAnswer) => {
    preview.mutate(
      { id: questionId, answer: value },
      {
        onSuccess: setResult,
        onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
      },
    );
  };

  return (
    <div className="flex flex-col gap-4">
      <Alert tone="info" icon={Info}>
        {t(bank.previewNote)}
      </Alert>
      <QuestionPrompt value={question.prompt} />
      {question.stimulus && (
        <div className="flex justify-center py-2 text-center">
          <QuestionText value={question.stimulus} display={question.jaDisplay} large />
        </div>
      )}
      <AnswerControls
        key={round}
        question={question}
        disabled={result !== null || preview.isPending}
        onAnswer={answer}
      />
      {result && (
        <AnswerFeedback
          question={question}
          result={result}
          onNext={() => {
            setResult(null);
            setRound(round + 1);
          }}
        />
      )}
    </div>
  );
}
