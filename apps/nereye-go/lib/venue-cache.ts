import type { VenueDetail } from '@/lib/types';

/**
 * Mekan detayı için bellek içi önbellek. Kart dokunulduğu anda istek başlar (prefetch);
 * sayfa açıldığında veri çoğu zaman hazırdır. Geri dönüp aynı mekana girince anında açılır,
 * arka planda tazelenir.
 */
const TTL_MS = 10 * 60_000;
const MAX = 40;
const store = new Map<string, { at: number; value: VenueDetail }>();
const inflight = new Map<string, Promise<VenueDetail>>();

export function peekVenue(placeId: string): VenueDetail | null {
  return store.get(placeId)?.value ?? null;
}

export function isFresh(placeId: string, now = Date.now()) {
  const hit = store.get(placeId);
  return Boolean(hit && now - hit.at < TTL_MS);
}

export function putVenue(value: VenueDetail, now = Date.now()) {
  store.delete(value.placeId);
  store.set(value.placeId, { at: now, value });
  // En eski girdiyi at: Map ekleme sırasını korur.
  while (store.size > MAX) {
    store.delete(store.keys().next().value as string);
  }
}

/** Aynı mekan için eşzamanlı istekleri tek isteğe indirger. */
export function shareVenueRequest(placeId: string, load: () => Promise<VenueDetail>): Promise<VenueDetail> {
  const pending = inflight.get(placeId);
  if (pending) {
    return pending;
  }
  const started = load()
    .then((value) => {
      putVenue(value);
      return value;
    })
    .finally(() => inflight.delete(placeId));
  inflight.set(placeId, started);
  return started;
}

export function clearVenueCache() {
  store.clear();
  inflight.clear();
}
