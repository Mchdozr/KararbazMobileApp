// İstek iptali ve öneri adresi. React Native içe aktarmaz: node ile test edilir.

/** Dış sinyal (ekranın "bu istek artık gereksiz" kararı) isteğin kendi denetleyicisini de durdurur. */
export function linkAbort(outer: AbortSignal | undefined, inner: AbortController): () => void {
  if (!outer) {
    return () => {};
  }
  if (outer.aborted) {
    inner.abort();
    return () => {};
  }
  const onAbort = () => inner.abort();
  outer.addEventListener('abort', onAbort, { once: true });
  return () => outer.removeEventListener('abort', onAbort);
}

export type RecommendParams = {
  lat: number;
  lng: number;
  intent: string;
  radiusMeters: number;
  openNow: boolean;
  vibes: string[];
  skip: number;
  take: number;
};

/** Konum 4 basamağa (~11 m) yuvarlanır: sunucu zaten hücre bazında önbellekler, ETag tekrar kullanılabilir. */
export function recommendPath(p: RecommendParams): string {
  const lat = Number(p.lat.toFixed(4));
  const lng = Number(p.lng.toFixed(4));
  const vibes = p.vibes.length ? `&vibes=${p.vibes.join(',')}` : '';
  return `/api/recommendations?lat=${lat}&lng=${lng}&intent=${p.intent}&radiusMeters=${p.radiusMeters}&openNow=${p.openNow}&skip=${p.skip}&take=${p.take}${vibes}`;
}
