import { zodResolver } from "@hookform/resolvers/zod";
import { CircleAlert } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldError, FieldHint, FieldLabel, Input, Select } from "@/components/ui/form";
import { Alert } from "@/components/ui/misc";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { fieldErrors, profile, users } from "@/i18n/labels/identity";
import { roles } from "@/i18n/labels/nav";
import { useT } from "@/i18n/prefs";
import type { UiLabel } from "@/i18n/tb";
import { ApiError, errorCode } from "@/lib/api/errors";
import { useCreateUser, useUpdateUser, type CreateUserResponse, type UserProfile } from "../api/identity";
import { TemporaryPassword } from "./TemporaryPassword";

// Same limits as BR-AKUN-02 on the server (AccountRules); the server validates again.
const usernamePattern = /^[a-z0-9._-]{3,64}$/u;
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/u;

const schema = z.object({
  username: z.string().regex(usernamePattern, "invalidUsername"),
  displayName: z.string().trim().min(1, "required").max(80, "tooLong"),
  email: z
    .string()
    .trim()
    .max(254, "tooLong")
    .refine((value) => value === "" || emailPattern.test(value), "invalidEmail"),
  role: z.enum(["admin", "sensei", "murid"]),
});

type FormValues = z.infer<typeof schema>;

const messageLabels: Record<string, UiLabel> = {
  required: fieldErrors.required,
  tooLong: fieldErrors.tooLong,
  invalidEmail: fieldErrors.invalidEmail,
  invalidUsername: fieldErrors.invalidUsername,
  invalid: fieldErrors.invalid,
};

/**
 * Create (admin picks the role) or edit (display name and email) an account. After creation the one-time temporary
 * password is shown in the same dialog (BR-AKUN-02).
 */
export function UserFormDialog({
  open,
  onOpenChange,
  user,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Edit this account; omit to create a new one. */
  user?: UserProfile;
}) {
  const t = useT();
  const createUser = useCreateUser();
  const updateUser = useUpdateUser();
  const [created, setCreated] = useState<CreateUserResponse | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const editing = user !== undefined;

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      username: user?.username ?? "",
      displayName: user?.displayName ?? "",
      email: user?.email ?? "",
      role: user?.role ?? "murid",
    },
  });
  const { register, handleSubmit, formState, setError, reset } = form;

  const close = (next: boolean) => {
    if (!next) {
      reset();
      setCreated(null);
      setFormError(null);
    }
    onOpenChange(next);
  };

  const applyServerError = (error: unknown) => {
    if (error instanceof ApiError && error.code === "validation_failed") {
      for (const field of Object.keys(error.fieldErrors)) {
        if (field in schema.shape) setError(field as keyof FormValues, { message: "invalid" });
      }
    }
    setFormError(errorCode(error));
  };

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      if (editing) {
        await updateUser.mutateAsync({
          id: user.id,
          changes: { displayName: values.displayName, email: values.email.trim() },
        });
        toast.success(t(users.updated));
        close(false);
      } else {
        const response = await createUser.mutateAsync({
          username: values.username,
          displayName: values.displayName,
          role: values.role,
          email: values.email.trim() === "" ? null : values.email.trim(),
        });
        toast.success(t(users.created));
        setCreated(response);
      }
    } catch (error) {
      applyServerError(error);
    }
  });

  const fieldMessage = (name: keyof FormValues) => {
    const message = formState.errors[name]?.message;
    if (!message) return null;
    const label = messageLabels[message] ?? fieldErrors.invalid;
    return <FieldError id={`${name}-error`}>{t(label, { max: name === "email" ? 254 : 80 })}</FieldError>;
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent closeLabel={t(common.close)} aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>
            <Label value={editing ? users.editTitle : users.createTitle} companion="inline" />
          </DialogTitle>
        </DialogHeader>

        {created ? (
          <>
            <TemporaryPassword username={created.user.username} password={created.temporaryPassword} />
            <DialogFooter>
              <Button
                variant="primary"
                onClick={() => {
                  close(false);
                }}
              >
                {t(common.close)}
              </Button>
            </DialogFooter>
          </>
        ) : (
          <form onSubmit={(event) => void onSubmit(event)} noValidate className="flex flex-col gap-4">
            {!editing && (
              <Field>
                <FieldLabel htmlFor="username">{t(profile.username)}</FieldLabel>
                <Input
                  id="username"
                  autoComplete="off"
                  autoCapitalize="none"
                  spellCheck={false}
                  aria-invalid={formState.errors.username ? true : undefined}
                  aria-describedby="username-hint username-error"
                  {...register("username")}
                />
                <FieldHint id="username-hint">{t(users.usernameHint)}</FieldHint>
                {fieldMessage("username")}
              </Field>
            )}

            <Field>
              <FieldLabel htmlFor="displayName">{t(profile.displayName)}</FieldLabel>
              <Input
                id="displayName"
                autoComplete="off"
                aria-invalid={formState.errors.displayName ? true : undefined}
                aria-describedby="displayName-error"
                {...register("displayName")}
              />
              {fieldMessage("displayName")}
            </Field>

            <Field>
              <FieldLabel htmlFor="email">
                {t(profile.email)} <span className="font-normal text-ink-muted">({t(common.optional)})</span>
              </FieldLabel>
              <Input
                id="email"
                type="email"
                autoComplete="off"
                aria-invalid={formState.errors.email ? true : undefined}
                aria-describedby="email-hint email-error"
                {...register("email")}
              />
              <FieldHint id="email-hint">{t(editing ? users.emailClearHint : users.emailHint)}</FieldHint>
              {fieldMessage("email")}
            </Field>

            {!editing && (
              <Field>
                <FieldLabel htmlFor="role">{t(profile.role)}</FieldLabel>
                <Select id="role" {...register("role")}>
                  {(["murid", "sensei", "admin"] as const).map((role) => (
                    <option key={role} value={role}>
                      {t(roles[role])}
                    </option>
                  ))}
                </Select>
              </Field>
            )}

            {formError && (
              <Alert tone="danger" icon={CircleAlert}>
                {t(errorLabel(formError))}
              </Alert>
            )}

            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  close(false);
                }}
              >
                {t(common.cancel)}
              </Button>
              <Button type="submit" variant="primary" loading={formState.isSubmitting}>
                {t(editing ? common.save : users.createSubmit)}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
