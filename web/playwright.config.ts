import { defineConfig } from "@playwright/test";

/**
 * End-to-end tests against the real stack (docs/09 §1). Docker infrastructure must be running
 * (`docker compose -f infra/docker-compose.yml up -d`); API, Gateway and Vite are started here unless already up.
 * Uses the locally installed Chrome, so no browser download is needed.
 */
export default defineConfig({
  testDir: "./e2e",
  timeout: 90_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [["list"]],
  use: {
    baseURL: "http://localhost:5173",
    channel: "chrome",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    { name: "desktop", use: { viewport: { width: 1280, height: 800 } } },
    { name: "phone", use: { viewport: { width: 360, height: 740 }, isMobile: true, hasTouch: true } },
  ],
  webServer: [
    {
      command: "dotnet run --project ../src/Nomax.Api",
      url: "http://localhost:5080/health/live",
      reuseExistingServer: true,
      timeout: 180_000,
    },
    {
      command: "dotnet run --project ../src/Nomax.Gateway",
      url: "http://localhost:8080/health/live",
      reuseExistingServer: true,
      timeout: 180_000,
    },
    { command: "npm run dev", url: "http://localhost:5173", reuseExistingServer: true, timeout: 120_000 },
  ],
});
