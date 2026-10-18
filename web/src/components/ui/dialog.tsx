import { X } from "lucide-react";
import { Dialog as DialogPrimitive } from "radix-ui";
import type { ComponentProps, ReactNode } from "react";
import { cn } from "@/lib/utils";

export const Dialog = DialogPrimitive.Root;
export const DialogTrigger = DialogPrimitive.Trigger;
export const DialogClose = DialogPrimitive.Close;

export function DialogContent({
  className,
  children,
  closeLabel,
  ...props
}: ComponentProps<typeof DialogPrimitive.Content> & { closeLabel: string }) {
  return (
    <DialogPrimitive.Portal>
      <DialogPrimitive.Overlay className="fixed inset-0 z-40 bg-sumi-950/50" />
      <DialogPrimitive.Content
        className={cn(
          "fixed z-50 flex max-h-[92dvh] w-full flex-col gap-4 overflow-y-auto bg-surface p-6 shadow-lg",
          // Bottom sheet on phones, centered dialog from sm (docs/07 §10).
          "inset-x-0 bottom-0 rounded-t-xl sm:inset-auto sm:top-1/2 sm:left-1/2 sm:max-w-lg sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-xl",
          className,
        )}
        {...props}
      >
        {children}
        <DialogPrimitive.Close
          className="absolute top-3 right-3 inline-flex size-11 items-center justify-center rounded-md text-ink-muted hover:bg-canvas-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
          aria-label={closeLabel}
        >
          <X className="size-5" aria-hidden="true" />
        </DialogPrimitive.Close>
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  );
}

export function DialogHeader({ children }: { children: ReactNode }) {
  return <div className="flex flex-col gap-1 pr-10">{children}</div>;
}

export function DialogTitle({ className, ...props }: ComponentProps<typeof DialogPrimitive.Title>) {
  return <DialogPrimitive.Title className={cn("text-h3 font-semibold text-ink", className)} {...props} />;
}

export function DialogDescription({
  className,
  ...props
}: ComponentProps<typeof DialogPrimitive.Description>) {
  return <DialogPrimitive.Description className={cn("text-body-sm text-ink-muted", className)} {...props} />;
}

export function DialogFooter({ children }: { children: ReactNode }) {
  return <div className="mt-2 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">{children}</div>;
}
