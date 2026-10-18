import { cva } from "class-variance-authority";

/** Button variants of docs/07 §10. One `primary` (red) per screen or section. */
export const buttonVariants = cva(
  [
    "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md font-semibold",
    "transition-colors duration-[var(--nx-duration-fast)] select-none",
    "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-canvas",
    "disabled:pointer-events-none disabled:bg-canvas-muted disabled:text-sumi-400 disabled:border-transparent",
    "[&_svg]:size-4 [&_svg]:shrink-0",
  ],
  {
    variants: {
      variant: {
        primary: "bg-action text-on-action hover:bg-action-hover active:bg-action-active",
        secondary: "bg-sumi-900 text-ink-inverse hover:bg-sumi-800 active:bg-sumi-950",
        outline: "border border-sumi-300 bg-surface text-ink hover:bg-canvas-subtle",
        ghost: "text-ink hover:bg-canvas-muted",
        "destructive-outline": "border border-danger bg-surface text-danger hover:bg-danger-bg",
        link: "h-auto px-0 text-action underline-offset-4 hover:underline",
      },
      size: {
        sm: "h-8 px-3 text-body-sm",
        md: "h-10 px-4 text-body-sm",
        lg: "h-12 px-6 text-body",
        icon: "size-11",
      },
    },
    defaultVariants: { variant: "outline", size: "md" },
  },
);
