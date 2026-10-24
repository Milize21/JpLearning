import { CircleAlert } from "lucide-react";
import { useState } from "react";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldLabel, Select } from "@/components/ui/form";
import { Alert } from "@/components/ui/misc";
import { useLevels } from "@/features/content/api/content";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { generator } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { useRunGenerator, type GeneratorTemplate } from "../api/practice";

const templates: readonly GeneratorTemplate[] = [
  "kana_to_romaji",
  "romaji_to_kana",
  "kana_type_romaji",
  "vocab_to_meaning",
  "meaning_to_vocab",
  "vocab_type_reading",
  "kanji_to_reading",
  "kanji_to_meaning",
  "sentence_tap",
];

/** Admin: BR-SOAL-05 generator run for a level; results land in review and are published from the bank. */
export function GeneratorDialog({
  open,
  onOpenChange,
  onDone,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDone: (levelId: string) => void;
}) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const levels = useLevels();
  const run = useRunGenerator();
  const [levelId, setLevelId] = useState("");
  const [chosen, setChosen] = useState<GeneratorTemplate[]>(["kana_to_romaji", "romaji_to_kana"]);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t(common.close)}>
        <DialogHeader>
          <DialogTitle>
            <Label value={generator.title} companion="inline" />
          </DialogTitle>
          <DialogDescription>{t(generator.description)}</DialogDescription>
        </DialogHeader>
        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            setError(null);
            run.mutate(
              { levelId, templates: chosen },
              {
                onSuccess: (result) => {
                  setMessage(t(generator.result, { created: result.created, skipped: result.skipped }));
                },
                onError: (failure) => {
                  setError(errorCode(failure));
                },
              },
            );
          }}
        >
          <Field>
            <FieldLabel htmlFor="gen-level">{t(generator.level)}</FieldLabel>
            <Select
              id="gen-level"
              value={levelId}
              onChange={(event) => {
                setLevelId(event.target.value);
                setMessage(null);
              }}
            >
              <option value="">—</option>
              {levels.data?.map((level) => (
                <option key={level.id} value={level.id}>
                  {textIn(level.name, uiLocale)}
                </option>
              ))}
            </Select>
          </Field>
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-1 text-body-sm font-semibold text-ink">{t(generator.templates)}</legend>
            {templates.map((template) => (
              <label key={template} className="flex items-center gap-2 text-body text-ink">
                <input
                  type="checkbox"
                  className="size-4 accent-sumi-900"
                  checked={chosen.includes(template)}
                  onChange={(event) => {
                    setChosen(
                      event.target.checked
                        ? [...chosen, template]
                        : chosen.filter((item) => item !== template),
                    );
                  }}
                />
                {t(generator[template])}
              </label>
            ))}
          </fieldset>
          {message && <Alert icon={CircleAlert}>{message}</Alert>}
          {error && (
            <Alert tone="danger" icon={CircleAlert}>
              {t(errorLabel(error))}
            </Alert>
          )}
          <DialogFooter>
            {message && (
              <Button
                variant="outline"
                type="button"
                onClick={() => {
                  onDone(levelId);
                }}
              >
                {t(generator.review)}
              </Button>
            )}
            <Button
              variant="primary"
              type="submit"
              loading={run.isPending}
              disabled={!levelId || chosen.length === 0}
            >
              {t(generator.run)}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
