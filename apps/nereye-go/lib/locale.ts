import { resolveLanguage, setLanguage, type Lang, type LangPref } from '@/lib/i18n';
import { getItemSync, setItem } from '@/lib/storage';
import { getLocales } from 'expo-localization';
import { Platform } from 'react-native';

/**
 * Dil açılışta seçilir (Profil tercihi > cihaz dili). Bu modül uygulamanın ilk içe aktarılan modülüdür
 * (app/_layout.tsx): metinler ekrana çizilirken t() doğru dili görür.
 */
const KEY = 'lang';
const stored = getItemSync(KEY);
export const languagePref: LangPref = stored === 'tr' || stored === 'en' ? stored : 'system';

function deviceLanguage(): string | null {
  try {
    return getLocales()[0]?.languageCode ?? null;
  } catch {
    return null;
  }
}

export const appLanguage: Lang = resolveLanguage(languagePref, deviceLanguage());
setLanguage(appLanguage);

if (Platform.OS === 'web' && typeof document !== 'undefined') {
  document.documentElement.lang = appLanguage;
}

/** Tercihi kaydeder; web'de sayfa yenilenir, yerelde bir sonraki açılışta uygulanır (true döner). */
export async function setLanguagePref(pref: LangPref): Promise<boolean> {
  await setItem(KEY, pref);
  if (Platform.OS === 'web' && typeof window !== 'undefined') {
    window.location.reload();
    return false;
  }
  return resolveLanguage(pref, deviceLanguage()) !== appLanguage;
}
