// Haritanın hangi koşulda gösterileceği. React Native içe aktarmaz: node ile test edilir.

export type MapMode = 'google' | 'no-key' | 'unsupported';

/**
 * Google Places içeriği (puan, yorum, fotoğraf) yalnızca Google haritası üzerinde gösterilir.
 * - Android: derlemeye Google Maps anahtarı verildiyse ya da Expo Go içindeysek Google haritası.
 *   Anahtar yoksa harita hiç çizilmez; public OSM döşemesine düşülmez.
 * - iOS ve web: v1 kapsamı dışında; Google dışı (Apple) haritaya Google içeriği koymamak için gösterilmez.
 */
export function mapMode(platform: string, googleMapsEnabled: boolean, inExpoGo: boolean): MapMode {
  if (platform !== 'android') {
    return 'unsupported';
  }
  return googleMapsEnabled || inExpoGo ? 'google' : 'no-key';
}

export const MAP_ATTRIBUTION = 'Harita © Google · Mekan verisi © OpenStreetMap katkıcıları';
