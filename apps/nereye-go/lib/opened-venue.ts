import type { VenueCard } from '@/lib/types';

let opened: VenueCard | null = null;

export function rememberOpenedVenue(card: VenueCard) {
  opened = card;
}

export function openedVenue(placeId: string) {
  return opened?.placeId === placeId ? opened : null;
}
