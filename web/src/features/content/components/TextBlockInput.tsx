import { z } from "zod";
import { JapaneseText } from "@/components/japanese/JapaneseText";
import { Field, FieldError, FieldHint, FieldLabel, Input } from "@/components/ui/form";
import { common } from "@/i18n/labels/common";
import { textFields } from "@/i18n/labels/content";
import { fieldErrors } from "@/i18n/labels/identity";
import { parseFurigana } from "@/i18n/furigana";
import { useT } from "@/i18n/prefs";
import type { TextBlockFields } from "@/i18n/textblock";
import type { UiLabel } from "@/i18n/tb";

/** docs/03 §3.1 limits: Indonesian required for titles; Japanese must be valid furigana markup. */
export const textBlockSchema = z.object({
  id: z.string().trim().min(1, "required").max(2000, "tooLong"),
  en: z.string().trim().max(2000, "tooLong"),
  ja: z
    .string()
    .trim()
    .refine((value) => value === "" || parseFurigana(value).errors.length === 0, "invalidMarkup"),
});

export const emptyTextBlock: TextBlockFields = { id: "", en: "", ja: "" };

const messageLabels: Record<string, UiLabel> = {
  required: fieldErrors.required,
  tooLong: fieldErrors.tooLong,
};

/**
 * Indonesian, English and Japanese of one TextBlock. Japanese is typed as furigana markup (docs/06 §3) and previewed
 * live in the user's script preference; errors and particle warnings come from the same parser as the server.
 */
export function TextBlockInput({
  id,
  label,
  value,
  onChange,
  errors,
}: {
  id: string;
  label: UiLabel;
  value: TextBlockFields;
  onChange: (next: TextBlockFields) => void;
  errors?: { id?: string | undefined; en?: string | undefined; ja?: string | undefined };
}) {
  const t = useT();
  const parsed = value.ja.trim() === "" ? null : parseFurigana(value.ja.trim());
  const firstError = parsed?.errors[0];
  const firstWarning = parsed?.warnings[0];
  const message = (key: string | undefined) => (key ? t(messageLabels[key] ?? fieldErrors.invalid, { max: 2000 }) : null);

  return (
    <fieldset className="flex flex-col gap-3 rounded-lg border border-line p-4">
      <legend className="px-1 text-body-sm font-semibold text-ink">{t(label)}</legend>
      <Field>
        <FieldLabel htmlFor={`${id}-id`}>{t(textFields.indonesian)}</FieldLabel>
        <Input
          id={`${id}-id`}
          value={value.id}
          aria-invalid={errors?.id ? true : undefined}
          aria-describedby={`${id}-id-error`}
          onChange={(event) => {
            onChange({ ...value, id: event.target.value });
          }}
        />
        {errors?.id && <FieldError id={`${id}-id-error`}>{message(errors.id)}</FieldError>}
      </Field>
      <Field>
        <FieldLabel htmlFor={`${id}-en`}>
          {t(textFields.english)} <span className="font-normal text-ink-muted">({t(common.optional)})</span>
        </FieldLabel>
        <Input
          id={`${id}-en`}
          value={value.en}
          onChange={(event) => {
            onChange({ ...value, en: event.target.value });
          }}
        />
      </Field>
      <Field>
        <FieldLabel htmlFor={`${id}-ja`}>
          {t(textFields.japanese)} <span className="font-normal text-ink-muted">({t(common.optional)})</span>
        </FieldLabel>
        <Input
          id={`${id}-ja`}
          lang="ja"
          className="font-ja"
          value={value.ja}
          autoComplete="off"
          spellCheck={false}
          aria-invalid={firstError || errors?.ja ? true : undefined}
          aria-describedby={`${id}-ja-hint ${id}-ja-error`}
          onChange={(event) => {
            onChange({ ...value, ja: event.target.value });
          }}
        />
        <FieldHint id={`${id}-ja-hint`}>{t(textFields.japaneseHint)}</FieldHint>
        {firstError && (
          <FieldError id={`${id}-ja-error`}>
            {t(textFields.markupError, { issue: firstError.text ? `${firstError.code} (${firstError.text})` : firstError.code })}
          </FieldError>
        )}
        {!firstError && firstWarning && <FieldHint>{t(textFields.particleWarning, { text: firstWarning.text ?? "" })}</FieldHint>}
        {parsed && !firstError && (
          <p className="rounded-md bg-canvas-subtle px-3 py-2 text-body">
            <span className="mr-2 text-caption font-semibold text-ink-muted">{t(textFields.preview)}</span>
            <JapaneseText value={parsed.segments} variant="content" />
          </p>
        )}
      </Field>
    </fieldset>
  );
}
