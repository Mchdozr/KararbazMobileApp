import { expect, test } from '@playwright/test';
import { API, apiJson, openApp } from './helpers';

test('mekanı yeni listeye ekle, listeyi paylaş, bağlantıdan web sayfasında gör', async ({ page, browser }) => {
  const email = `liste${Date.now()}${Math.floor(Math.random() * 1000)}@example.com`;
  const reg = await apiJson<Record<string, unknown>>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify({ email, password: 'E2e.Deneme2026!', displayName: 'Liste Kişi', acceptTerms: true }),
  });
  await openApp(page, '/venue/nereye:petra', { session: JSON.stringify(reg.body), 'analytics.consent': 'denied' });

  await page.getByRole('button', { name: 'Listeye ekle' }).filter({ visible: true }).first().click();
  await expect(page.getByText('Henüz listen yok.')).toBeVisible();
  await page.getByPlaceholder('Kahve rotam').fill('Kahve rotam');
  await page.getByRole('button', { name: 'Oluştur ve ekle' }).click();
  await expect(page.getByText(/"Kahve rotam" oluşturuldu/)).toBeVisible();
  await expect(page.getByRole('checkbox', { name: /Kahve rotam, 1 mekan/ })).toBeChecked();
  await page.getByRole('button', { name: 'Tamam' }).click();

  await page.goto('/collections');
  await page.getByRole('button', { name: /Kahve rotam, 1 mekan/ }).click();
  await expect(page.getByText('Petra Roasting Co.').filter({ visible: true }).first()).toBeVisible();

  const [share] = await Promise.all([
    page.waitForResponse((r) => r.url().endsWith('/share') && r.request().method() === 'POST'),
    page.getByRole('button', { name: 'Bağlantıyla paylaş' }).click(),
  ]);
  const { code } = (await share.json()) as { code: string };
  await expect(page.getByText(/bağlantıyla paylaşılıyor/)).toBeVisible();

  const visitor = await (await browser.newContext()).newPage();
  await visitor.goto(`${API}/l/${code}`);
  await expect(visitor.locator('h1')).toHaveText('Kahve rotam');
  await expect(visitor.getByText('Petra Roasting Co.')).toBeVisible();
});
