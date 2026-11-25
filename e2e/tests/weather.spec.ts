import { expect, test } from '@playwright/test';
import { API, openApp, waitForCards } from './helpers';

test('yağmurda ana sayfa açık hava mekanlarının geri alındığını söyler', async ({ page }) => {
  await openApp(page, '/', { 'analytics.consent': 'denied' });
  await waitForCards(page);
  await expect(page.getByText(/Yağmur yağıyor \(12°\)\. Açık hava mekanlarını geriye/)).toBeVisible();
});

test('açık hava listesinde yağmur notu kültüre geçişi önerir', async () => {
  const res = await fetch(`${API}/api/recommendations?lat=40.9885&lng=29.025&intent=AcikHava&radiusMeters=5000`);
  const body = (await res.json()) as { weather: { kind: string; suggestIntent: string; note: string } };
  expect(body.weather.kind).toBe('Rain');
  expect(body.weather.suggestIntent).toBe('Kultur');
});
