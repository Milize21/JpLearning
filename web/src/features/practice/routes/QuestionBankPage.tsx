import { EllipsisVertical, FileQuestion, Plus, Search, Sparkles } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { ContentText } from "@/components/japanese/ContentText";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { buttonVariants } from "@/components/ui/button-variants";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { FieldLabel, Input, Select } from "@/components/ui/form";
import { Badge, EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { useLevels } from "@/features/content/api/content";
import { common } from "@/i18n/labels/common";
import { errorLabel } from "@/i18n/labels/errors";
import { bank, categoryLabels, kindLabels, questionStatusLabels, scopeLabels } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import {
  categories,
  usePublishMany,
  useQuestionAction,
  useQuestions,
  type Category,
  type QuestionAction,
  type QuestionDto,
  type QuestionFilter,
  type QuestionScope,
  type QuestionStatus,
} from "../api/practice";
import { GeneratorDialog } from "../components/GeneratorDialog";
import { QuestionEditorDialog } from "../components/QuestionEditorDialog";
import { QuestionPreviewDialog } from "../components/QuestionPreviewDialog";

const statuses: readonly QuestionStatus[] = ["draft", "review", "published", "archived"];
const statusTone = {
  draft: "neutral",
  review: "warning",
  published: "success",
  archived: "neutral",
} as const;

/**
 * /sensei/bank-soal and /admin/bank-soal (docs/05 §7): filters, the student preview, status actions of BR-SOAL-02 and,
 * for admins, the generator with bulk publishing of reviewed questions.
 */
export function QuestionBankPage() {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const isAdmin = me.role === "admin";
  const levels = useLevels();
  const [filter, setFilter] = useState<QuestionFilter>({ page: 1, pageSize: 20 });
  const [search, setSearch] = useState("");
  const questions = useQuestions(filter);
  const action = useQuestionAction();
  const publishMany = usePublishMany();
  const [editing, setEditing] = useState<QuestionDto | undefined>(undefined);
  const [editorOpen, setEditorOpen] = useState(false);
  const [previewId, setPreviewId] = useState<string | null>(null);
  const [generatorOpen, setGeneratorOpen] = useState(false);
  const [selected, setSelected] = useState<string[]>([]);
  const total = questions.data?.totalCount ?? 0;
  const pages = Math.max(1, Math.ceil(total / filter.pageSize));
  const update = (changes: Partial<QuestionFilter>) => {
    setFilter({ ...filter, ...changes, page: changes.page ?? 1 });
    setSelected([]);
  };

  const run = (question: QuestionDto, kind: QuestionAction) => {
    action.mutate(
      { id: question.id, action: kind },
      {
        onSuccess: (result) => {
          toast.success(t(kind === "duplicate" ? bank.duplicated : bank.actionDone));
          if (kind === "duplicate") {
            setEditing(result);
            setEditorOpen(true);
          }
        },
        onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
      },
    );
  };

  const canManage = (question: QuestionDto) =>
    isAdmin || (question.scope === "private" && question.ownerId === me.id);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={bank.title} companionClassName="text-body font-medium" />}
        description={t(bank.description)}
        actions={
          <>
            {isAdmin && (
              <>
                <Link to="/admin/sumber-soal" className={buttonVariants({ variant: "ghost" })}>
                  {t(bank.sources)}
                </Link>
                <Button
                  variant="outline"
                  onClick={() => {
                    setGeneratorOpen(true);
                  }}
                >
                  <Sparkles aria-hidden="true" />
                  {t(bank.generator)}
                </Button>
              </>
            )}
            <Button
              variant="primary"
              onClick={() => {
                setEditing(undefined);
                setEditorOpen(true);
              }}
            >
              <Plus aria-hidden="true" />
              {t(bank.create)}
            </Button>
          </>
        }
      />

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <form
          className="flex flex-col gap-1.5 lg:col-span-2"
          role="search"
          onSubmit={(event) => {
            event.preventDefault();
            update({ q: search });
          }}
        >
          <FieldLabel htmlFor="bank-search">{t(bank.search)}</FieldLabel>
          <div className="flex gap-2">
            <Input
              id="bank-search"
              type="search"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
              }}
            />
            <Button variant="outline" size="icon" type="submit" aria-label={t(common.search)}>
              <Search aria-hidden="true" />
            </Button>
          </div>
        </form>
        <div className="flex flex-col gap-1.5">
          <FieldLabel htmlFor="bank-level">{t(bank.level)}</FieldLabel>
          <Select
            id="bank-level"
            value={filter.levelId ?? ""}
            onChange={(event) => {
              update({ levelId: event.target.value || undefined });
            }}
          >
            <option value="">{t(bank.all)}</option>
            {levels.data?.map((level) => (
              <option key={level.id} value={level.id}>
                {textIn(level.name, uiLocale)}
              </option>
            ))}
          </Select>
        </div>
        <div className="flex flex-col gap-1.5">
          <FieldLabel htmlFor="bank-category">{t(bank.category)}</FieldLabel>
          <Select
            id="bank-category"
            value={filter.category ?? ""}
            onChange={(event) => {
              update({ category: (event.target.value || undefined) as Category | undefined });
            }}
          >
            <option value="">{t(bank.all)}</option>
            {categories.map((value) => (
              <option key={value} value={value}>
                {t(categoryLabels[value])}
              </option>
            ))}
          </Select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-1.5">
            <FieldLabel htmlFor="bank-status">{t(bank.status)}</FieldLabel>
            <Select
              id="bank-status"
              value={filter.status ?? ""}
              onChange={(event) => {
                update({ status: (event.target.value || undefined) as QuestionStatus | undefined });
              }}
            >
              <option value="">{t(bank.all)}</option>
              {statuses.map((value) => (
                <option key={value} value={value}>
                  {t(questionStatusLabels[value])}
                </option>
              ))}
            </Select>
          </div>
          <div className="flex flex-col gap-1.5">
            <FieldLabel htmlFor="bank-scope">{t(bank.scope)}</FieldLabel>
            <Select
              id="bank-scope"
              value={filter.scope ?? ""}
              onChange={(event) => {
                update({ scope: (event.target.value || undefined) as QuestionScope | undefined });
              }}
            >
              <option value="">{t(bank.all)}</option>
              <option value="builtin">{t(scopeLabels.builtin)}</option>
              <option value="private">{t(scopeLabels.private)}</option>
            </Select>
          </div>
        </div>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-body-sm text-ink-muted" aria-live="polite">
          {t(bank.count, { n: total })}
        </p>
        {isAdmin && selected.length > 0 && (
          <Button
            variant="secondary"
            loading={publishMany.isPending}
            onClick={() => {
              publishMany.mutate(selected, {
                onSuccess: (result) => {
                  toast.success(t(bank.publishedMany, { n: result.published }));
                  setSelected([]);
                },
                onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
              });
            }}
          >
            {t(bank.publishSelected, { n: selected.length })}
          </Button>
        )}
      </div>

      {questions.isPending && <Skeleton className="h-64" />}
      {questions.data?.items.length === 0 && (
        <EmptyState icon={FileQuestion} title={<Label value={bank.empty} />} />
      )}
      <ul className="flex flex-col divide-y divide-line rounded-lg border border-line bg-surface">
        {questions.data?.items.map((question) => {
          const promptText = textIn(question.prompt, uiLocale);
          return (
            <li key={question.id} className="flex items-start gap-3 px-4 py-3">
              {isAdmin && question.status !== "published" && question.status !== "archived" && (
                <input
                  type="checkbox"
                  className="mt-1 size-4 accent-sumi-900"
                  aria-label={t(bank.selectForPublish, { name: promptText })}
                  checked={selected.includes(question.id)}
                  onChange={(event) => {
                    setSelected(
                      event.target.checked
                        ? [...selected, question.id]
                        : selected.filter((id) => id !== question.id),
                    );
                  }}
                />
              )}
              <div className="flex min-w-0 flex-1 flex-col gap-1">
                <ContentText value={question.prompt} className="text-ink" companion="none" />
                <div className="flex flex-wrap items-center gap-1.5">
                  <Badge tone={statusTone[question.status]}>{t(questionStatusLabels[question.status])}</Badge>
                  <Badge>{t(kindLabels[question.kind])}</Badge>
                  <Badge>{t(categoryLabels[question.category])}</Badge>
                  <Badge tone={question.scope === "builtin" ? "solid" : "neutral"}>
                    {t(scopeLabels[question.scope])}
                  </Badge>
                  {question.hasAnswers && <Badge tone="brand">{t(bank.answered)}</Badge>}
                </div>
                {question.tags.filter((tag) => !tag.startsWith("gen:")).length > 0 && (
                  <p className="truncate text-caption text-ink-muted">
                    {question.tags.filter((tag) => !tag.startsWith("gen:")).join(" · ")}
                  </p>
                )}
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setPreviewId(question.id);
                }}
              >
                {t(bank.tryIt)}
              </Button>
              <DropdownMenu>
                <DropdownMenuTrigger
                  className={cn(buttonVariants({ variant: "ghost", size: "icon" }))}
                  aria-label={`${t(common.actions)} ${promptText}`}
                >
                  <EllipsisVertical aria-hidden="true" />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  {canManage(question) && question.status !== "archived" && (
                    <DropdownMenuItem
                      onSelect={() => {
                        setEditing(question);
                        setEditorOpen(true);
                      }}
                    >
                      {t(common.edit)}
                    </DropdownMenuItem>
                  )}
                  {canManage(question) && question.status === "draft" && (
                    <DropdownMenuItem
                      onSelect={() => {
                        run(question, "submit-review");
                      }}
                    >
                      {t(bank.submitReview)}
                    </DropdownMenuItem>
                  )}
                  {canManage(question) &&
                    (question.status === "draft" || question.status === "review") &&
                    (question.scope === "private" || isAdmin) && (
                      <DropdownMenuItem
                        onSelect={() => {
                          run(question, "publish");
                        }}
                      >
                        {t(bank.publish)}
                      </DropdownMenuItem>
                    )}
                  <DropdownMenuItem
                    onSelect={() => {
                      run(question, "duplicate");
                    }}
                  >
                    {t(bank.duplicate)}
                  </DropdownMenuItem>
                  {canManage(question) && question.status !== "archived" && (
                    <DropdownMenuItem
                      onSelect={() => {
                        run(question, "archive");
                      }}
                    >
                      {t(bank.archive)}
                    </DropdownMenuItem>
                  )}
                </DropdownMenuContent>
              </DropdownMenu>
            </li>
          );
        })}
      </ul>

      {pages > 1 && (
        <nav
          className="flex items-center justify-center gap-2"
          aria-label={t(common.pageOf, { page: filter.page, pages })}
        >
          <Button
            variant="outline"
            size="sm"
            disabled={filter.page <= 1}
            onClick={() => {
              update({ page: filter.page - 1 });
            }}
          >
            {t(common.previous)}
          </Button>
          <span className="text-body-sm text-ink-muted">
            {t(common.pageOf, { page: filter.page, pages })}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={filter.page >= pages}
            onClick={() => {
              update({ page: filter.page + 1 });
            }}
          >
            {t(common.next)}
          </Button>
        </nav>
      )}

      <QuestionEditorDialog question={editing} open={editorOpen} onOpenChange={setEditorOpen} />
      <QuestionPreviewDialog
        questionId={previewId}
        onClose={() => {
          setPreviewId(null);
        }}
      />
      {isAdmin && (
        <GeneratorDialog
          open={generatorOpen}
          onOpenChange={setGeneratorOpen}
          onDone={(levelId) => {
            setGeneratorOpen(false);
            update({ levelId, status: "review", scope: "builtin" });
          }}
        />
      )}
    </div>
  );
}
