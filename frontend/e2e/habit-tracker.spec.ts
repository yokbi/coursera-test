import { expect, test, type Page } from "@playwright/test";

/**
 * Full user journey: register → logout → login → create one habit of each
 * type → check in → verify streak → archive → logout.
 * Runs serially in one browser context; state carries across steps.
 */
test.describe.configure({ mode: "serial" });

const email = `e2e-${Date.now()}@test.local`;
const password = "E2eStrong1x";

let page: Page;

test.beforeAll(async ({ browser }) => {
  page = await (await browser.newContext()).newPage();
});

test.afterAll(async () => {
  await page.close();
});

async function createHabit(options: {
  name: string;
  type?: "quantity" | "duration";
  target?: string;
  unit?: string;
}) {
  await page.getByRole("button", { name: "Yeni alışkanlık" }).click();
  await page.getByLabel("İsim").fill(options.name);
  if (options.type) {
    await page.getByLabel("Tür").selectOption(options.type);
    await page.getByLabel("Hedef").fill(options.target ?? "3");
    if (options.type === "quantity") {
      await page.getByLabel("Birim").fill(options.unit ?? "bardak");
    }
  }
  await page.getByRole("button", { name: "Oluştur" }).click();
  await expect(
    page.getByRole("listitem").filter({ hasText: options.name }).first(),
  ).toBeVisible();
}

test("registers a new account", async () => {
  await page.goto("/kayit");
  await page.getByLabel("E-posta").fill(email);
  await page.getByLabel("Şifre", { exact: true }).fill(password);
  await page.getByLabel("Şifre (tekrar)").fill(password);
  await page.getByRole("button", { name: "Kayıt ol" }).click();
  await expect(page.getByRole("heading", { name: "Bugünkü alışkanlıklar" })).toBeVisible();
});

test("logs out and logs back in", async () => {
  await page.getByRole("button", { name: "Çıkış yap" }).click();
  await expect(page).toHaveURL(/\/giris/);

  await page.getByLabel("E-posta").fill(email);
  await page.getByLabel("Şifre").fill(password);
  await page.getByRole("button", { name: "Giriş yap" }).click();
  await expect(page.getByRole("heading", { name: "Bugünkü alışkanlıklar" })).toBeVisible();
});

test("creates one habit of each type", async () => {
  await page.getByRole("link", { name: "Alışkanlıklar" }).click();
  await expect(page.getByRole("heading", { name: "Alışkanlıklar" })).toBeVisible();

  await createHabit({ name: "Meditasyon" });
  await createHabit({ name: "Su iç", type: "quantity", target: "2", unit: "bardak" });
  await createHabit({ name: "Kitap oku", type: "duration", target: "10" });
});

test("checks in each habit and sees the streak", async () => {
  await page.getByRole("link", { name: "Panel" }).click();
  await expect(page.getByRole("heading", { name: "Bugünkü alışkanlıklar" })).toBeVisible();

  // Boolean: single toggle completes the day.
  const meditation = page.getByRole("listitem").filter({ hasText: "Meditasyon" });
  await meditation.getByRole("button", { name: /Meditasyon/ }).click();
  await expect(meditation.getByText(/1 gün seri/)).toBeVisible();

  // Quantity: two increments reach the target of 2.
  const water = page.getByRole("listitem").filter({ hasText: "Su iç" });
  await water.getByRole("button", { name: "Su iç: Artır" }).click();
  await expect(water.getByText("1/2 bardak")).toBeVisible();
  await water.getByRole("button", { name: "Su iç: Artır" }).click();
  await expect(water.getByText("2/2 bardak")).toBeVisible();
  await expect(water.getByText(/1 gün seri/)).toBeVisible();

  // Duration: two 5-minute increments reach the target of 10.
  const reading = page.getByRole("listitem").filter({ hasText: "Kitap oku" });
  await reading.getByRole("button", { name: "Kitap oku: Artır" }).click();
  await reading.getByRole("button", { name: "Kitap oku: Artır" }).click();
  await expect(reading.getByText("10/10 dk")).toBeVisible();
  await expect(reading.getByText(/1 gün seri/)).toBeVisible();
});

test("shows streak on the stats page", async () => {
  await page.getByRole("link", { name: "Alışkanlıklar" }).click();
  await page
    .getByRole("listitem")
    .filter({ hasText: "Meditasyon" })
    .getByRole("link", { name: "İstatistikler" })
    .click();

  await expect(page.getByRole("heading", { name: "Meditasyon" })).toBeVisible();
  await expect(page.getByText("Mevcut seri").locator("..").getByText("1 gün")).toBeVisible();
});

test("archives a habit and it leaves the active list", async () => {
  await page.getByRole("link", { name: "Alışkanlıklar" }).click();
  const meditation = page.getByRole("listitem").filter({ hasText: "Meditasyon" });
  await meditation.getByRole("button", { name: "Arşivle" }).click();
  await expect(
    page.getByRole("button", { name: /Arşivlenmiş \(1\)/ }),
  ).toBeVisible();

  // The dashboard no longer offers it for check-in.
  await page.getByRole("link", { name: "Panel" }).click();
  await expect(page.getByRole("heading", { name: "Bugünkü alışkanlıklar" })).toBeVisible();
  await expect(page.getByRole("listitem").filter({ hasText: "Meditasyon" })).toHaveCount(0);
});

test("logs out and protected routes redirect to login", async () => {
  await page.getByRole("button", { name: "Çıkış yap" }).click();
  await expect(page).toHaveURL(/\/giris/);

  await page.goto("/");
  await expect(page).toHaveURL(/\/giris/);
});
