import { expect, type Page } from '@playwright/test';
import { API } from '../playwright.config';

export { API };

/** Karşılama ekranını atlayıp (yerel depoya işaret) uygulamayı açar. Ek anahtarlar da yazılabilir. */
export async function openApp(page: Page, path = '/', storage: Record<string, string> = {}) {
  await page.addInitScript((entries) => {
    localStorage.setItem('nereye.onboardedAt', new Date().toISOString());
    for (const [k, v] of Object.entries(entries)) {
      localStorage.setItem(k, v);
    }
  }, storage);
  await page.goto(path, { waitUntil: 'domcontentloaded' });
}

/** İlk öneri kartı görünene dek bekler. */
export async function waitForCards(page: Page) {
  await expect(page.getByText('Sana özel')).toBeVisible();
  await expect(page.getByText('Petra Roasting Co.').first()).toBeVisible();
}

export async function apiJson<T>(path: string, init: RequestInit = {}): Promise<{ status: number; body: T }> {
  const res = await fetch(API + path, { ...init, headers: { 'Content-Type': 'application/json', ...(init.headers ?? {}) } });
  const text = await res.text();
  return { status: res.status, body: (text ? JSON.parse(text) : null) as T };
}
