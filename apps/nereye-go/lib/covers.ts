import { ALL, getCover, restoreCovers, setCover, snapshotCovers, subscribeCovers } from '@/lib/cover-store';
import { getLargeJson, setLargeJson } from '@/lib/storage';
import { useEffect, useState } from 'react';

const FILE = 'covers-v1';
let restored: Promise<void> | null = null;
let saveTimer: ReturnType<typeof setTimeout> | null = null;

/** Uygulama açılışında bir kez: önceki oturumda görülen kapaklar ilk kartlarda hemen görünür. */
export function hydrateCovers(): Promise<void> {
  restored ??= getLargeJson<[string, string][]>(FILE).then((rows) => restoreCovers(rows));
  return restored;
}

function scheduleSave() {
  if (saveTimer) {
    return;
  }
  // Toplu güncellemeler tek yazıma insin.
  saveTimer = setTimeout(() => {
    saveTimer = null;
    void setLargeJson(FILE, snapshotCovers());
  }, 1500);
}

export function rememberCover(placeId: string, url: string | null | undefined) {
  if (setCover(placeId, url)) {
    scheduleSave();
  }
}

/** Kartın kapağı: depoda değişince yeniden çizilir. */
export function useCover(placeId: string): string | null {
  const [url, setUrl] = useState(() => getCover(placeId));
  useEffect(() => {
    setUrl(getCover(placeId));
    return subscribeCovers((id) => {
      if (id === placeId || id === ALL) {
        setUrl(getCover(placeId));
      }
    });
  }, [placeId]);
  return url;
}
