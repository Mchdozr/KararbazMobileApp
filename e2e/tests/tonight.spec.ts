import { expect, test } from '@playwright/test';
import { openApp, waitForCards } from './helpers';

test('ana sayfada yakındaki bu akşamın etkinliği görünür ve mekana götürür', async ({ page }) => {
  // Tohum etkinliği "şimdi + ~1 sa"; gece 02:30–04:00 arası bu akşam penceresinin dışına düşer.
  const hourIst = (new Date().getUTCHours() + 3) % 24;
  test.skip(hourIst >= 2 && hourIst < 4, 'Gece 02:00–04:00 arası tohum etkinliği bu akşam penceresinde değil');

  await openApp(page, '/', { 'analytics.consent': 'denied' });
  await waitForCards(page);
  await expect(page.getByText('Bu akşam yakında')).toBeVisible();
  const card = page.getByRole('button', { name: /Akustik akşam, Süreyya Operası/ });
  await expect(card).toBeVisible();
  await card.click();
  await expect(page).toHaveURL(/\/venue\/nereye:sureyya/);
});
