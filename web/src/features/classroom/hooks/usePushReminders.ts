import { useCallback, useEffect, useState } from "react";
import { fetchPushPublicKey, useDeletePushSubscription, useSavePushSubscription } from "../api/classroom";

export type PushState = "checking" | "unsupported" | "denied" | "unavailable" | "off" | "on";

const storageKey = "nomax.push.subscriptionId";

function readStoredId(): string | null {
  try {
    return window.localStorage.getItem(storageKey);
  } catch {
    return null;
  }
}

function storeId(id: string | null) {
  try {
    if (id) window.localStorage.setItem(storageKey, id);
    else window.localStorage.removeItem(storageKey);
  } catch {
    // Private mode: the server subscription is then only removed by the push service later.
  }
}

/** VAPID keys are base64url; PushManager wants the raw bytes. */
function keyBytes(base64Url: string): Uint8Array<ArrayBuffer> {
  const padded = (base64Url + "=".repeat((4 - (base64Url.length % 4)) % 4))
    .replace(/-/gu, "+")
    .replace(/_/gu, "/");
  const raw = window.atob(padded);
  const bytes = new Uint8Array(new ArrayBuffer(raw.length));
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
  return bytes;
}

const isSupported = () =>
  typeof window !== "undefined" &&
  "serviceWorker" in navigator &&
  "PushManager" in window &&
  "Notification" in window;

/**
 * Web push reminders on this device (docs/07 §11 flow 10). Permission is asked only when the user presses the button,
 * never on page load. iOS supports it only for the app added to the home screen (16.4+).
 */
export function usePushReminders() {
  const save = useSavePushSubscription();
  const remove = useDeletePushSubscription();
  const [state, setState] = useState<PushState>("checking");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const check = async () => {
      if (!isSupported()) return "unsupported" as const;
      if (Notification.permission === "denied") return "denied" as const;
      const registration = await navigator.serviceWorker.getRegistration("/");
      const subscription = await registration?.pushManager.getSubscription();
      return subscription ? ("on" as const) : ("off" as const);
    };
    void check()
      .then((result) => {
        if (!cancelled) setState(result);
      })
      .catch(() => {
        if (!cancelled) setState("off");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const enable = useCallback(async () => {
    if (!isSupported()) return;
    setBusy(true);
    try {
      const permission = await Notification.requestPermission();
      if (permission !== "granted") {
        setState(permission === "denied" ? "denied" : "off");
        return;
      }
      const publicKey = await fetchPushPublicKey();
      if (!publicKey) {
        setState("unavailable");
        return;
      }
      const registration = await navigator.serviceWorker.register("/sw.js");
      await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: keyBytes(publicKey),
      });
      const saved = await save.mutateAsync(subscription.toJSON());
      storeId(saved.id);
      setState("on");
    } finally {
      setBusy(false);
    }
  }, [save]);

  const disable = useCallback(async () => {
    setBusy(true);
    try {
      const registration = await navigator.serviceWorker.getRegistration("/");
      const subscription = await registration?.pushManager.getSubscription();
      await subscription?.unsubscribe();
      const id = readStoredId();
      if (id) await remove.mutateAsync(id).catch(() => undefined);
      storeId(null);
      setState("off");
    } finally {
      setBusy(false);
    }
  }, [remove]);

  return { state, busy, enable, disable };
}
