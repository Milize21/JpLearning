import type { ReactNode } from "react";
import { Button } from "./button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "./dialog";

/**
 * Confirmation for destructive actions with an explicit confirm label, e.g. "Ya, nonaktifkan akun" (docs/07 §4.4).
 */
export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel,
  cancelLabel,
  closeLabel,
  onConfirm,
  busy = false,
  destructive = false,
  children,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: ReactNode;
  description?: ReactNode;
  confirmLabel: string;
  cancelLabel: string;
  closeLabel: string;
  onConfirm: () => void;
  busy?: boolean;
  destructive?: boolean;
  children?: ReactNode;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={closeLabel} role="alertdialog">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        {children}
        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => {
              onOpenChange(false);
            }}
            disabled={busy}
          >
            {cancelLabel}
          </Button>
          <Button
            variant={destructive ? "destructive-outline" : "primary"}
            onClick={onConfirm}
            loading={busy}
          >
            {confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
