import { ChevronDown } from "lucide-react";
import { buttonVariants } from "@/components/ui/button-variants";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { languages, nav } from "@/i18n/labels/nav";
import { usePrefs, useT } from "@/i18n/prefs";
import { formatLabel } from "@/i18n/tb";
import { uiLocales, type UiLocale } from "@/i18n/types";
import { cn } from "@/lib/utils";

/** ID / EN (docs/06 §7). The language code itself is shown, not translated. */
export function LanguageSwitcher() {
  const { uiLocale, setUiLocale } = usePrefs();
  const t = useT();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        className={cn(buttonVariants({ variant: "ghost", size: "sm" }), "h-11 gap-1 px-2")}
      >
        {/* Name as text, not aria-label: the visible glyph is decorative (WCAG 2.5.3 label in name). */}
        <span className="sr-only">{`${t(nav.language)}: ${t(languages[uiLocale])}`}</span>
        <span className="text-body-sm font-bold" aria-hidden="true">
          {uiLocale.toUpperCase()}
        </span>
        <ChevronDown aria-hidden="true" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel>{t(nav.language)}</DropdownMenuLabel>
        <DropdownMenuRadioGroup
          value={uiLocale}
          onValueChange={(value) => {
            setUiLocale(value as UiLocale);
          }}
        >
          {uiLocales.map((locale) => (
            <DropdownMenuRadioItem key={locale} value={locale} lang={locale}>
              {/* Each language is named in itself, so it can be found in either UI language. */}
              {formatLabel(languages[locale], locale)}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
