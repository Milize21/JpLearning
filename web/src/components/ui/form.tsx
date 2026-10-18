import { CircleAlert } from "lucide-react";
import type { ComponentProps, ReactNode } from "react";
import { cn } from "@/lib/utils";

const fieldBase = [
  "w-full rounded-md border border-line-strong bg-surface px-3 text-body text-ink",
  "placeholder:text-ink-subtle",
  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-canvas",
  "disabled:cursor-not-allowed disabled:bg-canvas-muted disabled:text-ink-muted",
  "aria-[invalid=true]:border-danger",
];

export function Input({ className, ...props }: ComponentProps<"input">) {
  return <input className={cn(fieldBase, "h-10", className)} {...props} />;
}

export function Select({ className, ...props }: ComponentProps<"select">) {
  return <select className={cn(fieldBase, "h-10 pr-8", className)} {...props} />;
}

export function FieldLabel({ className, ...props }: ComponentProps<"label">) {
  // eslint-disable-next-line jsx-a11y/label-has-associated-control -- callers pass htmlFor
  return <label className={cn("block text-body-sm font-semibold text-ink", className)} {...props} />;
}

export function FieldHint({ className, ...props }: ComponentProps<"p">) {
  return <p className={cn("text-body-sm text-ink-muted", className)} {...props} />;
}

/** Error = red + icon + text (docs/07 §4.4 rule 6). Link it to the field with aria-describedby. */
export function FieldError({ id, children }: { id: string; children: ReactNode }) {
  return (
    <p id={id} className="flex items-center gap-1 text-body-sm text-danger" role="alert">
      <CircleAlert className="size-4 shrink-0" aria-hidden="true" />
      {children}
    </p>
  );
}

export function Field({ className, ...props }: ComponentProps<"div">) {
  return <div className={cn("flex flex-col gap-1.5", className)} {...props} />;
}

export function Textarea({ className, ...props }: ComponentProps<"textarea">) {
  return <textarea className={cn(fieldBase, "min-h-24 py-2", className)} {...props} />;
}
