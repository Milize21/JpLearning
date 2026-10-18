import { createContext, useCallback, useContext } from "react";
import { formatLabel, type LabelVars, type UiLabel } from "./tb";
import type { ScriptPref, UiLocale } from "./types";

export interface Prefs {
  readonly uiLocale: UiLocale;
  readonly scriptPref: ScriptPref;
}

export type PersistPrefs = (changes: Partial<Prefs>) => void;

export interface PrefsContextValue extends Prefs {
  readonly setUiLocale: (locale: UiLocale) => void;
  readonly setScriptPref: (pref: ScriptPref) => void;
  /** Takes over the preferences stored in the user's profile after login. */
  readonly adopt: (prefs: Prefs) => void;
  /** Lets the signed-in app save every change to the profile (PATCH /me/preferences). */
  readonly registerPersistence: (persist: PersistPrefs) => () => void;
}

export const defaultPrefs: Prefs = { uiLocale: "id", scriptPref: "kanji_furigana" };

export const PrefsContext = createContext<PrefsContextValue | null>(null);

export function usePrefs(): PrefsContextValue {
  const context = useContext(PrefsContext);
  if (!context) throw new Error("usePrefs must be used inside <PrefsProvider>.");
  return context;
}

/** Text in the UI language only, for attributes and non-JSX strings (docs/06 §6.4 rule 7). */
export function useT(): (label: UiLabel, vars?: LabelVars) => string {
  const { uiLocale } = usePrefs();
  return useCallback((label: UiLabel, vars?: LabelVars) => formatLabel(label, uiLocale, vars), [uiLocale]);
}
