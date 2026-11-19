// Mekan kapak görseli deposu: bir kez görülen gerçek kare (detay galerisi ya da toplu görsel ucu) kartlarda
// anında kullanılır. React Native içe aktarmaz: node ile test edilir. Kalıcılık lib/covers.ts'tedir.

export const COVER_LIMIT = 600;

type Listener = (placeId: string) => void;

const map = new Map<string, string>();
const listeners = new Set<Listener>();

export function getCover(placeId: string): string | null {
  return map.get(placeId) ?? null;
}

/** Değiştiyse true. En son kullanılan sona taşınır; sınır aşılınca en eskiler düşer. */
export function setCover(placeId: string, url: string | null | undefined): boolean {
  const value = url?.trim();
  if (!placeId || !value) {
    return false;
  }
  const before = map.get(placeId);
  map.delete(placeId);
  map.set(placeId, value);
  while (map.size > COVER_LIMIT) {
    const oldest = map.keys().next().value;
    if (oldest === undefined) {
      break;
    }
    map.delete(oldest);
  }
  if (before === value) {
    return false;
  }
  for (const l of listeners) {
    l(placeId);
  }
  return true;
}

/** İndirilemeyen kare kapak olarak kalmasın. */
export function dropCover(placeId: string, url?: string) {
  if (map.has(placeId) && (!url || map.get(placeId) === url)) {
    map.delete(placeId);
    for (const l of listeners) {
      l(placeId);
    }
  }
}

export function subscribeCovers(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function snapshotCovers(): [string, string][] {
  return [...map.entries()];
}

/** Toplu değişiklik bildirimi: tüm dinleyiciler kendi kapağını yeniden okur. */
export const ALL = '*';

/** Kalıcı kayıttan geri yükleme: bellekte daha yeni kayıt varsa ezilmez. */
export function restoreCovers(entries: unknown) {
  if (!Array.isArray(entries)) {
    return;
  }
  let added = 0;
  for (const row of entries.slice(-COVER_LIMIT)) {
    if (Array.isArray(row) && typeof row[0] === 'string' && typeof row[1] === 'string' && !map.has(row[0])) {
      map.set(row[0], row[1]);
      added += 1;
    }
  }
  if (added > 0) {
    for (const l of listeners) {
      l(ALL);
    }
  }
}

export function clearCoversForTest() {
  map.clear();
}
