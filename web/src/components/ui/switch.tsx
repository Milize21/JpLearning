import { Switch as SwitchPrimitive } from "radix-ui";
import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

/** On = action red, e.g. "chapter open" (docs/07 §10). */
export function Switch({ className, ...props }: ComponentProps<typeof SwitchPrimitive.Root>) {
  return (
    <SwitchPrimitive.Root
      className={cn(
        "inline-flex h-6 w-11 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent bg-sumi-300 transition-colors",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-canvas",
        "disabled:cursor-not-allowed disabled:opacity-50 data-[state=checked]:bg-action",
        className,
      )}
      {...props}
    >
      <SwitchPrimitive.Thumb className="pointer-events-none block size-5 rounded-full bg-surface shadow-sm transition-transform data-[state=checked]:translate-x-5 data-[state=unchecked]:translate-x-0" />
    </SwitchPrimitive.Root>
  );
}
