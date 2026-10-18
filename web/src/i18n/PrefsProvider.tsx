import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { defaultPrefs, PrefsContext, type PersistPrefs, type Prefs, type PrefsContextValue } from "./prefs";
import { scriptPrefs, uiLocales, type ScriptPref, type UiLocale } from "./types";

// Display preferences are not sensitive; keeping them per browser lets the welcome page use them before login.
const STORAGE_KEY = "nomax.prefs";

function readStored(): Prefs {
  try {
    const parsed = JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? "null") as Partial<Prefs> | null;
    return {
      uiLocale: uiLocales.includes(parsed?.uiLocale as UiLocale)
        ? (parsed?.uiLocale as UiLocale)
        : defaultPrefs.uiLocale,
      scriptPref: scriptPrefs.includes(parsed?.scriptPref as ScriptPref)
        ? (parsed?.scriptPref as ScriptPref)
        : defaultPrefs.scriptPref,
    };
  } catch {
    return defaultPrefs;
  }
}

function store(prefs: Prefs) {
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(prefs));
  } catch {
    // Storage may be blocked (private mode); the preference still applies for this page view.
  }
}

export function PrefsProvider({ children, initial }: { children: ReactNode; initial?: Prefs }) {
  const [prefs, setPrefs] = useState<Prefs>(() => initial ?? readStored());
  const persistRef = useRef<PersistPrefs | null>(null);

  const update = useCallback((changes: Partial<Prefs>) => {
    setPrefs((previous) => {
      const next = { ...previous, ...changes };
      store(next);
      return next;
    });
    persistRef.current?.(changes);
  }, []);

  const adopt = useCallback((next: Prefs) => {
    setPrefs((previous) =>
      previous.uiLocale === next.uiLocale && previous.scriptPref === next.scriptPref ? previous : next,
    );
    store(next);
  }, []);

  const registerPersistence = useCallback((persist: PersistPrefs) => {
    persistRef.current = persist;
    return () => {
      if (persistRef.current === persist) persistRef.current = null;
    };
  }, []);

  useEffect(() => {
    document.documentElement.lang = prefs.uiLocale;
  }, [prefs.uiLocale]);

  const value = useMemo<PrefsContextValue>(
    () => ({
      ...prefs,
      setUiLocale: (uiLocale) => {
        update({ uiLocale });
      },
      setScriptPref: (scriptPref) => {
        update({ scriptPref });
      },
      adopt,
      registerPersistence,
    }),
    [prefs, update, adopt, registerPersistence],
  );

  return <PrefsContext.Provider value={value}>{children}</PrefsContext.Provider>;
}
