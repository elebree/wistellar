import { defineConfig } from "@playwright/test";

// End-to-end tests for the SPA. They run against a production build served by `vite preview` and
// stub every /api and /geo request (e2e/stub-api.ts), so they need neither the .NET server nor a
// database.
const baseURL = "https://localhost:5179";

export default defineConfig({
  testDir: "e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL,
    // Vite serves the ASP.NET dev certificate, which the test browser doesn't trust: it is only
    // trusted where `dotnet dev-certs https --trust` has run, and never on a CI runner.
    ignoreHTTPSErrors: true,
    // ignoreHTTPSErrors doesn't reach the service worker: Chrome refuses to register one whose
    // script came with a certificate error unless this switch is set, so e2e/pwa.spec.ts would
    // time out waiting for it wherever the certificate isn't trusted.
    launchOptions: { args: ["--ignore-certificate-errors"] },
    // The installed Chrome, so there is no browser to download. Drop this line to use Playwright's
    // own Chromium instead (after `pnpm exec playwright install chromium`).
    channel: "chrome",
    viewport: { width: 1280, height: 800 },
    serviceWorkers: "block",
    trace: "retain-on-failure",
  },
  webServer: {
    // A build rather than the dev server: on a cold start, the dev server compiles on demand and
    // re-optimizes dependencies, reloading pages in the middle of tests. Port 5179 keeps 5173 free
    // for `pnpm dev`. Never reused, so a run always tests the current code.
    command: "pnpm exec vite build && pnpm exec vite preview --port 5179 --strictPort",
    url: baseURL,
    ignoreHTTPSErrors: true,
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
