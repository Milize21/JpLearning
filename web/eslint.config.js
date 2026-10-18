import js from "@eslint/js";
import jsxA11y from "eslint-plugin-jsx-a11y";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import globals from "globals";
import tseslint from "typescript-eslint";

export default tseslint.config(
  { ignores: ["dist", "coverage", "playwright-report", "test-results", "src/lib/api/schema/**"] },
  {
    files: ["**/*.{ts,tsx}"],
    extends: [js.configs.recommended, ...tseslint.configs.strictTypeChecked],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: {
      "react-hooks": reactHooks,
      "react-refresh": reactRefresh,
      "jsx-a11y": jsxA11y,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      ...jsxA11y.flatConfigs.recommended.rules,
      "react-refresh/only-export-components": ["warn", { allowConstantExport: true }],
      "@typescript-eslint/restrict-template-expressions": ["error", { allowNumber: true }],
      // AGENTS.md §7: no `any` without a written reason.
      "@typescript-eslint/no-explicit-any": "error",
      // AGENTS.md §7: server data only through TanStack Query hooks in features/*/api.
      "no-restricted-globals": [
        "error",
        { name: "fetch", message: "Use the typed API client in features/*/api." },
      ],
    },
  },
  {
    files: ["src/lib/api/**", "src/test/**", "**/*.test.{ts,tsx}"],
    rules: { "no-restricted-globals": "off" },
  },
  {
    files: ["scripts/**/*.mjs", "*.config.{js,ts}", "e2e/**/*.ts"],
    languageOptions: { globals: globals.node },
    extends: [tseslint.configs.disableTypeChecked],
  },
);
