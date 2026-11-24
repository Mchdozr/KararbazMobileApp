import { rememberCover } from '@/lib/covers';
import { photoSource } from '@/lib/photos';
import type { VenueCard } from '@/lib/types';
import { Image } from 'expo-image';
import { PixelRatio } from 'react-native';
import { CARD_IMAGE_PT, variantWidth } from '@/lib/media-width';

const done = new Set<string>();

/**
 * Liste gelir gelmez ilk kartların görsellerini indirmeye başla: kullanıcı kaydırmadan önce disk önbelleğinde
 * olsunlar. Kartta gelen gerçek kare kapak deposuna da yazılır (kaydedilenler, arama, harita aynı kareyi kullanır).
 */
export function warmCards(cards: readonly VenueCard[], count = 8) {
  const uris: string[] = [];
  const cardWidthPx = variantWidth(CARD_IMAGE_PT, PixelRatio.get());
  for (const card of cards) {
    const raw = card.photoUrl || card.photoReference;
    if (raw) {
      rememberCover(card.placeId, raw);
    }
    if (uris.length >= count) {
      continue;
    }
    // Kartın isteyeceği sürümün aynısı (aynı URI): önden indirilen dosya kartta kullanılır.
    const src = photoSource(card.placeId, raw, null, cardWidthPx);
    const uri = src && typeof src === 'object' ? src.uri : null;
    if (uri && !done.has(uri)) {
      done.add(uri);
      uris.push(uri);
    }
  }
  if (uris.length > 0) {
    void Image.prefetch(uris, 'memory-disk').catch(() => false);
  }
}
