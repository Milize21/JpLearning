import { cva, type VariantProps } from "class-variance-authority";
import type { LucideIcon } from "lucide-react";
import type { ComponentProps, ReactNode } from "react";
import { cn } from "@/lib/utils";

/** Card: no shadow by default, like paper (docs/07 §7). */
export function Card({ className, ...props }: ComponentProps<"section">) {
  return <section className={cn("rounded-lg border border-line bg-surface p-5", className)} {...props} />;
}

const badgeVariants = cva(
  "inline-flex items-center gap-1 rounded-sm px-2 py-0.5 text-caption font-semibold",
  {
    variants: {
      tone: {
        neutral: "bg-canvas-muted text-sumi-700",
        brand: "bg-action-subtle text-aka-800",
        solid: "bg-sumi-900 text-ink-inverse",
        success: "bg-success-bg text-success",
        warning: "bg-warning-bg text-warning",
        danger: "bg-danger-bg text-danger",
      },
    },
    defaultVariants: { tone: "neutral" },
  },
);

export function Badge({
  className,
  tone,
  ...props
}: ComponentProps<"span"> & VariantProps<typeof badgeVariants>) {
  return <span className={cn(badgeVariants({ tone }), className)} {...props} />;
}

export function Skeleton({ className, ...props }: ComponentProps<"div">) {
  return (
    <div
      aria-hidden="true"
      className={cn("animate-pulse rounded-md bg-canvas-muted", className)}
      {...props}
    />
  );
}

/** Empty state: line icon, title with Japanese companion, explanation, at most one action (docs/07 §8). */
export function EmptyState({
  icon: Icon,
  title,
  children,
  action,
}: {
  icon: LucideIcon;
  title: ReactNode;
  children?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed border-line px-6 py-10 text-center">
      <Icon className="size-10 text-ink-subtle" strokeWidth={1.75} aria-hidden="true" />
      <h2 className="text-h3 font-semibold text-ink">{title}</h2>
      {children && <div className="max-w-md text-body-sm text-ink-muted">{children}</div>}
      {action}
    </div>
  );
}

export function Alert({
  tone = "info",
  icon: Icon,
  children,
  className,
}: {
  tone?: "info" | "warning" | "danger" | "success";
  icon: LucideIcon;
  children: ReactNode;
  className?: string;
}) {
  const tones = {
    info: "bg-info-bg text-info",
    warning: "bg-warning-bg text-warning",
    danger: "bg-danger-bg text-danger",
    success: "bg-success-bg text-success",
  } as const;
  return (
    <div
      role={tone === "danger" ? "alert" : "status"}
      className={cn("flex gap-3 rounded-lg px-4 py-3 text-body-sm", tones[tone], className)}
    >
      <Icon className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
      <div className="flex flex-col gap-1">{children}</div>
    </div>
  );
}

export function PageHeader({
  title,
  description,
  actions,
}: {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div className="flex flex-col gap-1">
        <h1 className="text-h2 font-bold text-ink sm:text-h1">{title}</h1>
        {description && <p className="text-body-sm text-ink-muted">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  );
}
