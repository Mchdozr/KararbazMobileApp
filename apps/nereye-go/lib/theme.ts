export const colors = {
  bg: '#fbf6ee',
  surface: '#fffdf8',
  ink: '#064e3b',
  muted: '#3f6f5c',
  line: '#ebe3d2',
  coral: '#064e3b',
  coralSoft: '#d7e8dc',
  sage: '#0a6b50',
  gold: '#c4a574',
  espresso: '#04382b',
  white: '#FFFFFF',
  overlay: 'rgba(6,78,59,0.35)',
  /** Hata ve yıkıcı eylemler. Marka yeşiliyle karışmasın diye ayrı bir ton. */
  danger: '#a8402b',
  dangerSoft: '#f6e1da',
  /** Fotoğraf üstü yazı okunurluğu için koyu perde. */
  scrim: 'rgba(4,56,43,0.72)',
  scrimSoft: 'rgba(4,56,43,0.18)',
  glass: 'rgba(4,56,43,0.45)',
};

export const ACCENT = colors.coral;

export const radii = { sm: 12, md: 20, lg: 28, pill: 999 };

export const space = { xs: 4, sm: 8, md: 12, lg: 16, xl: 20, xxl: 24 };

export const shadows = {
  card: {
    shadowColor: '#064e3b',
    shadowOpacity: 0.1,
    shadowRadius: 16,
    shadowOffset: { width: 0, height: 6 },
    elevation: 3,
  },
  sheet: {
    shadowColor: '#064e3b',
    shadowOpacity: 0.16,
    shadowRadius: 24,
    shadowOffset: { width: 0, height: -8 },
    elevation: 12,
  },
};

export const fonts = {
  display: 'Fraunces_700Bold',
  sans: 'DMSans_400Regular',
  sansMed: 'DMSans_500Medium',
  sansBold: 'DMSans_700Bold',
};

export const INTENT_PASTELS: Record<string, string> = {
  Kahve: '#f8ead4',
  Yemek: '#f4e6c8',
  Eglence: '#d7e8dc',
  Kultur: '#cfe0c8',
  AcikHava: '#c9ddd4',
  Gece: '#b7cfc4',
};

export const INTENT_ICONS: Record<string, 'cafe' | 'restaurant' | 'musical-notes' | 'color-palette' | 'leaf' | 'moon'> = {
  Kahve: 'cafe',
  Yemek: 'restaurant',
  Eglence: 'musical-notes',
  Kultur: 'color-palette',
  AcikHava: 'leaf',
  Gece: 'moon',
};
