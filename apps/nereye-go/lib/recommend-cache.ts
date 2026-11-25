import type { VenueCard } from '@/lib/types';

export type RecommendPage = { items: VenueCard[]; hasMore: boolean; weather?: import('./weather').WeatherInfo | null };

const mem = new Map<string, RecommendPage>();

export function recommendCacheKey(input: {
  lat: number;
  lng: number;
  intent: string;
  radiusMeters: number;
  openNow: boolean;
  vibes: string[];
  skip: number;
  take: number;
}) {
  const lat = input.lat.toFixed(3);
  const lng = input.lng.toFixed(3);
  const radius = Math.round(input.radiusMeters / 500) * 500;
  const vibes = [...input.vibes].sort().join(',');
  return `${input.intent}:${lat}:${lng}:${radius}:${input.openNow}:${vibes}:${input.skip}:${input.take}`;
}

export function peekRecommend(key: string) {
  return mem.get(key);
}

export function putRecommend(key: string, value: RecommendPage) {
  mem.set(key, value);
}
