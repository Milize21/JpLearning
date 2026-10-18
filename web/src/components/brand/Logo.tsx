import { common } from "@/i18n/labels/common";
import { useT } from "@/i18n/prefs";
import { cn } from "@/lib/utils";

/**
 * Logo tile + wordmark (docs/07 §3): the tile is derived from design/brand without changing colors and always sits on
 * a white or black background, never on red.
 */
export function Logo({ size = "md", className }: { size?: "md" | "lg"; className?: string }) {
  const t = useT();
  const pixels = size === "lg" ? 64 : 32;
  // "NomaX" bold, the rest medium (docs/07 §3).
  const [brandName = "", ...rest] = t(common.brand).split(" ");
  return (
    <span className={cn("inline-flex items-center gap-2", className)}>
      <img
        src={size === "lg" ? "/brand/logo-128.png" : "/brand/logo-64.png"}
        width={pixels}
        height={pixels}
        alt=""
        className={cn("rounded-md", size === "md" && "size-7 sm:size-8")}
      />
      <span className={cn("text-ink", size === "lg" ? "text-h2" : "text-body")}>
        <span className="font-extrabold">{brandName}</span>{" "}
        <span className="font-medium">{rest.join(" ")}</span>
      </span>
    </span>
  );
}
