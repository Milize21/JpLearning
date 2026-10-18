import { useEffect } from "react";
import { toast } from "sonner";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { useMe, useUpdatePreferences } from "../api/identity";

/**
 * Keeps the UI preferences and the profile in step (BR-AKUN-07): the profile wins after login, and every change in
 * the UI is saved to /me/preferences (optimistic: the UI changes first).
 */
export function PrefsSync() {
  const { data: me } = useMe();
  const { adopt, registerPersistence } = usePrefs();
  const { mutate } = useUpdatePreferences();
  const t = useT();

  useEffect(() => {
    if (me) adopt({ uiLocale: me.uiLocale, scriptPref: me.scriptPref });
  }, [me, adopt]);

  useEffect(() => {
    if (!me) return undefined;
    return registerPersistence((changes) => {
      mutate(changes, {
        onError: (error) => {
          toast.error(t(errorLabel(errorCode(error))));
        },
      });
    });
  }, [me, registerPersistence, mutate, t]);

  return null;
}
