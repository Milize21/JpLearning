// lint:tokens (docs/07 §15, AGENTS.md rule 6): colors and fonts come only from design tokens.
// Fails on color literals, Tailwind's built-in palette classes, arbitrary color values and literal font families in web/src.
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const root = new URL("../src/", import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1");
const skip = [/[\\/]lib[\\/]api[\\/]schema[\\/]/];

const palette =
  "slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose";
const utilities =
  "bg|text|border|border-[trblxy]|ring|ring-offset|outline|from|via|to|fill|stroke|decoration|divide|placeholder|caret|accent|shadow|inset-shadow";

const rules = [
  { name: "hex color literal", pattern: /(?<![\w&])#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})\b/g },
  { name: "color function literal", pattern: /\b(?:rgba?|hsla?|oklch|oklab|lab|lch|hwb)\(/g },
  {
    name: "Tailwind built-in palette class",
    pattern: new RegExp(`\\b(?:${utilities})-(?:${palette})-\\d{2,3}\\b`, "g"),
  },
  { name: "arbitrary color value", pattern: /-\[(?:#|rgb|hsl|oklch|color)/g },
  { name: "literal font family", pattern: /\b(?:font-family\s*:|fontFamily\s*:)/g },
];

function* files(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) yield* files(path);
    else if (/\.(?:tsx?|css)$/.test(name) && !skip.some((pattern) => pattern.test(path))) yield path;
  }
}

const problems = [];
for (const file of files(root)) {
  const lines = readFileSync(file, "utf8").split("\n");
  lines.forEach((line, index) => {
    for (const rule of rules) {
      for (const match of line.matchAll(rule.pattern)) {
        problems.push(`${relative(root, file)}:${index + 1}  ${rule.name}: ${match[0]}`);
      }
    }
  });
}

if (problems.length > 0) {
  console.error(`lint:tokens found ${problems.length} problem(s). Use design tokens (docs/07 §15):`);
  for (const problem of problems) console.error(`  src/${problem}`);
  process.exit(1);
}
console.log("lint:tokens: no color or font literals in src/");
