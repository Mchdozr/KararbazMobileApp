// Renk paletleri (açık ve koyu). Bileşenler doğrudan buraya değil lib/theme.ts'e bakar.
// Kontrast kuralları lib/palette.test.ts'de: metin ve düğmeler WCAG AA (4,5:1).
// React Native içe aktarmaz: node ile test edilir.

export type ThemeMode = 'system' | 'light' | 'dark';
export type Scheme = 'light' | 'dark';

export const lightColors = {
  /** Sıcak taş zemin: kartların beyazı üstünde ayrışsın. */
  bg: '#f6f2ea',
  surface: '#ffffff',
  /** Kart içi ikincil yüzey (çip, ikon arka planı). */
  surfaceAlt: '#f1ece2',
  /** Metin: yeşile çalan koyu ton; saf yeşilden daha okunaklı. */
  ink: '#10291f',
  muted: '#5a6e65',
  line: '#e6dfd2',
  /** Tarihsel ad: marka yeşili (birincil eylem, bağlantı, vurgu metni). */
  coral: '#0b5d45',
  /** Marka yeşili zemin üstündeki yazı ve ikon. */
  onCoral: '#FFFFFF',
  coralSoft: '#dcebe2',
  sage: '#15845f',
  /** Puan yıldızı ve öne çıkan rozet. */
  gold: '#d9973a',
  /** Koyu yeşil zemin (bant, seçili yarıçap); iki temada da koyu, üstünde beyaz yazı. */
  espresso: '#0a2f24',
  /** Sıcak vurgu: "Tümü", seçili sekme noktası, akşam bandı. Birincil eylem için kullanılmaz. */
  accent: '#e07b39',
  onAccent: '#2b1306',
  accentSoft: '#fbe8d8',
  white: '#FFFFFF',
  overlay: 'rgba(10,47,36,0.35)',
  /** Hata ve yıkıcı eylemler. Marka yeşiliyle karışmasın diye ayrı bir ton. */
  danger: '#b3412a',
  onDanger: '#FFFFFF',
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

export type Colors = typeof lightColors;

/** Koyu tema: zemin koyu yeşil-gri, birincil açık yeşil (üstünde koyu yazı), hata açık mercan. */
export const darkColors: Colors = {
  bg: '#0e1915',
  surface: '#15231e',
  surfaceAlt: '#1c2d27',
  ink: '#e8f1ec',
  muted: '#a3b8ae',
  line: '#2a3d35',
  coral: '#62c79f',
  onCoral: '#06201a',
  coralSoft: '#1d3a2f',
  sage: '#4cc596',
  gold: '#e7ad57',
  espresso: '#0a2f24',
  accent: '#f0954f',
  onAccent: '#2b1306',
  accentSoft: '#3a2a1e',
  white: '#FFFFFF',
  overlay: 'rgba(0,0,0,0.45)',
  danger: '#ef8a74',
  onDanger: '#2a0d07',
  dangerSoft: '#3d2420',
  scrim: 'rgba(5,16,12,0.78)',
  scrimSoft: 'rgba(5,16,12,0.24)',
  glass: 'rgba(5,16,12,0.5)',
  glassLight: 'rgba(255,255,255,0.12)',
  glassLine: 'rgba(255,255,255,0.18)',
  onDarkMuted: 'rgba(255,255,255,0.78)',
};

export const lightPastels: Record<string, string> = {
  Kahve: '#f6e6d2',
  Yemek: '#f8e1cf',
  Eglence: '#e6e0f2',
  Kultur: '#dfe9d8',
  AcikHava: '#d6ebe2',
  Gece: '#d9e0ea',
};

export const darkPastels: Record<string, string> = {
  Kahve: '#3a2f22',
  Yemek: '#3b2c22',
  Eglence: '#2e2a3a',
  Kultur: '#26332a',
  AcikHava: '#1f3a31',
  Gece: '#232b38',
};

export function resolveScheme(mode: string | null | undefined, system: string | null | undefined): Scheme {
  if (mode === 'light' || mode === 'dark') {
    return mode;
  }
  return system === 'dark' ? 'dark' : 'light';
}

function luminance(hex: string): number {
  const h = hex.replace('#', '');
  const [r, g, b] = [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16) / 255);
  const f = (c: number) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}

export function contrast(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}
