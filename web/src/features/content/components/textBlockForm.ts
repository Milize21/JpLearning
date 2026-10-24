import { z } from "zod";
import { parseFurigana } from "@/i18n/furigana";
import type { TextBlockFields } from "@/i18n/textblock";

export const textBlockSchema = z.object({
  id: z.string().trim().min(1, "required").max(2000, "tooLong"),
  en: z.string().trim().max(2000, "tooLong"),
  ja: z
    .string()
    .trim()
    .refine((value) => value === "" || parseFurigana(value).errors.length === 0, "invalidMarkup"),
});

export const emptyTextBlock: TextBlockFields = { id: "", en: "", ja: "" };
