import { BookMarked, Plus } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { Label } from "@/components/japanese/Label";
import { Button } from "@/components/ui/button";
import { FieldLabel, Input } from "@/components/ui/form";
import { Badge, Card, EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { errorLabel } from "@/i18n/labels/errors";
import { sources } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { errorCode } from "@/lib/api/errors";
import { formatDate } from "@/lib/datetime";
import { useSaveSource, useSources } from "../api/practice";

/** /admin/sumber-soal — sources and licenses of questions (BR-SOAL-01). */
export function SourcesPage() {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const list = useSources();
  const save = useSaveSource();
  const [name, setName] = useState("");
  const [license, setLicense] = useState("");
  const [url, setUrl] = useState("");

  const onError = (error: unknown) => toast.error(t(errorLabel(errorCode(error))));

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={sources.title} companionClassName="text-body font-medium" />}
        description={t(sources.description)}
      />
      <Card>
        <form
          noValidate
          className="grid gap-3 sm:grid-cols-[1fr_1fr_1fr_auto] sm:items-end"
          onSubmit={(event) => {
            event.preventDefault();
            if (!name.trim() || !license.trim()) return;
            save.mutate(
              { name: name.trim(), license: license.trim(), url: url.trim() || null, markReviewed: false },
              {
                onSuccess: () => {
                  setName("");
                  setLicense("");
                  setUrl("");
                },
                onError,
              },
            );
          }}
        >
          <div className="flex flex-col gap-1.5">
            <FieldLabel htmlFor="source-name">{t(sources.name)}</FieldLabel>
            <Input
              id="source-name"
              value={name}
              maxLength={200}
              onChange={(event) => {
                setName(event.target.value);
              }}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <FieldLabel htmlFor="source-license">{t(sources.license)}</FieldLabel>
            <Input
              id="source-license"
              value={license}
              maxLength={100}
              onChange={(event) => {
                setLicense(event.target.value);
              }}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <FieldLabel htmlFor="source-url">{t(sources.url)}</FieldLabel>
            <Input
              id="source-url"
              type="url"
              value={url}
              onChange={(event) => {
                setUrl(event.target.value);
              }}
            />
          </div>
          <Button
            variant="primary"
            type="submit"
            loading={save.isPending}
            disabled={!name.trim() || !license.trim()}
          >
            <Plus aria-hidden="true" />
            {t(sources.add)}
          </Button>
        </form>
      </Card>
      {list.isPending && <Skeleton className="h-32" />}
      {list.data?.length === 0 && <EmptyState icon={BookMarked} title={<Label value={sources.empty} />} />}
      <ul className="flex flex-col divide-y divide-line rounded-lg border border-line bg-surface">
        {list.data?.map((source) => (
          <li key={source.id} className="flex flex-wrap items-center gap-3 px-4 py-3">
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="font-semibold text-ink">{source.name}</span>
              <span className="truncate text-body-sm text-ink-muted">{source.url ?? ""}</span>
            </span>
            <Badge>{source.license}</Badge>
            {source.reviewedAt ? (
              <Badge tone="success">
                {t(sources.reviewed, { date: formatDate(source.reviewedAt, uiLocale, me.timezone) })}
              </Badge>
            ) : (
              <Button
                variant="outline"
                size="sm"
                loading={save.isPending}
                onClick={() => {
                  save.mutate(
                    {
                      id: source.id,
                      name: source.name,
                      license: source.license,
                      url: source.url,
                      markReviewed: true,
                    },
                    { onError },
                  );
                }}
              >
                {t(sources.markReviewed)}
              </Button>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
