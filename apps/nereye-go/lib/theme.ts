import { darkColors, darkPastels, lightColors, lightPastels, resolveScheme, type Scheme, type ThemeMode } from '@/lib/palette';
import { getItemSync, setItem } from '@/lib/storage';
import { Appearance, Platform } from 'react-native';

/**
 * Tema açılışta seçilir: Profil'deki tercih (Sistem / Açık / Koyu), "Sistem" ise cihazın teması.
 * Bileşen stilleri modül yüklenirken oluşturulduğu için değişiklik web'de sayfa yenilenince, yerel uygulamada
 * bir sonraki açılışta uygulanır.
 */
const THEME_KEY = 'theme.mode';
const stored = getItemSync(THEME_KEY);
export const themeMode: ThemeMode = stored === 'light' || stored === 'dark' ? stored : 'system';
export const scheme: Scheme = resolveScheme(themeMode, Appearance.getColorScheme());

export const colors = scheme === 'dark' ? darkColors : lightColors;

/** Zemin üstündeki durum çubuğu yazısı. */
export const statusBarOnBg: 'light' | 'dark' = scheme === 'dark' ? 'light' : 'dark';

/** Tercihi kaydeder. Web'de sayfayı yeniler; yerelde true döner (bir sonraki açılışta uygulanır). */
export async function setThemeMode(mode: ThemeMode): Promise<boolean> {
  await setItem(THEME_KEY, mode);
  if (Platform.OS === 'web' && typeof window !== 'undefined') {
    window.location.reload();
    return false;
  }
  return resolveScheme(mode, Appearance.getColorScheme()) !== scheme;
}

/** Üst menü ve kahraman alanların degradesi (yukarıdan aşağı). İki temada da koyu yeşil. */
export const gradients = {
  hero: ['#0e6249', '#0a3d2f'] as const,
  evening: ['#0a2f24', '#061d16'] as const,
  photo: ['transparent', 'rgba(8,38,29,0.78)'] as const,
};

export const ACCENT = colors.coral;

export const radii = { sm: 12, md: 20, lg: 28, pill: 999 };

export const space = { xs: 4, sm: 8, md: 12, lg: 16, xl: 20, xxl: 24 };

const shadowColor = scheme === 'dark' ? '#000000' : '#0a2f24';

export const shadows = {
  card: {
    shadowColor,
    shadowOpacity: scheme === 'dark' ? 0.3 : 0.08,
    shadowRadius: 18,
    shadowOffset: { width: 0, height: 8 },
    elevation: 3,
  },
  /** Yüzen öğeler: arama kutusu, sekme çubuğu. */
  float: {
    shadowColor,
    shadowOpacity: scheme === 'dark' ? 0.4 : 0.14,
    shadowRadius: 22,
    shadowOffset: { width: 0, height: 10 },
    elevation: 8,
  },
  sheet: {
    shadowColor,
    shadowOpacity: scheme === 'dark' ? 0.45 : 0.16,
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

export const INTENT_PASTELS: Record<string, string> = scheme === 'dark' ? darkPastels : lightPastels;

export const INTENT_ICONS: Record<string, 'cafe' | 'restaurant' | 'musical-notes' | 'color-palette' | 'leaf' | 'moon'> = {
  Kahve: 'cafe',
  Yemek: 'restaurant',
  Eglence: 'musical-notes',
  Kultur: 'color-palette',
  AcikHava: 'leaf',
  Gece: 'moon',
};
