import {
  CalendarClock,
  ChevronDown,
  ChevronUp,
  Download,
  ExternalLink,
  FileText,
  Link2,
  Pencil,
  ScrollText,
  Trash2,
} from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { useCurrentUser } from "@/app/current-user";
import { ContentText } from "@/components/japanese/ContentText";
import { Button } from "@/components/ui/button";
import { Badge, Card } from "@/components/ui/misc";
import { common } from "@/i18n/labels/common";
import { materials } from "@/i18n/labels/content";
import { errorLabel } from "@/i18n/labels/errors";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { errorCode } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/datetime";
import { useDownloadMaterial, type MaterialDto } from "../api/content";
import { RichTextView } from "./RichTextView";

const kindIcons = { richtext: ScrollText, file: FileText, link: Link2 } as const;

function formatSize(bytes: number): string {
  if (bytes >= 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024 / 1024).toFixed(1)} GB`;
  if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

function domainOf(url: string): string {
  try {
    return new URL(url).hostname;
  } catch {
    return url;
  }
}

/**
 * One material (docs/05 §5). Reading texts expand in place; links show their domain and open in a new tab
 * (BR-KONTEN-05); files download through a signed link (BR-FILE-04). Staff also see the publish time and actions.
 */
export function MaterialCard({
  material,
  onEdit,
  onDelete,
}: {
  material: MaterialDto;
  onEdit?: (material: MaterialDto) => void;
  onDelete?: (material: MaterialDto) => void;
}) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const me = useCurrentUser();
  const [open, setOpen] = useState(false);
  const download = useDownloadMaterial();
  const Icon = kindIcons[material.kind];
  const title = textIn(material.title, uiLocale);
  const scheduled = material.publishAt !== null && new Date(material.publishAt) > new Date();
  const canManage = onEdit !== undefined && (me.role === "admin" || material.createdBy === me.id);

  return (
    <Card className="flex flex-col gap-3 p-4">
      <div className="flex items-start gap-3">
        <Icon className="mt-0.5 size-5 shrink-0 text-ink-muted" strokeWidth={1.75} aria-hidden="true" />
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <h3 className="font-semibold text-ink">
            <ContentText value={material.title} />
          </h3>
          {scheduled && material.publishAt && (
            <Badge tone="warning" className="self-start">
              <CalendarClock className="size-3.5" aria-hidden="true" />
              {t(materials.scheduled, { date: formatDateTime(material.publishAt, uiLocale, me.timezone) })}
            </Badge>
          )}
          {material.kind === "link" && material.url && (
            <span className="text-body-sm text-ink-muted">
              {t(materials.externalLink, { domain: domainOf(material.url) })}
            </span>
          )}
          {material.kind === "file" && material.file && (
            <span className="truncate text-body-sm text-ink-muted">
              {material.file.originalName} · {formatSize(material.file.sizeBytes)}
            </span>
          )}
        </div>
        {canManage && (
          <div className="flex shrink-0 gap-1">
            <Button
              variant="ghost"
              size="icon"
              aria-label={`${t(common.edit)} ${title}`}
              onClick={() => {
                onEdit(material);
              }}
            >
              <Pencil aria-hidden="true" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              aria-label={t(materials.deleteTitle, { name: title })}
              onClick={() => {
                onDelete?.(material);
              }}
            >
              <Trash2 aria-hidden="true" />
            </Button>
          </div>
        )}
      </div>

      {material.kind === "richtext" && material.body && (
        <>
          <Button
            variant="outline"
            size="sm"
            className="self-start"
            aria-expanded={open}
            onClick={() => {
              setOpen(!open);
            }}
          >
            {open ? <ChevronUp aria-hidden="true" /> : <ChevronDown aria-hidden="true" />}
            {t(open ? common.close : materials.readMore)}
          </Button>
          {open && (
            <div className="rounded-md border border-line bg-surface p-4">
              <RichTextView value={material.body} />
            </div>
          )}
        </>
      )}
      {material.kind === "link" && material.url && (
        <a
          href={material.url}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-2 self-start rounded-md border border-sumi-300 px-3 py-2 text-body-sm font-semibold text-ink hover:bg-canvas-subtle focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
        >
          {t(materials.openLink)}
          <ExternalLink className="size-4" aria-hidden="true" />
        </a>
      )}
      {material.kind === "file" && material.file && (
        <Button
          variant="secondary"
          size="sm"
          className="self-start"
          loading={download.isPending}
          onClick={() => {
            download.mutate(material.id, {
              onError: (error) => toast.error(t(errorLabel(errorCode(error)))),
            });
          }}
        >
          <Download aria-hidden="true" />
          {t(common.download)}
        </Button>
      )}
    </Card>
  );
}
