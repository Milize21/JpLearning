import { useMemo } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { ScriptPreview } from "@/components/japanese/ScriptToggle";
import { Field, FieldHint, FieldLabel, Select } from "@/components/ui/form";
import { Card, PageHeader } from "@/components/ui/misc";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { profile } from "@/i18n/labels/identity";
import { languages, nav, roles, scriptModes } from "@/i18n/labels/nav";
import { usePrefs, useT } from "@/i18n/prefs";
import { scriptPrefs, uiLocales, type UiLocale } from "@/i18n/types";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { useUpdatePreferences } from "../api/identity";

/** IANA zones known to the browser; the API validates them again (BR-AKUN-07). */
function useTimeZones(current: string): string[] {
  return useMemo(() => {
    const zones = typeof Intl.supportedValuesOf === "function" ? Intl.supportedValuesOf("timeZone") : [];
    return zones.includes(current) ? zones : [current, ...zones];
  }, [current]);
}

/** /{role}/profil — own account and preferences (docs/05 §4, BR-AKUN-07). */
export function ProfilePage() {
  const me = useCurrentUser();
  const { uiLocale, scriptPref, setUiLocale, setScriptPref } = usePrefs();
  const updatePreferences = useUpdatePreferences();
  const t = useT();
  const zones = useTimeZones(me.timezone);

  const changeTimezone = (timezone: string) => {
    updatePreferences.mutate(
      { timezone },
      {
        onSuccess: () => toast.success(t(common.saved)),
        onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
      },
    );
  };

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      <PageHeader title={<Label value={nav.profile} />} />

      <Card className="flex flex-col gap-4">
        <h2 className="text-h3 font-semibold">
          <Label value={profile.account} companion="inline" />
        </h2>
        <dl className="grid gap-4 sm:grid-cols-2">
          <div>
            <dt className="text-body-sm text-ink-muted">{t(profile.displayName)}</dt>
            <dd className="font-semibold">{me.displayName}</dd>
            <dd className="text-caption text-ink-muted">{t(profile.displayNameHint)}</dd>
          </div>
          <div>
            <dt className="text-body-sm text-ink-muted">{t(profile.username)}</dt>
            <dd className="font-semibold">{me.username}</dd>
          </div>
          <div>
            <dt className="text-body-sm text-ink-muted">{t(profile.email)}</dt>
            <dd className="font-semibold break-all">{me.email ?? t(profile.noEmail)}</dd>
          </div>
          <div>
            <dt className="text-body-sm text-ink-muted">{t(profile.role)}</dt>
            <dd className="font-semibold">{t(roles[me.role])}</dd>
          </div>
        </dl>
      </Card>

      <Card className="flex flex-col gap-6">
        <h2 className="text-h3 font-semibold">
          <Label value={profile.preferences} companion="inline" />
        </h2>

        <fieldset className="flex flex-col gap-2">
          <legend className="mb-2 text-body-sm font-semibold">{t(profile.uiLanguage)}</legend>
          <div className="flex flex-wrap gap-2">
            {uiLocales.map((locale: UiLocale) => (
              <button
                key={locale}
                type="button"
                lang={locale}
                aria-pressed={uiLocale === locale}
                onClick={() => {
                  setUiLocale(locale);
                }}
                className={cn(
                  "min-h-11 rounded-md border px-4 text-body-sm focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none",
                  uiLocale === locale
                    ? "border-2 border-sumi-900 font-semibold"
                    : "border-line hover:bg-canvas-subtle",
                )}
              >
                {t(languages[locale])}
              </button>
            ))}
          </div>
        </fieldset>

        <fieldset className="flex flex-col gap-2">
          <legend className="mb-2 text-body-sm font-semibold">{t(nav.japaneseDisplay)}</legend>
          <div className="grid gap-2 sm:grid-cols-2">
            {scriptPrefs.map((mode) => (
              <button
                key={mode}
                type="button"
                aria-pressed={scriptPref === mode}
                onClick={() => {
                  setScriptPref(mode);
                }}
                className={cn(
                  "flex min-h-14 flex-col items-start gap-0.5 rounded-lg border px-4 py-2 text-left text-body-sm focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none",
                  scriptPref === mode
                    ? "border-2 border-sumi-900 font-semibold"
                    : "border-line hover:bg-canvas-subtle",
                )}
              >
                <span>{t(scriptModes[mode])}</span>
                <ScriptPreview mode={mode} />
              </button>
            ))}
          </div>
        </fieldset>

        <Field>
          <FieldLabel htmlFor="timezone">{t(profile.timezone)}</FieldLabel>
          <Select
            id="timezone"
            value={me.timezone}
            disabled={updatePreferences.isPending}
            onChange={(event) => {
              changeTimezone(event.target.value);
            }}
            className="sm:max-w-sm"
          >
            {zones.map((zone) => (
              <option key={zone} value={zone}>
                {zone}
              </option>
            ))}
          </Select>
          <FieldHint>{me.timezone}</FieldHint>
        </Field>
      </Card>
    </div>
  );
}
