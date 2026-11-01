import type { Page, Request } from "@playwright/test";
import { readFileSync } from "node:fs";
import { gzipSync } from "node:zlib";
import { test, expect, stubToken } from "./stub-api";
import { fillInSignIn, mapQuery, menuButton, visitMap, waitForNetworkAtCenter } from "./map";

const uploadUrl = /\/api\/v2\/file\/upload$/;
const statisticsUrl = /\/api\/v2\/file\/transactions\?/;
const token = stubToken();
const importDialog = (page: Page) => page.getByRole("dialog", { name: "Import files", exact: true });
const submit = (page: Page) => importDialog(page).locator('button[type="submit"]');
const file = (name: string, content = "observations") => ({
  name,
  mimeType: "application/octet-stream",
  buffer: Buffer.from(content),
});

async function openImport(page: Page) {
  await menuButton(page).click();
  await page.getByRole("menuitem", { name: "Import files" }).click();
  await expect(importDialog(page)).toBeVisible();
}

test.describe("file import", () => {
  test.beforeEach(async ({ page }) => {
    await page.route(uploadUrl, (route) => route.fulfill({ json: { success: true, warning: "" } }));
    await page.route(statisticsUrl, (route) => route.fulfill({ json: { success: true } }));
    await visitMap(page, { token });
  });

  test("opens a modal from the menu and restores focus on close", async ({ page }) => {
    await openImport(page);
    expect(await importDialog(page).evaluate((el) => el.matches(":modal"))).toBe(true);
    await expect(importDialog(page).getByLabel("Choose files")).toBeFocused();
    await expect(submit(page)).toBeDisabled();
    await expect(page.getByRole("menu")).toHaveCount(0);
    await page.keyboard.press("Escape");
    await expect(importDialog(page)).toHaveCount(0);
    await expect(menuButton(page)).toBeFocused();

    await openImport(page);
    await importDialog(page).getByRole("button", { name: "Cancel" }).click();
    await expect(importDialog(page)).toHaveCount(0);
    await openImport(page);
    await page.mouse.click(10, 10);
    await expect(importDialog(page)).toHaveCount(0);
  });

  test("uploads files in order with authentication, updates statistics and reloads tiles", async ({ page, api }) => {
    const uploads: Request[] = [];
    const order: string[] = [];
    let releaseFirst!: () => void;
    const firstPending = new Promise<void>((resolve) => (releaseFirst = resolve));
    await page.route(uploadUrl, async (route) => {
      uploads.push(route.request());
      order.push("upload");
      if (uploads.length === 1) await firstPending;
      await route.fulfill({ json: { success: true, warning: "" } });
    });
    await page.route(statisticsUrl, async (route) => {
      expect(new URL(route.request().url()).searchParams.get("pagestart")).toBe("0");
      expect(await route.request().headerValue("authorization")).toBe(`Bearer ${token}`);
      order.push("statistics");
      await route.fulfill({ json: { success: true } });
    });
    await waitForNetworkAtCenter(page);
    const tileCount = api.tiles.length;
    const styleCount = api.style.length;
    const queryBefore = mapQuery(page);
    await openImport(page);
    await importDialog(page).getByLabel("Choose files").setInputFiles([
      file("survey.csv"), file("cells.csv.gz"), file("surveys.zip"),
    ]);
    await submit(page).click();
    await expect.poll(() => uploads.length).toBe(1);
    await expect(submit(page)).toBeDisabled();
    await expect(importDialog(page).getByLabel("Choose files")).toBeDisabled();
    await expect(importDialog(page).getByRole("button", { name: "Cancel" })).toBeDisabled();
    await expect(importDialog(page).getByRole("progressbar")).toBeVisible();
    await page.keyboard.press("Escape");
    await expect(importDialog(page)).toBeVisible();
    expect(uploads).toHaveLength(1);
    releaseFirst();

    await expect(importDialog(page).getByRole("status")).toHaveText("3 of 3 files imported.");
    expect(order).toEqual(["upload", "upload", "upload", "statistics"]);
    for (const [index, name] of ["survey.csv", "cells.csv.gz", "surveys.zip"].entries()) {
      const request = uploads[index];
      expect(request.method()).toBe("POST");
      expect(await request.headerValue("authorization")).toBe(`Bearer ${token}`);
      expect(await request.headerValue("content-type")).toContain("multipart/form-data; boundary=");
      expect(request.postDataBuffer()!.toString()).toContain(`filename="${name}"`);
    }
    await expect.poll(() => api.tiles.length).toBeGreaterThan(tileCount);
    expect(api.style).toHaveLength(styleCount);
    expect(mapQuery(page)).toEqual(queryBefore);
    await expect(submit(page)).toBeDisabled();
  });

  test("sends KML unchanged as multipart data", async ({ page }) => {
    const content = '<?xml version="1.0"?><kml><Document /></kml>';
    const requestPending = page.waitForRequest(uploadUrl);
    await openImport(page);
    await importDialog(page).getByLabel("Choose files").setInputFiles(file("export.kml", content));
    await submit(page).click();
    const request = await requestPending;
    expect(await request.headerValue("content-type")).toContain("multipart/form-data; boundary=");
    expect(await request.headerValue("authorization")).toBe(`Bearer ${token}`);
    expect(request.postDataBuffer()!.toString()).toContain('filename="export.kml"');
    expect(request.postDataBuffer()!.toString()).toContain(content);
    await expect(importDialog(page).getByRole("status")).toHaveText("1 of 1 file imported.");
  });

  for (const name of ["survey.kmz", "survey.KMZ", "survey.kml.gz", "backup.sqlite"]) {
    test(`uploads ${name} without extracting it in the browser`, async ({ page }) => {
      const buffer = name.toLowerCase().endsWith(".kmz")
        ? readFileSync(new URL("./fixtures/survey.kmz", import.meta.url))
        : name.endsWith(".gz") ? gzipSync("<kml><Document /></kml>") : Buffer.from("SQLite format 3\0");
      const requestPending = page.waitForRequest(uploadUrl);
      await openImport(page);
      await importDialog(page).getByLabel("Choose files").setInputFiles({
        name, mimeType: "application/octet-stream", buffer,
      });
      await submit(page).click();
      const request = await requestPending;
      expect(await request.headerValue("content-type")).toContain("multipart/form-data; boundary=");
      expect(await request.headerValue("authorization")).toBe(`Bearer ${token}`);
      expect(request.postDataBuffer()!.toString()).toContain(`filename="${name}"`);
      expect(request.postDataBuffer()!.includes(buffer)).toBe(true);
      await expect(importDialog(page).getByRole("status")).toHaveText("1 of 1 file imported.");
    });
  }

  test("rejects unsupported and empty files before uploading", async ({ page }) => {
    const uploads: Request[] = [];
    page.on("request", (request) => {
      if (uploadUrl.test(request.url())) uploads.push(request);
    });
    await openImport(page);
    await importDialog(page).getByLabel("Choose files").setInputFiles([
      file("backup.json"), file("export.json.gz"), file("empty.csv", ""),
    ]);
    await expect(importDialog(page).getByRole("alert")).toHaveText([
      "Unsupported file type. Choose CSV, KML, KMZ, ZIP or SQLite files, optionally gzipped.",
      "Unsupported file type. Choose CSV, KML, KMZ, ZIP or SQLite files, optionally gzipped.",
      "This file is empty.",
    ]);
    await expect(submit(page)).toBeDisabled();
    expect(uploads).toHaveLength(0);
    await importDialog(page).getByLabel("Choose files").setInputFiles(file("valid.csv"));
    await expect(importDialog(page).getByRole("alert")).toHaveCount(0);
    await expect(submit(page)).toBeEnabled();
  });

  test("handles a rejected file and retries only the failed files", async ({ page }) => {
    const names: string[] = [];
    await page.route(uploadUrl, (route) => {
      const name = route.request().postDataBuffer()!.toString().match(/filename="([^"]+)"/)![1];
      names.push(name);
      return route.fulfill({ json: {
        success: names.length !== 2,
        warning: names.length === 2 ? "Invalid content (no observations)" : "",
      } });
    });
    await openImport(page);
    await importDialog(page).getByLabel("Choose files").setInputFiles([file("good.csv"), file("retry.csv")]);
    await submit(page).click();
    await expect(importDialog(page).getByRole("status")).toHaveText("1 of 2 files imported.");
    await expect(importDialog(page).getByRole("alert")).toHaveText("Invalid content (no observations)");
    await importDialog(page).getByRole("button", { name: "Retry failed files" }).click();
    await expect(importDialog(page).getByRole("status")).toHaveText("2 of 2 files imported.");
    expect(names).toEqual(["good.csv", "retry.csv", "retry.csv"]);
  });

  test("shows archive warnings once, as plain text", async ({ page }) => {
    const warning = '<img src=x onerror="window.__importXss=1">';
    await page.route(uploadUrl, (route) => route.fulfill({ json: {
      success: true, warning: `${warning}\r\n${warning}\r\n`,
    } }));
    await openImport(page);
    await importDialog(page).getByLabel("Choose files").setInputFiles(file("mixed.zip"));
    await submit(page).click();
    await expect(importDialog(page).getByText("Imported with warnings", { exact: true })).toBeVisible();
    await expect(importDialog(page).getByRole("status").first()).toHaveText(warning);
    await expect(importDialog(page).locator("img")).toHaveCount(0);
    expect(await page.evaluate(() => (window as { __importXss?: number }).__importXss)).toBeUndefined();
  });

  for (const [status, message] of [
    [413, "The file is too large for the server."],
    [503, "The server is busy with another upload."],
    [500, "The server failed to import this file."],
  ] as const) {
    test(`explains upload error ${status}`, async ({ page }) => {
      await page.route(uploadUrl, (route) => route.fulfill({ status }));
      await openImport(page);
      await importDialog(page).getByLabel("Choose files").setInputFiles(file("survey.csv"));
      await submit(page).click();
      await expect(importDialog(page).getByRole("alert")).toContainText(message);
      await expect(submit(page)).toBeEnabled();
    });
  }

  test("keeps the imported result when updating statistics fails", async ({ page, api }) => {
    await page.route(statisticsUrl, (route) => route.fulfill({ status: 503 }));
    await waitForNetworkAtCenter(page);
    const tileCount = api.tiles.length;
    await openImport(page);
    await importDialog(page).getByLabel("Choose files").setInputFiles(file("survey.csv"));
    await submit(page).click();
    await expect(importDialog(page).getByRole("status")).toHaveText("1 of 1 file imported.");
    await expect(importDialog(page).getByRole("alert")).toContainText("Files were imported, but network statistics could not be updated.");
    await expect.poll(() => api.tiles.length).toBeGreaterThan(tileCount);
    await expect(submit(page)).toBeDisabled();
  });

  test("stops the queue and asks to sign in again when the session expires", async ({ page }) => {
    const uploads: Request[] = [];
    await page.route(uploadUrl, (route) => {
      uploads.push(route.request());
      return route.fulfill({ status: 401 });
    });
    await openImport(page);
    await importDialog(page).getByLabel("Choose files").setInputFiles([file("one.csv"), file("two.csv")]);
    await submit(page).click();
    await expect(importDialog(page)).toHaveCount(0);
    const signIn = page.getByRole("dialog", { name: "Sign in", exact: true });
    await expect(signIn).toBeVisible();
    await expect(signIn.getByRole("status")).toContainText("Your session has expired");
    expect(uploads).toHaveLength(1);
    await fillInSignIn(page);
    await signIn.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(signIn).not.toBeVisible();
    await expect(importDialog(page)).toHaveCount(0);
  });

  test("fits the dialog and long filenames on a phone", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await openImport(page);
    await importDialog(page).getByLabel("Choose files").setInputFiles(file(`${"survey".repeat(25)}.csv`));
    const box = (await importDialog(page).boundingBox())!;
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(390);
    expect(await importDialog(page).evaluate((el) => el.scrollWidth <= el.clientWidth)).toBe(true);
    await expect(submit(page)).toBeInViewport();
  });
});
