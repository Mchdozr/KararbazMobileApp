import type { VenueCard } from '@/lib/types';

export type SortKey = 'smart' | 'distance' | 'rating';

export const SORTS: { key: SortKey; label: string }[] = [
  { key: 'smart', label: 'Önerilen' },
  { key: 'distance', label: 'En yakın' },
  { key: 'rating', label: 'En beğenilen' },
];

/** 'smart' sunucunun sırasını korur; diğerleri sponsorlu kartları yerinde bırakmadan yeniden dizer. */
export function sortCards<T extends Pick<VenueCard, 'distanceMeters' | 'nereyeRating' | 'nereyeRatingCount'>>(
  cards: T[],
  key: SortKey,
): T[] {
  if (key === 'smart') {
    return cards;
  }
  const copy = [...cards];
  if (key === 'distance') {
    return copy.sort((a, b) => a.distanceMeters - b.distanceMeters);
  }
  return copy.sort(
    (a, b) =>
      (b.nereyeRating ?? -1) - (a.nereyeRating ?? -1)
      || b.nereyeRatingCount - a.nereyeRatingCount
      || a.distanceMeters - b.distanceMeters,
  );
}

export function activeFilterCount(f: { openNow: boolean; vibes: string[]; minRating: number }) {
  return (f.openNow ? 1 : 0) + f.vibes.length + (f.minRating > 0 ? 1 : 0);
}
