import { defineConfig, devices } from "@playwright/test";

/**
 * E2E against the real stack. Prerequisite: PostgreSQL running locally
 * (`docker compose up -d` at the repo root). The API and the Next.js dev
 * server are started automatically below.
 */
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? "line" : "list",
  timeout: 60_000,
  use: {
    baseURL: "http://localhost:3000",
    trace: "retain-on-failure",
    // Allow pointing at a system/preinstalled Chromium instead of downloading one.
    launchOptions: process.env.PLAYWRIGHT_CHROMIUM_PATH
      ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_PATH }
      : {},
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: [
    {
      command:
        "dotnet run --project ../backend/src/HabitTracker.Api --urls http://localhost:5000",
      url: "http://localhost:5000/health",
      reuseExistingServer: !process.env.CI,
      // A cold CI runner restores and builds the solution before the API listens.
      timeout: 240_000,
      env: { ASPNETCORE_ENVIRONMENT: "Development" },
    },
    {
      command: "npm run dev",
      url: "http://localhost:3000",
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
  ],
});
