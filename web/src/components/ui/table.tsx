import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

/** Table of docs/07 §10: header on bg-subtle, rows separated by lines. Pages switch to cards below md. */
export function Table({ className, ...props }: ComponentProps<"table">) {
  return (
    <div className="w-full overflow-x-auto rounded-lg border border-line">
      <table className={cn("w-full border-collapse text-left text-body-sm", className)} {...props} />
    </div>
  );
}

export function THead(props: ComponentProps<"thead">) {
  return <thead className="bg-canvas-subtle text-ink-muted" {...props} />;
}

export function TR({ className, ...props }: ComponentProps<"tr">) {
  return <tr className={cn("border-b border-line last:border-b-0", className)} {...props} />;
}

export function TH({ className, ...props }: ComponentProps<"th">) {
  return <th scope="col" className={cn("px-4 py-3 font-semibold", className)} {...props} />;
}

export function TD({ className, ...props }: ComponentProps<"td">) {
  return <td className={cn("px-4 py-3 align-middle text-ink", className)} {...props} />;
}
