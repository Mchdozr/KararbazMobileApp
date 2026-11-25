import { t } from '@/lib/i18n';
import { resolveApiUrl } from '@/lib/release-config';

export const ACCENT = '#0b5d45'; // lib/theme.ts colors.coral ile aynı
export const KADIKOY = { lat: 40.9885, lng: 29.025 };

export const INTENTS = ['Kahve', 'Yemek', 'Eglence', 'Kultur', 'AcikHava', 'Gece'] as const;
export const VIBES = ['Sessiz', 'Laptop', 'Manzarali', 'Romantik', 'Ucuz', 'Premium', 'CocukDostu', 'GecSaat', 'Tarihi'] as const;

export function intentLabel(intent: string) {
  switch (intent) {
    case 'Kahve':
      return t('Kahve');
    case 'Yemek':
      return t('Yemek');
    case 'Eglence':
      return t('Eğlence');
    case 'Kultur':
      return t('Kültür');
    case 'AcikHava':
      return t('Açık hava');
    case 'Gece':
      return t('Gece');
    default:
      return intent;
  }
}

export function vibeText(vibe: string) {
  switch (vibe) {
    case 'Sessiz':
    case 'Laptop':
    case 'Romantik':
    case 'Premium':
    case 'Tarihi':
      return t(vibe);
    case 'Manzarali':
      return t('Manzaralı');
    case 'Ucuz':
      return t('Uygun');
    case 'CocukDostu':
      return t('Çocuklu');
    case 'GecSaat':
      return t('Geç saat');
    default:
      return vibe;
  }
}

export function vibeLabel(vibe: (typeof VIBES)[number]) {
  return vibeText(vibe);
}

export const RADIUS_OPTIONS = [
  { meters: 800, label: '0.8 km', sub: 'Yürüme' },
  { meters: 1500, label: '1.5 km', sub: 'Mahalle' },
  { meters: 3000, label: '3 km', sub: 'İlçe' },
  { meters: 8000, label: '8 km', sub: 'Şehir' },
  { meters: 15000, label: '15 km', sub: 'Geniş' },
  { meters: 25000, label: '25 km', sub: 'Metropol' },
  { meters: 50000, label: '50 km', sub: 'Bölge' },
] as const;

/**
 * API adresi. `process.env.EXPO_PUBLIC_API_URL` derlemede gömülür (Expo yalnızca bu yazımı tanır).
 * Yayında yalnızca https kabul edilir; LAN adresi ve emülatör adresleri yalnızca __DEV__'de.
 */
export const DEFAULT_API = resolveApiUrl(__DEV__, process.env.EXPO_PUBLIC_API_URL);
