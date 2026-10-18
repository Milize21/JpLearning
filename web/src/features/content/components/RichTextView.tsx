import { ExternalLink } from "lucide-react";
import type { ReactNode } from "react";
import { JapaneseText } from "@/components/japanese/JapaneseText";
import { parseFurigana } from "@/i18n/furigana";
import { usePrefs } from "@/i18n/prefs";

/**
 * Restricted Markdown of docs/03 §3.2 rendered without any raw HTML: paragraphs, ## and ### headings, bold, italic,
 * lists, tables, quotes and https links. Japanese runs (kana, kanji, furigana markup) go through JapaneseText so they
 * follow the user's script preference. Text is always escaped by React.
 */
export function RichTextView({ value }: { value: { id: string; en?: string | null } }) {
  const { uiLocale } = usePrefs();
  const markdown = uiLocale === "en" && value.en ? value.en : value.id;
  return <div className="flex flex-col gap-3 text-body-lg text-ink">{renderBlocks(markdown)}</div>;
}

type Block =
  | { kind: "heading"; level: 2 | 3; text: string }
  | { kind: "paragraph"; text: string }
  | { kind: "quote"; text: string }
  | { kind: "list"; ordered: boolean; items: string[] }
  | { kind: "table"; header: string[]; rows: string[][] };

const tableSeparator = /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$/u;
const splitRow = (line: string) =>
  line
    .trim()
    .replace(/^\|/u, "")
    .replace(/\|$/u, "")
    .split("|")
    .map((cell) => cell.trim());

export function parseBlocks(markdown: string): Block[] {
  const lines = markdown.replace(/\r\n?/gu, "\n").split("\n");
  const blocks: Block[] = [];
  let paragraph: string[] = [];
  const flush = () => {
    if (paragraph.length > 0) blocks.push({ kind: "paragraph", text: paragraph.join(" ") });
    paragraph = [];
  };

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i] ?? "";
    const trimmed = line.trim();
    if (trimmed === "") {
      flush();
      continue;
    }

    const heading = /^(#{2,3})\s+(.*)$/u.exec(trimmed);
    if (heading) {
      flush();
      blocks.push({ kind: "heading", level: heading[1] === "##" ? 2 : 3, text: heading[2] ?? "" });
      continue;
    }

    if (trimmed.startsWith(">")) {
      flush();
      const quote: string[] = [];
      while (i < lines.length && (lines[i] ?? "").trim().startsWith(">")) {
        quote.push((lines[i] ?? "").trim().replace(/^>\s?/u, ""));
        i++;
      }
      i--;
      blocks.push({ kind: "quote", text: quote.join(" ") });
      continue;
    }

    const listItem = /^([-*]|\d+\.)\s+(.*)$/u;
    if (listItem.test(trimmed)) {
      flush();
      const ordered = /^\d+\./u.test(trimmed);
      const items: string[] = [];
      while (i < lines.length && listItem.test((lines[i] ?? "").trim())) {
        items.push(listItem.exec((lines[i] ?? "").trim())?.[2] ?? "");
        i++;
      }
      i--;
      blocks.push({ kind: "list", ordered, items });
      continue;
    }

    if (trimmed.startsWith("|") && tableSeparator.test(lines[i + 1] ?? "")) {
      flush();
      const header = splitRow(trimmed);
      const rows: string[][] = [];
      i += 2;
      while (i < lines.length && (lines[i] ?? "").trim().startsWith("|")) {
        rows.push(splitRow(lines[i] ?? ""));
        i++;
      }
      i--;
      blocks.push({ kind: "table", header, rows });
      continue;
    }

    paragraph.push(trimmed);
  }

  flush();
  return blocks;
}

function renderBlocks(markdown: string): ReactNode[] {
  return parseBlocks(markdown).map((block, index) => {
    switch (block.kind) {
      case "heading":
        return block.level === 2 ? (
          <h2 key={index} className="mt-2 text-h3 font-bold">
            {renderInline(block.text)}
          </h2>
        ) : (
          <h3 key={index} className="mt-1 text-body-lg font-semibold">
            {renderInline(block.text)}
          </h3>
        );
      case "quote":
        return (
          <blockquote key={index} className="border-l-4 border-line-strong pl-4 text-ink-muted">
            {renderInline(block.text)}
          </blockquote>
        );
      case "list": {
        const items = block.items.map((item, itemIndex) => <li key={itemIndex}>{renderInline(item)}</li>);
        return block.ordered ? (
          <ol key={index} className="list-decimal pl-6">
            {items}
          </ol>
        ) : (
          <ul key={index} className="list-disc pl-6">
            {items}
          </ul>
        );
      }
      case "table":
        return (
          <div key={index} className="overflow-x-auto">
            <table className="w-full border-collapse text-body-sm">
              <thead>
                <tr>
                  {block.header.map((cell, cellIndex) => (
                    <th key={cellIndex} className="border border-line bg-canvas-subtle px-3 py-2 text-left font-semibold">
                      {renderInline(cell)}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {block.rows.map((row, rowIndex) => (
                  <tr key={rowIndex}>
                    {row.map((cell, cellIndex) => (
                      <td key={cellIndex} className="border border-line px-3 py-2">
                        {renderInline(cell)}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        );
      default:
        return <p key={index}>{renderInline(block.text)}</p>;
    }
  });
}

// Japanese characters (kana, kanji, Japanese punctuation, full-width forms) and furigana markup groups; a run may contain
// single spaces, which are word boundaries for romaji.
const JA = String.raw`\{[^{}\n]*\}|[　-ヿ㐀-䶿一-鿿＀-￯々〆ヶ]`;
const inlinePattern = new RegExp(
  String.raw`\[([^\]]+)\]\((https:\/\/[^\s)]+)\)|\*\*([^*]+)\*\*|\*([^*\s][^*]*)\*|((?:${JA})(?: ?(?:${JA}))*)`,
  "gu",
);

export function renderInline(text: string): ReactNode[] {
  const nodes: ReactNode[] = [];
  let last = 0;
  let key = 0;
  for (const match of text.matchAll(inlinePattern)) {
    const index = match.index;
    if (index > last) nodes.push(text.slice(last, index));
    const [whole, linkText, linkUrl, bold, italic, japanese] = match;
    if (linkText !== undefined && linkUrl !== undefined) {
      nodes.push(
        <a
          key={key++}
          href={linkUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1 font-medium text-action underline underline-offset-4"
        >
          {renderInline(linkText)}
          <ExternalLink className="size-3.5" aria-hidden="true" />
        </a>,
      );
    } else if (bold !== undefined) {
      nodes.push(<strong key={key++}>{renderInline(bold)}</strong>);
    } else if (italic !== undefined) {
      nodes.push(<em key={key++}>{renderInline(italic)}</em>);
    } else if (japanese !== undefined) {
      const parsed = parseFurigana(japanese);
      nodes.push(parsed.errors.length > 0 ? japanese : <JapaneseText key={key++} value={parsed.segments} variant="content" />);
    } else {
      nodes.push(whole);
    }
    last = index + whole.length;
  }
  if (last < text.length) nodes.push(text.slice(last));
  return nodes;
}
