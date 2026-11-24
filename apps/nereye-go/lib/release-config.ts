// API adresi kuralları. React Native içe aktarmaz: node ile test edilir.

/** Yalnızca geliştirmede (Expo Go / __DEV__) denenen yerel ağ adresleri. */
export const DEV_LAN_API = 'http://192.168.1.248:5079';
export const DEV_FALLBACK_APIS = ['http://10.0.2.2:5079', 'http://localhost:5079'] as const;

export function isHttps(url?: string | null): url is string {
  return typeof url === 'string' && /^https:\/\/[^\s/]+/i.test(url.trim());
}

/** Aynı makinedeki sunucu (yerel web önizlemesi). Ağdan dinlenemez; telefon derlemesinde işe yaramaz. */
export function isLoopback(url?: string | null): url is string {
  return typeof url === 'string' && /^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/i.test(url.trim());
}

/**
 * Uygulamanın kullanacağı API adresi.
 * - Geliştirmede: ortam değişkeni varsa o, yoksa yerel ağ adresi.
 * - Yayında: yalnızca https ile başlayan, derlemeye verilmiş adres. Yoksa boş döner;
 *   uygulama ağ hatası gösterir ama asla düz HTTP'ye ya da LAN'a düşmez.
 */
export function resolveApiUrl(dev: boolean, envUrl?: string | null): string {
  const given = envUrl?.trim().replace(/\/+$/, '') ?? '';
  if (dev) {
    return given || DEV_LAN_API;
  }
  // Yayın derlemesinde https zorunlu; tek istisna yerel web önizlemesi için loopback (EAS derlemesi bunu
  // zaten app.config.ts'te reddeder: production/preview için adres https olmalı).
  return isHttps(given) || isLoopback(given) ? given : '';
}

/**
 * Açılışta adres yoklaması gerekli mi? Tek aday varken (yayın derlemesi) yoklama yalnızca ilk isteği bir gidiş-dönüş
 * geciktirir; ağ yokken açılışı zaman aşımı kadar bekletir. Seçilecek başka adres olmadığı için atlanır.
 */
export function needsProbe(candidates: readonly string[]): boolean {
  return candidates.length > 1;
}

/** Adres seçiminde denenecek adaylar. Yayında yalnızca derlemeye verilen https adres. */
export function apiCandidates(dev: boolean, defaultApi: string, saved?: string | null): string[] {
  const pool = dev ? [saved, defaultApi, ...DEV_FALLBACK_APIS] : [defaultApi];
  return pool
    .map((u) => u?.trim().replace(/\/+$/, '') ?? '')
    .filter((u, i, all) => (dev ? u.startsWith('http') : isHttps(u) || isLoopback(u)) && all.indexOf(u) === i);
}
