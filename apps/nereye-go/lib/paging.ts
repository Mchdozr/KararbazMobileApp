import type { VenueCard } from '@/lib/types';

/**
 * Öneri sayfası sonrası durum. Sonraki skip sunucunun döndürdüğü kayıt sayısıyla ilerler, ekranda kalan
 * (puan filtresinden geçen) kart sayısıyla değil: aksi hâlde aynı pencere tekrar istenir.
 */
export function advancePage(
  skip: number,
  res: { items: VenueCard[]; hasMore: boolean },
  minRating?: number | null,
) {
  const fetched = res.items.length;
  return {
    cards: res.items.filter((c) => !minRating || (c.nereyeRating ?? 0) >= minRating),
    nextSkip: skip + fetched,
    // Sunucu boş sayfa döndürdüyse "devamı var" dese de bitti say: sonsuz istek döngüsü olmasın.
    hasMore: res.hasMore && fetched > 0,
  };
}
