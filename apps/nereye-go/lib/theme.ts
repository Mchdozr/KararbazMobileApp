export const colors = {
  bg: '#0F1419',
  surface: '#1A222C',
  ink: '#F4F7FA',
  muted: '#8B9AAB',
  line: '#2E3846',
  coral: '#FF6B4A',
  coralSoft: '#3A241F',
  sage: '#3DD6C6',
  gold: '#3DD6C6',
  espresso: '#222C39',
  white: '#FFFFFF',
  overlay: 'rgba(15,20,25,0.55)',
};

export const ACCENT = colors.coral;

export const radii = { sm: 12, md: 20, lg: 28, pill: 999 };

export const space = { xs: 4, sm: 8, md: 12, lg: 16, xl: 20, xxl: 24 };

export const shadows = {
  card: {
    shadowColor: '#000000',
    shadowOpacity: 0.35,
    shadowRadius: 16,
    shadowOffset: { width: 0, height: 6 },
    elevation: 3,
  },
  sheet: {
    shadowColor: '#000000',
    shadowOpacity: 0.4,
    shadowRadius: 24,
    shadowOffset: { width: 0, height: -8 },
    elevation: 12,
  },
};

export const fonts = {
  display: 'DMSans_700Bold',
  sans: 'DMSans_400Regular',
  sansMed: 'DMSans_500Medium',
  sansBold: 'DMSans_700Bold',
};

export const INTENT_PASTELS: Record<string, string> = {
  Kahve: '#3A2A22',
  Yemek: '#2A3228',
  Eglence: '#2A2438',
  Kultur: '#243038',
  AcikHava: '#1F3030',
  Gece: '#1C2230',
};

export const INTENT_ICONS: Record<string, 'cafe' | 'restaurant' | 'musical-notes' | 'color-palette' | 'leaf' | 'moon'> = {
  Kahve: 'cafe',
  Yemek: 'restaurant',
  Eglence: 'musical-notes',
  Kultur: 'color-palette',
  AcikHava: 'leaf',
  Gece: 'moon',
};
