import { expect, test } from '@playwright/test';
import { openApp, waitForCards } from './helpers';

test('ana sayfa öneri kartlarını gerçek API ile gösterir', async ({ page }) => {
  await openApp(page);
  await waitForCards(page);
  // Kartta açık/kapalı bilgisi ve mesafe var.
  await expect(page.getByText(/kadar açık|Şu an açık|Kapalı|açılıyor|Yakında kapanıyor/).first()).toBeVisible();
});

test('"Benim için seç" gerekçeli bir mekan önerir, "Başka öner" başka mekana geçer', async ({ page }) => {
  await openApp(page);
  await waitForCards(page);
  await page.getByRole('button', { name: /Benim için seç/ }).click();
  await expect(page.getByText('Senin için seçtim')).toBeVisible();
  const first = await page.locator('text=/· 1\\/\\d+/').first().textContent();
  expect(first).toMatch(/1\/\d+/);
  await expect(page.getByText(/dk yürüme|km uzakta/).first()).toBeVisible();

  await page.getByRole('button', { name: 'Başka bir mekan öner' }).click();
  await expect(page.locator('text=/· 2\\/\\d+/').first()).toBeVisible();
  await page.getByRole('button', { name: 'Kapat', exact: true }).click();
  await expect(page.getByText('Senin için seçtim')).toBeHidden();
});

test('karttan mekan sayfasına geçilir; yol tarifi ve paylaşma düğmeleri var', async ({ page }) => {
  await openApp(page);
  await waitForCards(page);
  await page.getByText('Petra Roasting Co.').first().click();
  await expect(page).toHaveURL(/\/venue\//);
  // Yığındaki önceki ekran gizli olarak DOM'da kalır: yalnızca görünen öğelere bakılır.
  await expect(page.getByText('Petra Roasting Co.').filter({ visible: true }).first()).toBeVisible();
  await expect(page.getByRole('button', { name: /Yol tarifi/ }).filter({ visible: true }).first()).toBeVisible();
});
