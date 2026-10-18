import {
  CircleAlert,
  EllipsisVertical,
  FileUp,
  KeyRound,
  Pencil,
  Power,
  PowerOff,
  UserPlus,
  Users,
} from "lucide-react";
import { useDeferredValue, useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Field, FieldLabel, Input, Select } from "@/components/ui/form";
import { Alert, Badge, EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { Table, TD, TH, THead, TR } from "@/components/ui/table";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { profile, users as userLabels } from "@/i18n/labels/identity";
import { roles } from "@/i18n/labels/nav";
import { useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import {
  useResetPassword,
  useSetUserActive,
  useUsers,
  type UserProfile,
  type UserRole,
} from "../api/identity";
import { ImportUsersDialog } from "../components/ImportUsersDialog";
import { TemporaryPassword } from "../components/TemporaryPassword";
import { UserFormDialog } from "../components/UserFormDialog";

const PAGE_SIZE = 20;

type Pending =
  | { kind: "reset"; user: UserProfile }
  | { kind: "deactivate"; user: UserProfile }
  | { kind: "edit"; user: UserProfile }
  | null;

/** /admin/pengguna — list, create, import, reset password, (de)activate (docs/05 §4, BR-AKUN-01…06). */
export function AdminUsersPage() {
  const t = useT();
  const me = useCurrentUser();
  const [search, setSearch] = useState("");
  const [role, setRole] = useState<UserRole | "">("");
  const [status, setStatus] = useState<"" | "active" | "inactive">("");
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const [importing, setImporting] = useState(false);
  const [pending, setPending] = useState<Pending>(null);
  const [resetResult, setResetResult] = useState<{ username: string; password: string } | null>(null);

  const query = useDeferredValue(search.trim());
  const list = useUsers({
    q: query,
    role: role === "" ? undefined : role,
    isActive: status === "" ? undefined : status === "active",
    page,
    pageSize: PAGE_SIZE,
  });
  const resetPassword = useResetPassword();
  const setActive = useSetUserActive();

  const pages = list.data ? Math.max(1, Math.ceil(list.data.totalCount / PAGE_SIZE)) : 1;

  const toggleActive = (user: UserProfile) => {
    if (user.isActive) {
      setPending({ kind: "deactivate", user });
      return;
    }
    setActive.mutate(
      { id: user.id, active: true },
      {
        onSuccess: () => toast.success(t(userLabels.activated)),
        onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
      },
    );
  };

  const confirmDeactivate = (user: UserProfile) => {
    setActive.mutate(
      { id: user.id, active: false },
      {
        onSuccess: () => {
          toast.success(t(userLabels.deactivated));
          setPending(null);
        },
        onError: (error) => {
          toast.error(t(errorLabel(errorCode(error))));
          setPending(null);
        },
      },
    );
  };

  const confirmReset = (user: UserProfile) => {
    resetPassword.mutate(user.id, {
      onSuccess: (response) => {
        setResetResult({ username: user.username, password: response.temporaryPassword });
        setPending(null);
      },
      onError: (error) => {
        toast.error(t(errorLabel(errorCode(error))));
        setPending(null);
      },
    });
  };

  const rowMenu = (user: UserProfile) => (
    <DropdownMenu>
      <DropdownMenuTrigger
        className={cn(buttonVariants({ variant: "ghost", size: "icon" }))}
        aria-label={t(userLabels.rowActions, { name: user.displayName })}
      >
        <EllipsisVertical className="size-5" aria-hidden="true" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem
          onSelect={() => {
            setPending({ kind: "edit", user });
          }}
        >
          <Pencil aria-hidden="true" />
          {t(common.edit)}
        </DropdownMenuItem>
        <DropdownMenuItem
          onSelect={() => {
            setPending({ kind: "reset", user });
          }}
        >
          <KeyRound aria-hidden="true" />
          {t(userLabels.resetPassword)}
        </DropdownMenuItem>
        {user.id !== me.id && (
          <DropdownMenuItem
            onSelect={() => {
              toggleActive(user);
            }}
            className={cn(user.isActive && "text-danger")}
          >
            {user.isActive ? <PowerOff aria-hidden="true" /> : <Power aria-hidden="true" />}
            {t(user.isActive ? userLabels.deactivate : userLabels.activate)}
          </DropdownMenuItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );

  const statusBadge = (user: UserProfile) => (
    <Badge tone={user.isActive ? "success" : "neutral"}>
      {t(user.isActive ? common.active : common.inactive)}
    </Badge>
  );

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={userLabels.title} />}
        description={t(userLabels.description)}
        actions={
          <>
            <Button
              variant="secondary"
              onClick={() => {
                setImporting(true);
              }}
            >
              <FileUp aria-hidden="true" />
              {t(userLabels.importCsv)}
            </Button>
            <Button
              variant="primary"
              onClick={() => {
                setCreating(true);
              }}
            >
              <UserPlus aria-hidden="true" />
              {t(userLabels.add)}
            </Button>
          </>
        }
      />

      <div className="grid gap-3 sm:grid-cols-[1fr_auto_auto]">
        <Field>
          <FieldLabel htmlFor="user-search" className="sr-only">
            {t(common.search)}
          </FieldLabel>
          <Input
            id="user-search"
            type="search"
            placeholder={t(userLabels.searchPlaceholder)}
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              setPage(1);
            }}
          />
        </Field>
        <Field>
          <FieldLabel htmlFor="role-filter" className="sr-only">
            {t(userLabels.filterRole)}
          </FieldLabel>
          <Select
            id="role-filter"
            value={role}
            onChange={(event) => {
              setRole(event.target.value as UserRole | "");
              setPage(1);
            }}
          >
            <option value="">
              {t(profile.role)}: {t(common.all)}
            </option>
            {(["admin", "sensei", "murid"] as const).map((value) => (
              <option key={value} value={value}>
                {t(roles[value])}
              </option>
            ))}
          </Select>
        </Field>
        <Field>
          <FieldLabel htmlFor="status-filter" className="sr-only">
            {t(userLabels.filterStatus)}
          </FieldLabel>
          <Select
            id="status-filter"
            value={status}
            onChange={(event) => {
              setStatus(event.target.value as "" | "active" | "inactive");
              setPage(1);
            }}
          >
            <option value="">
              {t(common.status)}: {t(common.all)}
            </option>
            <option value="active">{t(common.active)}</option>
            <option value="inactive">{t(common.inactive)}</option>
          </Select>
        </Field>
      </div>

      {list.isError && (
        <Alert tone="danger" icon={CircleAlert}>
          {t(errorLabel(errorCode(list.error)))}
        </Alert>
      )}

      {list.isPending ? (
        <div className="flex flex-col gap-2" aria-busy="true">
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-12 w-full" />
          ))}
        </div>
      ) : list.data && list.data.items.length === 0 ? (
        <EmptyState icon={Users} title={<Label value={userLabels.empty} />} />
      ) : (
        list.data && (
          <>
            <p className="text-body-sm text-ink-muted" aria-live="polite">
              {t(common.resultCount, { n: list.data.totalCount })}
            </p>

            {/* Desktop: table. Phones: one card per account (docs/07 §10). */}
            <div className="hidden md:block">
              <Table>
                <THead>
                  <TR>
                    <TH>{t(profile.displayName)}</TH>
                    <TH>{t(profile.username)}</TH>
                    <TH>{t(profile.email)}</TH>
                    <TH>{t(profile.role)}</TH>
                    <TH>{t(common.status)}</TH>
                    <TH className="w-14">
                      <span className="sr-only">{t(common.actions)}</span>
                    </TH>
                  </TR>
                </THead>
                <tbody>
                  {list.data.items.map((user) => (
                    <TR key={user.id} className="hover:bg-canvas-subtle">
                      <TD className="font-semibold">{user.displayName}</TD>
                      <TD>
                        <code>{user.username}</code>
                      </TD>
                      <TD className="break-all text-ink-muted">{user.email ?? "–"}</TD>
                      <TD>{t(roles[user.role])}</TD>
                      <TD>{statusBadge(user)}</TD>
                      <TD>{rowMenu(user)}</TD>
                    </TR>
                  ))}
                </tbody>
              </Table>
            </div>

            <ul className="flex flex-col gap-2 md:hidden">
              {list.data.items.map((user) => (
                <li
                  key={user.id}
                  className="flex items-start justify-between gap-2 rounded-lg border border-line p-4"
                >
                  <div className="flex min-w-0 flex-col gap-1">
                    <span className="font-semibold">{user.displayName}</span>
                    <code className="text-body-sm">{user.username}</code>
                    <span className="text-body-sm text-ink-muted">{t(roles[user.role])}</span>
                    {statusBadge(user)}
                  </div>
                  {rowMenu(user)}
                </li>
              ))}
            </ul>

            {pages > 1 && (
              <nav
                className="flex items-center justify-between gap-2"
                aria-label={t(common.pageOf, { page, pages })}
              >
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => {
                    setPage(page - 1);
                  }}
                >
                  {t(common.previous)}
                </Button>
                <span className="text-body-sm text-ink-muted">{t(common.pageOf, { page, pages })}</span>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= pages}
                  onClick={() => {
                    setPage(page + 1);
                  }}
                >
                  {t(common.next)}
                </Button>
              </nav>
            )}
          </>
        )
      )}

      <UserFormDialog open={creating} onOpenChange={setCreating} />
      <ImportUsersDialog open={importing} onOpenChange={setImporting} />

      {pending?.kind === "edit" && (
        <UserFormDialog
          key={pending.user.id}
          open
          user={pending.user}
          onOpenChange={(open) => {
            if (!open) setPending(null);
          }}
        />
      )}

      <ConfirmDialog
        open={pending?.kind === "reset"}
        onOpenChange={(open) => {
          if (!open) setPending(null);
        }}
        title={t(userLabels.resetConfirmTitle, { name: pending?.user.displayName ?? "" })}
        description={t(userLabels.resetConfirmBody)}
        confirmLabel={t(userLabels.resetConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        busy={resetPassword.isPending}
        onConfirm={() => {
          if (pending?.kind === "reset") confirmReset(pending.user);
        }}
      />

      <ConfirmDialog
        open={pending?.kind === "deactivate"}
        onOpenChange={(open) => {
          if (!open) setPending(null);
        }}
        title={t(userLabels.deactivateConfirmTitle, { name: pending?.user.displayName ?? "" })}
        description={t(userLabels.deactivateConfirmBody)}
        confirmLabel={t(userLabels.deactivateConfirm)}
        cancelLabel={t(common.cancel)}
        closeLabel={t(common.close)}
        busy={setActive.isPending}
        destructive
        onConfirm={() => {
          if (pending?.kind === "deactivate") confirmDeactivate(pending.user);
        }}
      />

      <Dialog
        open={resetResult !== null}
        onOpenChange={(open) => {
          if (!open) setResetResult(null);
        }}
      >
        <DialogContent closeLabel={t(common.close)} aria-describedby={undefined}>
          <DialogHeader>
            <DialogTitle>{t(userLabels.tempPasswordTitle)}</DialogTitle>
          </DialogHeader>
          {resetResult && (
            <TemporaryPassword username={resetResult.username} password={resetResult.password} />
          )}
          <DialogFooter>
            <Button
              variant="primary"
              onClick={() => {
                setResetResult(null);
              }}
            >
              {t(common.close)}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
