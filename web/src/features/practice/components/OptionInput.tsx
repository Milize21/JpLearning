import { JapaneseText } from "@/components/japanese/JapaneseText";
import { FieldError, Input } from "@/components/ui/form";
import { textFields } from "@/i18n/labels/content";
import { parseFurigana } from "@/i18n/furigana";
import { useT } from "@/i18n/prefs";
import type { TextBlockFields } from "@/i18n/textblock";

/**
 * Compact TextBlock input for payload options (stimulus, choice, token, card side): Indonesian and/or Japanese as
 * furigana markup with a live preview. At least one of the two is needed (docs/03 §3.1).
 */
export function OptionInput({
  id,
  label,
  value,
  onChange,
}: {
  id: string;
  label: string;
  value: TextBlockFields;
  onChange: (next: TextBlockFields) => void;
}) {
  const t = useT();
  const parsed = value.ja.trim() === "" ? null : parseFurigana(value.ja.trim());
  const error = parsed?.errors[0];

  return (
    <fieldset className="flex min-w-0 flex-1 flex-col gap-1">
      <legend className="sr-only">{label}</legend>
      <div className="grid gap-2 sm:grid-cols-2">
        <Input
          id={`${id}-id`}
          aria-label={`${label} · ${t(textFields.indonesian)}`}
          placeholder={t(textFields.indonesian)}
          value={value.id}
          onChange={(event) => {
            onChange({ ...value, id: event.target.value });
          }}
        />
        <Input
          id={`${id}-ja`}
          lang="ja"
          className="font-ja"
          aria-label={`${label} · ${t(textFields.japanese)}`}
          placeholder={t(textFields.japanese)}
          autoComplete="off"
          spellCheck={false}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${id}-error` : undefined}
          value={value.ja}
          onChange={(event) => {
            onChange({ ...value, ja: event.target.value });
          }}
        />
      </div>
      {error && (
        <FieldError id={`${id}-error`}>
          {t(textFields.markupError, { issue: error.text ? `${error.code} (${error.text})` : error.code })}
        </FieldError>
      )}
      {parsed && !error && (
        <span className="text-body-sm text-ink-muted">
          <JapaneseText value={parsed.segments} variant="content" />
        </span>
      )}
    </fieldset>
  );
}
