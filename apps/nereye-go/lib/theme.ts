export const colors = {
  bg: '#F4EFE6',
  surface: '#FFFBF6',
  ink: '#1C1410',
  muted: '#6E6258',
  line: '#E6DCD0',
  coral: '#E24C1F',
  coralSoft: '#F6D3C4',
  sage: '#2C6E56',
  gold: '#C4A574',
  espresso: '#2B211C',
  white: '#FFFFFF',
  overlay: 'rgba(28,20,16,0.35)',
};

export const ACCENT = colors.coral;

export const radii = { sm: 12, md: 20, lg: 28, pill: 999 };

export const space = { xs: 4, sm: 8, md: 12, lg: 16, xl: 20, xxl: 24 };

export const shadows = {
  card: {
    shadowColor: '#1C1410',
    shadowOpacity: 0.08,
    shadowRadius: 16,
    shadowOffset: { width: 0, height: 6 },
    elevation: 3,
  },
  sheet: {
    shadowColor: '#1C1410',
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
  Kahve: '#E8D3B8',
  Yemek: '#E8C4B0',
  Eglence: '#D8C8E8',
  Kultur: '#C9DCC4',
  AcikHava: '#C5D9E6',
  Gece: '#C9C4DC',
};

export const INTENT_ICONS: Record<string, 'cafe' | 'restaurant' | 'musical-notes' | 'color-palette' | 'leaf' | 'moon'> = {
  Kahve: 'cafe',
  Yemek: 'restaurant',
  Eglence: 'musical-notes',
  Kultur: 'color-palette',
  AcikHava: 'leaf',
  Gece: 'moon',
};
