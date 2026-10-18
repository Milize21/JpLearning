import { useState } from "react";
import { Label } from "@/components/japanese/Label";
import { ScriptPreview } from "@/components/japanese/ScriptToggle";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { common } from "@/i18n/labels/common";
import { onboarding } from "@/i18n/labels/identity";
import { languages, scriptModes } from "@/i18n/labels/nav";
import { usePrefs, useT } from "@/i18n/prefs";
import { formatLabel } from "@/i18n/tb";
import { scriptPrefs, uiLocales } from "@/i18n/types";
import { cn } from "@/lib/utils";
import type { UserProfile } from "../api/identity";

const storageKey = (userId: string) => `nomax.onboarded.${userId}`;

function isOnboarded(userId: string): boolean {
  try {
    return window.localStorage.getItem(storageKey(userId)) === "1";
  } catch {
    return true;
  }
}

/**
 * First-login dialog in two steps: UI language, then Japanese display with a live preview (docs/07 §11 flow 1).
 * Choices apply immediately and are saved to the profile; the dialog shows once per account on this browser.
 */
export function OnboardingDialog({ me }: { me: UserProfile }) {
  const [open, setOpen] = useState(() => !isOnboarded(me.id));
  const [step, setStep] = useState<1 | 2>(1);
  const { uiLocale, scriptPref, setUiLocale, setScriptPref } = usePrefs();
  const t = useT();

  const finish = () => {
    try {
      window.localStorage.setItem(storageKey(me.id), "1");
    } catch {
      // Without storage the dialog may show again on the next visit; nothing else breaks.
    }
    setOpen(false);
  };

  const choiceClass = (selected: boolean) =>
    cn(
      "flex min-h-12 w-full items-center gap-3 rounded-lg border px-4 py-3 text-left text-body-sm",
      "focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none",
      selected ? "border-2 border-sumi-900 font-semibold" : "border-line hover:bg-canvas-subtle",
    );

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) finish();
      }}
    >
      <DialogContent closeLabel={t(common.close)}>
        <DialogHeader>
          <DialogTitle>
            <Label value={onboarding.title} companion="inline" />
          </DialogTitle>
          <DialogDescription>{t(onboarding.step, { step, total: 2 })}</DialogDescription>
        </DialogHeader>

        {step === 1 ? (
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-2 text-body font-semibold">
              <Label value={onboarding.languageQuestion} />
            </legend>
            {uiLocales.map((locale) => (
              <button
                key={locale}
                type="button"
                aria-pressed={uiLocale === locale}
                className={choiceClass(uiLocale === locale)}
                onClick={() => {
                  setUiLocale(locale);
                }}
                lang={locale}
              >
                {formatLabel(languages[locale], locale)}
              </button>
            ))}
          </fieldset>
        ) : (
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-2 text-body font-semibold">
              <Label value={onboarding.scriptQuestion} />
            </legend>
            {scriptPrefs.map((mode) => (
              <button
                key={mode}
                type="button"
                aria-pressed={scriptPref === mode}
                className={cn(choiceClass(scriptPref === mode), "flex-col items-start gap-1")}
                onClick={() => {
                  setScriptPref(mode);
                }}
              >
                <span>{t(scriptModes[mode])}</span>
                <ScriptPreview mode={mode} />
              </button>
            ))}
            <p className="text-body-sm text-ink-muted">{t(onboarding.hint)}</p>
          </fieldset>
        )}

        <DialogFooter>
          {step === 2 && (
            <Button
              variant="outline"
              onClick={() => {
                setStep(1);
              }}
            >
              {t(common.back)}
            </Button>
          )}
          {step === 1 ? (
            <Button
              variant="primary"
              onClick={() => {
                setStep(2);
              }}
            >
              {t(common.next)}
            </Button>
          ) : (
            <Button variant="primary" onClick={finish}>
              {t(onboarding.start)}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
