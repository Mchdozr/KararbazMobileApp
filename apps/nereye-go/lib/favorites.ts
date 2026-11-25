import { api } from '@/lib/api';
import { track } from '@/lib/analytics';
import { friendlyError } from '@/lib/empty';
import { haptic } from '@/lib/haptics';
import { toast } from '@/lib/toast';
import type { VenueCard } from '@/lib/types';
import { useEffect, useState } from 'react';

/**
 * Kaydedilen mekanlar için tek kaynak. Kalp her ekranda aynı durumu gösterir;
 * dokunuş anında yansır, sunucu reddederse geri alınır.
 */
let cards: VenueCard[] = [];
let ids = new Set<string>();
let loadedFor: string | null = null;
let loading = false;
const listeners = new Set<() => void>();

function emit() {
  listeners.forEach((l) => l());
}

function set(next: VenueCard[]) {
  cards = next;
  ids = new Set(next.map((c) => c.placeId));
  emit();
}

export async function loadFavorites(userId: string | null, force = false) {
  if (!userId) {
    loadedFor = null;
    set([]);
    return;
  }
  if (loading || (!force && loadedFor === userId)) {
    return;
  }
  loading = true;
  emit();
  try {
    set(await api.favorites());
    loadedFor = userId;
  } finally {
    loading = false;
    emit();
  }
}

export async function toggleFavorite(card: Pick<VenueCard, 'placeId' | 'name'> & Partial<VenueCard>) {
  const was = ids.has(card.placeId);
  const before = cards;
  haptic.press();
  set(
    was
      ? cards.filter((c) => c.placeId !== card.placeId)
      : [
          {
            distanceMeters: 0,
            kind: 'Organic',
            nereyeRatingCount: 0,
            vibes: [],
            upcomingEvents: [],
            latitude: 0,
            longitude: 0,
            ...card,
          } as VenueCard,
          ...cards,
        ],
  );
  try {
    if (was) {
      await api.unfavorite(card.placeId);
      toast.info('Kaydedilenlerden çıkarıldı', { label: 'Geri al', onPress: () => void toggleFavorite(card) });
    } else {
      await api.favorite(card.placeId);
      track('save', { placeId: card.placeId });
      toast.success('Kaydedildi');
    }
  } catch (e) {
    set(before);
    toast.error(friendlyError(e));
  }
  return !was;
}

export function useFavorites() {
  const [, setTick] = useState(0);
  useEffect(() => {
    const l = () => setTick((n) => n + 1);
    listeners.add(l);
    return () => {
      listeners.delete(l);
    };
  }, []);
  return { cards, loading, has: (placeId: string) => ids.has(placeId) };
}
