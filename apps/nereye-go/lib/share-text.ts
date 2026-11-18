// Saf metin üretimi: react-native'e bağlı değil, node testlerinde çalışır.
/**
 * Paylaşım bağlantısı. Alan adı yokken uygulama şeması (nereyego://venue/{id}) kullanılır; Nereye kurulu
 * telefonda mekan sayfasını açar. https adresi ancak insan EXPO_PUBLIC_SHARE_BASE_URL'i (https) verdiyse eklenir.
 * Harita sitesine (openstreetmap.org vb.) bağlantı verilmez.
 */
export function venueShareText(
  placeId: string,
  name: string,
  address: string | null | undefined,
  webBase: string | null | undefined = process.env.EXPO_PUBLIC_SHARE_BASE_URL,
) {
  const where = address ? ` — ${address}` : '';
  const id = encodeURIComponent(placeId);
  const lines = [`${name}${where}`, `nereyego://venue/${id}`];
  const base = httpsBase(webBase);
  if (base) {
    lines.push(`${base}/venue/${id}`);
  }
  lines.push('Nereye ile buldum.');
  return lines.join('\n');
}

function httpsBase(value: string | null | undefined): string | null {
  const v = value?.trim().replace(/\/+$/, '');
  return v && /^https:\/\/[^\s/]+/i.test(v) ? v : null;
}
