import { expect, test } from '@playwright/test';
import { openApp, waitForCards } from './helpers';

test('izin verilmeden kullanım olayı gönderilmez; izinden sonra kimliksiz olay gider', async ({ page }) => {
  const sent: string[] = [];
  page.on('request', (r) => {
    if (r.url().includes('/api/telemetry/events')) sent.push(r.postData() ?? '');
  });
  await openApp(page);
  await waitForCards(page);
  await expect(page.getByText("Kararbaz'ı geliştirmemize yardım eder misin?")).toBeVisible();
  await page.getByText('Petra Roasting Co.').first().click();
  await expect(page).toHaveURL(/\/venue\//);
  await page.waitForTimeout(1500);
  expect(sent).toHaveLength(0);

  await page.goBack();
  await page.getByRole('button', { name: 'Anonim kullanım istatistiğine izin ver' }).click();
  await expect(page.getByText("Kararbaz'ı geliştirmemize yardım eder misin?")).toBeHidden();
  await page.getByText('Petra Roasting Co.').first().click();
  // Olaylar 15 sn'de bir paketlenir.
  await expect.poll(() => sent.length, { timeout: 25_000 }).toBeGreaterThan(0);
  const body = JSON.parse(sent[0]) as { events: Record<string, string>[] };
  expect(body.events.some((e) => e.name === 'venue_view' && e.placeId === 'nereye:petra')).toBe(true);
  // Konum, kimlik ya da e-posta taşınmaz.
  expect(sent[0]).not.toMatch(/lat|lng|email|userId|token/i);
});

test('reddedilen izin kartı bir daha görünmez', async ({ page }) => {
  await openApp(page);
  await waitForCards(page);
  await page.getByRole('button', { name: 'Kullanım istatistiğine izin verme' }).click();
  await page.reload();
  await waitForCards(page);
  await expect(page.getByText("Kararbaz'ı geliştirmemize yardım eder misin?")).toHaveCount(0);
});

test('uygulama hatası kişisel veri ayıklanarak raporlanır', async ({ page }) => {
  const reports: string[] = [];
  page.on('request', (r) => {
    if (r.url().includes('/api/telemetry/errors')) reports.push(r.postData() ?? '');
  });
  await openApp(page);
  await waitForCards(page);
  await page.evaluate(() => setTimeout(() => { throw new Error('E2E hatası deniz@example.com 40.98853,29.02504'); }, 0));
  await expect.poll(() => reports.length, { timeout: 10_000 }).toBeGreaterThan(0);
  expect(reports[0]).toContain('E2E hatası');
  expect(reports[0]).not.toContain('deniz@example.com');
  expect(reports[0]).not.toContain('40.98853');
});
