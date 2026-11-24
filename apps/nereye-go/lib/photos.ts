import { getApiBase } from '@/lib/api';
import { withVariant } from '@/lib/media-width';

const local: Record<string, number> = {
  'nereye:petra': require('../assets/venues/petra.jpg'),
  'nereye:fazil': require('../assets/venues/fazil.jpg'),
  'nereye:walter': require('../assets/venues/walter.jpg'),
  'nereye:montag': require('../assets/venues/montag.jpg'),
  'nereye:federica': require('../assets/venues/federica.jpg'),
  'nereye:goygoy': require('../assets/venues/goygoy.jpg'),
  'nereye:opera': require('../assets/venues/opera.jpg'),
  'nereye:mandabatmaz': require('../assets/venues/mandabatmaz.jpg'),
  'nereye:kronotrop': require('../assets/venues/kronotrop.jpg'),
  'nereye:ciya': require('../assets/venues/ciya.jpg'),
  'nereye:karakoy': require('../assets/venues/karakoy.jpg'),
  'nereye:sureyya': require('../assets/venues/sureyya.jpg'),
  'nereye:zorlu': require('../assets/venues/zorlu.jpg'),
  'nereye:modern': require('../assets/venues/modern.jpg'),
  'nereye:mini': require('../assets/venues/mini.jpg'),
  'nereye:moda': require('../assets/venues/moda.jpg'),
};

export const INTENT_PHOTOS: Record<string, number> = {
  Kahve: local['nereye:petra'],
  Yemek: local['nereye:ciya'],
  Eglence: local['nereye:mini'],
  Kultur: local['nereye:sureyya'],
  AcikHava: local['nereye:moda'],
  Gece: local['nereye:opera'],
};

/**
 * Görselin kaynağı. widthPx verilirse yerel mekan/yükleme görselinin o genişlikteki küçültülmüş sürümü istenir
 * (lib/media-width.ts); Google kareleri zaten 800 px gelir.
 */
export function photoSource(
  placeId: string,
  photoUrl?: string | null,
  photoReference?: string | null,
  widthPx?: number | null,
) {
  if (local[placeId]) {
    return local[placeId];
  }
  const raw = photoUrl || photoReference;
  if (raw?.startsWith('/api/media/google')) {
    return { uri: `${getApiBase()}${raw}` };
  }
  if (raw?.startsWith('http')) {
    return { uri: withVariant(raw, widthPx) };
  }
  if (raw?.startsWith('places/')) {
    return { uri: `${getApiBase()}/api/media/google?name=${encodeURIComponent(raw)}` };
  }
  if (raw?.startsWith('/')) {
    return { uri: withVariant(`${getApiBase()}${raw}`, widthPx) };
  }
  return null;
}
