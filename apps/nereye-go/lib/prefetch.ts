import { rememberCover } from '@/lib/covers';
import { photoSource } from '@/lib/photos';
import type { VenueCard } from '@/lib/types';
import { Image } from 'expo-image';

const done = new Set<string>();

/**
 * Liste gelir gelmez ilk kartların görsellerini indirmeye başla: kullanıcı kaydırmadan önce disk önbelleğinde
 * olsunlar. Kartta gelen gerçek kare kapak deposuna da yazılır (kaydedilenler, arama, harita aynı kareyi kullanır).
 */
export function warmCards(cards: readonly VenueCard[], count = 8) {
  const uris: string[] = [];
  for (const card of cards) {
    const raw = card.photoUrl || card.photoReference;
    if (raw) {
      rememberCover(card.placeId, raw);
    }
    if (uris.length >= count) {
      continue;
    }
    const src = photoSource(card.placeId, raw);
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
