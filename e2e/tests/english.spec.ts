import { expect, test } from '@playwright/test';
import { openApp } from './helpers';

test.use({ locale: 'en-US' });

test('Türkçe olmayan cihazda arayüz İngilizce; tercih Türkçeye çevirir', async ({ page }) => {
  await openApp(page, '/', { 'analytics.consent': 'denied' });
  await expect(page.getByText('For you')).toBeVisible();
  await expect(page.getByRole('button', { name: /Pick for me/ })).toBeVisible();
  await expect(page.getByText(/Open until 23:00|Open now|Closed now|Opens/).first()).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');

  // Profil'den Türkçe seçilince sayfa yenilenir ve arayüz Türkçe olur.
  await page.goto('/profile');
  await page.getByRole('button', { name: 'Language: Türkçe' }).click();
  await expect(page.getByText('Sana özel').or(page.getByText('Profil').first())).toBeVisible();
  await page.goto('/');
  await expect(page.getByText('Sana özel')).toBeVisible();
});
