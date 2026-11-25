import { expect, test } from '@playwright/test';
import { apiJson, openApp } from './helpers';

test('arama sonuç getirir ve mekana gider', async ({ page }) => {
  await openApp(page, '/search');
  await page.getByPlaceholder(/Mekan|ara/i).first().fill('petra');
  await expect(page.getByText('Petra Roasting Co.').first()).toBeVisible();
});

test('oturumlu kullanıcı profilinde adını görür ve mekan kaydeder', async ({ page }) => {
  const email = `e2e${Date.now()}${Math.floor(Math.random() * 1000)}@example.com`;
  const reg = await apiJson<Record<string, unknown>>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify({ email, password: 'E2e.Deneme2026!', displayName: 'Deneme Kişi', acceptTerms: true }),
  });
  expect(reg.status).toBe(200);
  await openApp(page, '/profile', { session: JSON.stringify(reg.body), 'analytics.consent': 'denied' });
  await expect(page.getByText('Deneme Kişi').first()).toBeVisible();

  await page.goto('/venue/nereye:petra');
  await page.getByRole('button', { name: /Kaydet|kaydet/ }).first().click();
  await expect(page.getByText('Kaydedildi')).toBeVisible();
  const favs = await apiJson<{ placeId: string }[]>('/api/me/favorites', {
    headers: { Authorization: `Bearer ${String(reg.body.accessToken)}` },
  });
  expect(favs.body.some((f) => f.placeId === 'nereye:petra')).toBe(true);
});
