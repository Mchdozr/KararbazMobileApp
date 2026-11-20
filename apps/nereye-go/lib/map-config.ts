// Haritanın hangi sağlayıcıyla çizileceği. React Native içe aktarmaz: node ile test edilir.

/**
 * - google: Google haritası (Android; iOS'ta derlemeye iOS anahtarı verildiyse).
 * - apple: iOS'ta Apple Haritalar. Anahtar gerektirmez, Expo Go'da da çalışır: iPhone'da harita her zaman görünür.
 * - no-key: Android yayın derlemesinde anahtar yok. app.config.ts production derlemesini bu durumda durdurur;
 *   yalnızca anahtarsız önizleme/geliştirme derlemesinde görülebilir.
 * - web: web önizlemesi (Map.web.tsx).
 */
export type MapMode = 'google' | 'apple' | 'no-key' | 'web';

export function mapMode(
  platform: string,
  googleMapsEnabled: boolean,
  inExpoGo: boolean,
  googleMapsIosEnabled = false,
): MapMode {
  if (platform === 'ios') {
    return googleMapsIosEnabled ? 'google' : 'apple';
  }
  if (platform === 'android') {
    return googleMapsEnabled || inExpoGo ? 'google' : 'no-key';
  }
  return 'web';
}

export function mapAttribution(mode: MapMode) {
  const map = mode === 'apple' ? 'Harita © Apple' : 'Harita © Google';
  return `${map} · Mekan verisi © OpenStreetMap katkıcıları`;
}

/** Varsayılan atıf (Google haritası). */
export const MAP_ATTRIBUTION = mapAttribution('google');
