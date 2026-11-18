export const colors = {
  /** Sıcak taş zemin: kartların beyazı üstünde ayrışsın. */
  bg: '#f6f2ea',
  surface: '#ffffff',
  /** Kart içi ikincil yüzey (çip, ikon arka planı). */
  surfaceAlt: '#f1ece2',
  /** Metin: yeşile çalan koyu ton; saf yeşilden daha okunaklı. */
  ink: '#10291f',
  muted: '#5a6e65',
  line: '#e6dfd2',
  /** Tarihsel ad: marka yeşili (birincil eylem). */
  coral: '#0b5d45',
  coralSoft: '#dcebe2',
  sage: '#15845f',
  /** Puan yıldızı ve öne çıkan rozet. */
  gold: '#d9973a',
  espresso: '#0a2f24',
  /** Sıcak vurgu: "Tümü", seçili sekme noktası, akşam bandı. Birincil eylem için kullanılmaz. */
  accent: '#e07b39',
  accentSoft: '#fbe8d8',
  white: '#FFFFFF',
  overlay: 'rgba(10,47,36,0.35)',
  /** Hata ve yıkıcı eylemler. Marka yeşiliyle karışmasın diye ayrı bir ton. */
  danger: '#b3412a',
  dangerSoft: '#f8e2db',
  /** Fotoğraf üstü yazı okunurluğu için koyu perde. */
  scrim: 'rgba(8,38,29,0.74)',
  scrimSoft: 'rgba(8,38,29,0.18)',
  glass: 'rgba(8,38,29,0.42)',
  /** Koyu yeşil yüzey üstünde cam efekti (üst menü). */
  glassLight: 'rgba(255,255,255,0.14)',
  glassLine: 'rgba(255,255,255,0.22)',
  onDarkMuted: 'rgba(255,255,255,0.78)',
};

/** Üst menü ve kahraman alanların degradesi (yukarıdan aşağı). */
export const gradients = {
  hero: ['#0e6249', '#0a3d2f'] as const,
  evening: ['#0a2f24', '#061d16'] as const,
  photo: ['transparent', 'rgba(8,38,29,0.78)'] as const,
};

export const ACCENT = colors.coral;

export const radii = { sm: 12, md: 20, lg: 28, pill: 999 };

export const space = { xs: 4, sm: 8, md: 12, lg: 16, xl: 20, xxl: 24 };

export const shadows = {
  card: {
    shadowColor: '#0a2f24',
    shadowOpacity: 0.08,
    shadowRadius: 18,
    shadowOffset: { width: 0, height: 8 },
    elevation: 3,
  },
  /** Yüzen öğeler: arama kutusu, sekme çubuğu. */
  float: {
    shadowColor: '#0a2f24',
    shadowOpacity: 0.14,
    shadowRadius: 22,
    shadowOffset: { width: 0, height: 10 },
    elevation: 8,
  },
  sheet: {
    shadowColor: '#0a2f24',
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
  Kahve: '#f6e6d2',
  Yemek: '#f8e1cf',
  Eglence: '#e6e0f2',
  Kultur: '#dfe9d8',
  AcikHava: '#d6ebe2',
  Gece: '#d9e0ea',
};

export const INTENT_ICONS: Record<string, 'cafe' | 'restaurant' | 'musical-notes' | 'color-palette' | 'leaf' | 'moon'> = {
  Kahve: 'cafe',
  Yemek: 'restaurant',
  Eglence: 'musical-notes',
  Kultur: 'color-palette',
  AcikHava: 'leaf',
  Gece: 'moon',
};
