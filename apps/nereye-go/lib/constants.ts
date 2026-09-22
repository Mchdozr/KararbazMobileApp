export const ACCENT = '#064e3b';
export const KADIKOY = { lat: 40.9885, lng: 29.025 };

export const INTENTS = ['Kahve', 'Yemek', 'Eglence', 'Kultur', 'AcikHava', 'Gece'] as const;
export const VIBES = ['Sessiz', 'Laptop', 'Manzarali', 'Romantik', 'Ucuz', 'Premium', 'CocukDostu', 'GecSaat', 'Tarihi'] as const;

export function intentLabel(intent: string) {
  switch (intent) {
    case 'Kahve':
      return 'Kahve';
    case 'Yemek':
      return 'Yemek';
    case 'Eglence':
      return 'Eğlence';
    case 'Kultur':
      return 'Kültür';
    case 'AcikHava':
      return 'Açık hava';
    case 'Gece':
      return 'Gece';
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
      return vibe;
    case 'Manzarali':
      return 'Manzaralı';
    case 'Ucuz':
      return 'Uygun';
    case 'CocukDostu':
      return 'Çocuklu';
    case 'GecSaat':
      return 'Geç saat';
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

export const DEFAULT_API = process.env.EXPO_PUBLIC_API_URL ?? 'http://192.168.1.248:5079';
