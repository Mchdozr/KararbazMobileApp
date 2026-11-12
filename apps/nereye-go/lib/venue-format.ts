// Mekan kartı ve detayında kullanılan saf biçimlendiriciler. React Native içe aktarmaz: node ile test edilir.

/** 0 = ücretsiz, 1-4 = ₺ … ₺₺₺₺. Bilinmiyorsa boş. */
export function priceLabel(level?: number | null): string {
  if (level == null || level < 0 || level > 4) {
    return '';
  }
  return level === 0 ? 'Ücretsiz' : '₺'.repeat(level);
}

export function priceWord(level?: number | null): string {
  return ['Ücretsiz', 'Uygun', 'Orta', 'Pahalı', 'Çok pahalı'][level ?? -1] ?? '';
}

/** 4898 → "4,9 B", 950 → "950", 1200000 → "1,2 Mn". */
export function compactCount(n?: number | null): string {
  if (n == null || !Number.isFinite(n) || n <= 0) {
    return '';
  }
  if (n < 1000) {
    return String(Math.round(n));
  }
  const [value, unit] = n < 1_000_000 ? [n / 1000, 'B'] : [n / 1_000_000, 'Mn'];
  const text = value >= 100 ? String(Math.round(value)) : (Math.round(value * 10) / 10).toString().replace('.', ',');
  return `${text} ${unit}`;
}

export type RatingLine = { value: string; count: string; source: 'nereye' | 'google' } | null;

/**
 * Kartta tek bir puan gösterilir. Nereye topluluğunun puanı varsa o; yoksa Google puanı.
 * İkisi de yoksa null: "Yeni" etiketi çıkar.
 */
export function ratingLine(v: {
  nereyeRating?: number | null;
  nereyeRatingCount?: number | null;
  googleRating?: number | null;
  googleRatingCount?: number | null;
}): RatingLine {
  if (v.nereyeRating != null && (v.nereyeRatingCount ?? 0) > 0) {
    return { value: v.nereyeRating.toFixed(1).replace('.', ','), count: compactCount(v.nereyeRatingCount), source: 'nereye' };
  }
  if (v.googleRating != null && v.googleRating > 0) {
    return { value: v.googleRating.toFixed(1).replace('.', ','), count: compactCount(v.googleRatingCount), source: 'google' };
  }
  return null;
}

/** "Kahve dükkanı · ₺₺" gibi tek satır. */
export function metaLine(category?: string | null, priceLevel?: number | null): string {
  return [category?.trim(), priceLabel(priceLevel)].filter(Boolean).join(' · ');
}

const FEATURE_ICONS: Record<string, string> = {
  'Açık alan': 'sunny-outline',
  'Açık oturma': 'sunny-outline',
  Rezervasyon: 'calendar-outline',
  Kahvaltı: 'egg-outline',
  Brunch: 'restaurant-outline',
  Kahve: 'cafe-outline',
  Tatlı: 'ice-cream-outline',
  'Vejetaryen seçenek': 'leaf-outline',
  'Canlı müzik': 'musical-notes-outline',
  'Gruplara uygun': 'people-outline',
  'Çocuk dostu': 'happy-outline',
  'Evcil hayvan dostu': 'paw-outline',
  'Paket servis': 'bag-handle-outline',
  'Eve teslimat': 'bicycle-outline',
  'Mekanda servis': 'restaurant-outline',
  Tuvalet: 'water-outline',
  'Alkollü içecek': 'wine-outline',
  'Tekerlekli sandalyeye uygun': 'accessibility-outline',
  'Kart geçerli': 'card-outline',
  Otopark: 'car-outline',
  'Wi-Fi': 'wifi-outline',
};

export function featureIcon(label: string): string {
  return FEATURE_ICONS[label] ?? 'checkmark-circle-outline';
}

/** Uzun yorumu kelime ortasında kesmeden kısaltır. */
export function clip(text: string, max: number): { text: string; clipped: boolean } {
  const clean = text.trim();
  if (clean.length <= max) {
    return { text: clean, clipped: false };
  }
  const cut = clean.slice(0, max);
  const space = cut.lastIndexOf(' ');
  return { text: `${(space > max * 0.6 ? cut.slice(0, space) : cut).trimEnd()}…`, clipped: true };
}
