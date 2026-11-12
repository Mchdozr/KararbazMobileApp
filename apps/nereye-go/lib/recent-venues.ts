import { getJson, removeItem, setJson } from '@/lib/storage';
import type { VenueCard } from '@/lib/types';
import { useEffect, useState } from 'react';

const KEY = 'nereye.recentVenues';
const MAX = 10;

type Recent = Pick<VenueCard, 'placeId' | 'name' | 'address' | 'photoUrl' | 'nereyeRating' | 'latitude' | 'longitude'>;

let items: Recent[] = [];
let hydrated = false;
const listeners = new Set<() => void>();

function emit() {
  listeners.forEach((l) => l());
}

async function hydrate() {
  if (hydrated) {
    return;
  }
  hydrated = true;
  const saved = await getJson<Recent[]>(KEY);
  if (Array.isArray(saved)) {
    items = saved.filter((r) => r && typeof r.placeId === 'string').slice(0, MAX);
    emit();
  }
}

/** Mekan detayı açıldığında çağrılır; liste cihazda kalır, sunucuya gitmez. */
export function rememberVenue(venue: Recent) {
  items = [venue, ...items.filter((r) => r.placeId !== venue.placeId)].slice(0, MAX);
  emit();
  void setJson(KEY, items);
}

export function clearRecentVenues() {
  items = [];
  emit();
  void removeItem(KEY);
}

export function useRecentVenues() {
  const [, setTick] = useState(0);
  useEffect(() => {
    const l = () => setTick((n) => n + 1);
    listeners.add(l);
    void hydrate();
    return () => {
      listeners.delete(l);
    };
  }, []);
  return items;
}

export function recentAsCard(r: Recent): VenueCard {
  return {
    distanceMeters: -1,
    kind: 'Organic',
    nereyeRatingCount: 0,
    vibes: [],
    upcomingEvents: [],
    ...r,
  };
}
