import { useEffect, useRef, useState } from "react";
import { leaderboard } from "@/i18n/labels/insight";
import { useT } from "@/i18n/prefs";
import { useHubEvent } from "../hooks/leaderboardHub";

const visibleMs = 1600;

/**
 * The "+1" of the drilling screen (BR-SEASON-04 PointsAwarded). Rises and fades unless the user prefers reduced motion;
 * the text is announced politely. Answers worth 0 points (cap, self check) show nothing.
 */
export function PointsAwardedBurst() {
  const t = useT();
  const [bursts, setBursts] = useState<Array<{ id: number; points: number }>>([]);
  const nextId = useRef(0);
  const timers = useRef<number[]>([]);

  useEffect(
    () => () => {
      timers.current.forEach((timer) => {
        window.clearTimeout(timer);
      });
    },
    [],
  );

  useHubEvent("PointsAwarded", (award) => {
    if (award.points <= 0) return;
    nextId.current += 1;
    const id = nextId.current;
    setBursts((current) => [...current, { id, points: award.points }]);
    timers.current.push(
      window.setTimeout(() => {
        setBursts((current) => current.filter((burst) => burst.id !== id));
      }, visibleMs),
    );
  });

  return (
    <span className="relative inline-flex h-6 min-w-16 justify-end" aria-live="polite">
      {bursts.map((burst) => (
        <span
          key={burst.id}
          className="absolute right-0 text-body-sm font-bold whitespace-nowrap text-aka-700 motion-safe:animate-[nx-rise_1.6s_ease-out_forwards]"
        >
          {t(leaderboard.pointsAwarded, {
            n: Number.isInteger(burst.points) ? burst.points : burst.points.toFixed(1),
          })}
        </span>
      ))}
    </span>
  );
}
