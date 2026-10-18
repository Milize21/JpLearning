import { clsx, type ClassValue } from "clsx";
import { extendTailwindMerge } from "tailwind-merge";

// tailwind-merge only knows Tailwind's default font sizes. Without the token scale (design/tailwind-theme.css)
// it reads `text-body-sm` as a color and drops `text-on-action`, so red buttons lose their white text.
const twMerge = extendTailwindMerge({
  extend: {
    theme: {
      text: ["display", "h1", "h2", "h3", "body-lg", "body", "body-sm", "caption", "learn-lg", "learn-xl"],
    },
  },
});

/** Merges class names; later Tailwind utilities win over earlier ones. */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
