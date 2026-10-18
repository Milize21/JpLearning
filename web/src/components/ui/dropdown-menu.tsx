import { Check } from "lucide-react";
import { DropdownMenu as Menu } from "radix-ui";
import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

export const DropdownMenu = Menu.Root;
export const DropdownMenuTrigger = Menu.Trigger;
export const DropdownMenuRadioGroup = Menu.RadioGroup;

export function DropdownMenuContent({
  className,
  sideOffset = 6,
  ...props
}: ComponentProps<typeof Menu.Content>) {
  return (
    <Menu.Portal>
      <Menu.Content
        sideOffset={sideOffset}
        className={cn(
          "z-20 min-w-48 rounded-lg border border-line bg-surface p-1 text-body-sm text-ink shadow-md",
          className,
        )}
        {...props}
      />
    </Menu.Portal>
  );
}

const itemClass =
  "relative flex min-h-11 cursor-pointer items-center gap-2 rounded-md px-3 py-2 outline-none select-none data-[highlighted]:bg-canvas-muted data-[disabled]:pointer-events-none data-[disabled]:text-ink-subtle [&_svg]:size-4";

export function DropdownMenuItem({ className, ...props }: ComponentProps<typeof Menu.Item>) {
  return <Menu.Item className={cn(itemClass, className)} {...props} />;
}

export function DropdownMenuRadioItem({
  className,
  children,
  ...props
}: ComponentProps<typeof Menu.RadioItem>) {
  return (
    <Menu.RadioItem className={cn(itemClass, "pl-9", className)} {...props}>
      <span className="absolute left-3 flex size-4 items-center justify-center">
        <Menu.ItemIndicator>
          <Check aria-hidden="true" />
        </Menu.ItemIndicator>
      </span>
      {children}
    </Menu.RadioItem>
  );
}

export function DropdownMenuLabel({ className, ...props }: ComponentProps<typeof Menu.Label>) {
  return (
    <Menu.Label className={cn("px-3 py-2 text-caption font-semibold text-ink-muted", className)} {...props} />
  );
}

export function DropdownMenuSeparator({ className, ...props }: ComponentProps<typeof Menu.Separator>) {
  return <Menu.Separator className={cn("my-1 h-px bg-line", className)} {...props} />;
}
