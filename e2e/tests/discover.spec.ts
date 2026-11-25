import { expect, test } from '@playwright/test';
import { openApp } from './helpers';

test('niyet hızla değişince eski istekler iptal edilir, liste son niyete aittir', async ({ page }) => {
  const seen: { intent: string | null; outcome: string }[] = [];
  page.on('requestfailed', (r) => {
    if (r.url().includes('/api/recommendations')) seen.push({ intent: new URL(r.url()).searchParams.get('intent'), outcome: 'iptal' });
  });
  page.on('response', (r) => {
    if (r.url().includes('/api/recommendations')) seen.push({ intent: new URL(r.url()).searchParams.get('intent'), outcome: String(r.status()) });
  });
  // Liste isteklerini yavaşlat: iptal edilecek kadar uzun sürsünler.
  await page.route(/\/api\/recommendations/, async (route) => {
    await new Promise((r) => setTimeout(r, 1200));
    await route.continue().catch(() => {});
  });

  await openApp(page, '/discover');
  await page.waitForTimeout(300);
  for (const label of ['Yemek', 'Eğlence', 'Kültür']) {
    await page.getByText(label, { exact: true }).first().click();
    await page.waitForTimeout(200);
  }

  await expect(page.getByText('Süreyya Operası').first()).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Kültür' }).or(page.getByText('Kültür', { exact: true }).first())).toBeVisible();
  const done = seen.filter((s) => s.outcome === '200').map((s) => s.intent);
  expect(done.at(-1)).toBe('Kultur');
  expect(seen.filter((s) => s.outcome === 'iptal').length).toBeGreaterThanOrEqual(2);
});
