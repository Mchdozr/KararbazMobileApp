import { expect, test } from '@playwright/test';
import { API, openApp, waitForCards } from './helpers';

test('arkadaşlarla karar: uygulamada oylama açılır, web sayfasından gelen oy canlı görünür', async ({ page, browser }) => {
  await openApp(page, '/', { 'analytics.consent': 'denied' });
  await waitForCards(page);
  await page.getByRole('button', { name: /Benim için seç/ }).click();
  await page.getByRole('button', { name: /Arkadaşlarla karar ver/ }).click();
  await expect(page).toHaveURL(/\/poll\/[a-z2-9]{8}/);
  const code = page.url().split('/poll/')[1].slice(0, 8);
  await expect(page.getByText('Bir mekana dokunarak oy ver.')).toBeVisible();

  // Uygulamadan oy.
  const options = page.getByRole('button', { name: /, 0 oy/ });
  await expect(options.first()).toBeVisible();
  await options.first().click();
  await expect(page.getByText(/1 oy ·/)).toBeVisible();
  await expect(page.getByRole('button', { name: /senin oyun/ })).toHaveCount(1);

  // Uygulaması olmayan arkadaş: web sayfasından ikinci seçeneğe oy.
  const friend = await (await browser.newContext()).newPage();
  await friend.goto(`${API}/k/${code}`);
  await expect(friend.locator('h1')).toContainText('için nereye?');
  const [voteRes] = await Promise.all([
    friend.waitForResponse((r) => r.url().includes('/votes') && r.request().method() === 'POST'),
    friend.locator('.opt').nth(1).click(),
  ]);
  // Oy yanıtı tazelemeyi beklemeden doğru sayıyı vermeli (çift sayım olmasın).
  expect(((await voteRes.json()) as { totalVotes: number }).totalVotes).toBe(2);
  await expect(friend.locator('#total')).toHaveText('2 oy');
  await expect(friend.locator('.opt.mine')).toHaveCount(1);

  // Uygulama 5 sn'de bir tazelenir.
  await expect(page.getByText(/2 oy ·/)).toBeVisible({ timeout: 12_000 });
});
