import { ChevronDown } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { nav, scriptModes } from "@/i18n/labels/nav";
import { usePrefs, useT } from "@/i18n/prefs";
import { scriptPrefs, type ScriptPref } from "@/i18n/types";
import { buttonVariants } from "@/components/ui/button-variants";
import { cn } from "@/lib/utils";
import { JapaneseText } from "./JapaneseText";

/** The five Japanese display modes with a live preview of 練習 (docs/06 §7). Saved to the profile on change. */
export function ScriptToggle() {
  const { scriptPref, setScriptPref } = usePrefs();
  const t = useT();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        className={cn(buttonVariants({ variant: "ghost", size: "sm" }), "h-11 gap-1 px-2")}
      >
        {/* Name as text, not aria-label: the visible glyph is decorative (WCAG 2.5.3 label in name). */}
        <span className="sr-only">{`${t(nav.japaneseDisplay)}: ${t(scriptModes[scriptPref])}`}</span>
        <span lang="ja" className="font-ja text-body font-bold" aria-hidden="true">
          あ
        </span>
        <ChevronDown aria-hidden="true" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-72">
        <DropdownMenuLabel>{t(nav.japaneseDisplay)}</DropdownMenuLabel>
        <DropdownMenuRadioGroup
          value={scriptPref}
          onValueChange={(value) => {
            setScriptPref(value as ScriptPref);
          }}
        >
          {scriptPrefs.map((mode) => (
            <DropdownMenuRadioItem key={mode} value={mode} className="flex-col items-start gap-0.5">
              <span>{t(scriptModes[mode])}</span>
              <ScriptPreview mode={mode} />
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** "Latihan · 練習" as it would look in a mode. */
export function ScriptPreview({ mode }: { mode: ScriptPref }) {
  const t = useT();
  return (
    <span className="text-caption text-ink-muted">
      {t(scriptModes.previewWord)}
      {mode !== "off" && (
        <>
          <span aria-hidden="true"> · </span>
          <JapaneseText value={scriptModes.previewWord} variant="companion" scriptPref={mode} />
        </>
      )}
    </span>
  );
}
