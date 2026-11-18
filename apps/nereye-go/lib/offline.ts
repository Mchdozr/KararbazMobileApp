import type { VenueCard } from '@/lib/types';

/** Ağ yokken gösterilecek son başarılı öneri listesi. Niyet başına ayrı tutulur (Kültür listesi Yemek'te çıkmasın). */
export type SavedList = { savedAt: string; intent: string; label: string; items: VenueCard[] };

export const OFFLINE_KEEP = 20;

export function savedListName(intent: string) {
  return `last-recommend-${intent.toLowerCase().replace(/[^a-z0-9]/g, '')}`;
}

export function toSaved(intent: string, label: string, items: VenueCard[], now = new Date()): SavedList {
  return { savedAt: now.toISOString(), intent, label, items: items.slice(0, OFFLINE_KEEP) };
}

/** Bağlantı hatası mı (sunucu cevap verdiyse değil)? ApiError'a yapısal bakar; saf kalsın diye instanceof yok. */
export function isOffline(reason: unknown) {
  const e = reason as { name?: string; kind?: string } | null;
  return Boolean(e && e.name === 'ApiError' && (e.kind === 'network' || e.kind === 'timeout'));
}

/** Kayıt bu niyete ait ve boş değilse kullanılabilir. Uydurma veri yok: kayıt yoksa null. */
export function usableSaved(saved: SavedList | null | undefined, intent: string): SavedList | null {
  return saved && saved.intent === intent && Array.isArray(saved.items) && saved.items.length > 0 ? saved : null;
}

export function offlineBanner(saved: Pick<SavedList, 'savedAt' | 'label'>, now = new Date()) {
  const at = new Date(saved.savedAt);
  const sameDay = at.toDateString() === now.toDateString();
  const hh = String(at.getHours()).padStart(2, '0');
  const mm = String(at.getMinutes()).padStart(2, '0');
  const when = sameDay
    ? `bugün ${hh}:${mm}`
    : `${at.getDate()}.${String(at.getMonth() + 1).padStart(2, '0')} ${hh}:${mm}`;
  return `Çevrimdışısın. ${saved.label} için ${when} tarihli son liste gösteriliyor; bilgiler güncel olmayabilir.`;
}
