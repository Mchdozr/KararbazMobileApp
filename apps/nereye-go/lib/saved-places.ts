import { api } from '@/lib/api';
import type { SavedPlace } from '@/lib/types';
import { getItem, removeItem, setItem } from '@/lib/storage';
import { useEffect, useState } from 'react';

const KEY = 'nereye.savedPlaces';

let items: SavedPlace[] = [];
let hydrated = false;
const listeners = new Set<() => void>();

function emit() {
  listeners.forEach((l) => l());
}

async function persist() {
  try {
    await setItem(KEY, JSON.stringify(items));
  } catch {
    /* ignore */
  }
}

function localId() {
  return `local:${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

export async function hydrateSavedPlaces(authed: boolean) {
  if (!hydrated) {
    hydrated = true;
    try {
      const raw = await getItem(KEY);
      if (raw) {
        const parsed = JSON.parse(raw) as unknown;
        items = Array.isArray(parsed) ? (parsed as SavedPlace[]) : [];
      }
    } catch {
      items = [];
    }
  }
  if (authed) {
    try {
      const remote = await api.savedPlaces();
      if (remote.length) {
        // Misafirken kaydedilenler (local:) sunucudakilerle birlikte kalsın.
        const localOnly = items.filter(
          (p) => p.id.startsWith('local:') && !remote.some((r) => r.kind === p.kind && r.label === p.label),
        );
        items = [...remote, ...localOnly];
        await persist();
      }
    } catch {
      /* çevrimdışı: yerel listeyle devam */
    }
  }
  emit();
}

export function useSavedPlaces() {
  const [, setTick] = useState(0);
  useEffect(() => {
    const listener = () => setTick((n) => n + 1);
    listeners.add(listener);
    void hydrateSavedPlaces(false);
    return () => {
      listeners.delete(listener);
    };
  }, []);

  return {
    items,
    add: async (input: { label: string; kind: string; latitude: number; longitude: number }, authed: boolean) => {
      let row: SavedPlace | null = null;
      if (authed) {
        row = (await api.addSavedPlace(input)) ?? null;
      }
      if (!row) {
        row = { id: localId(), ...input };
      }
      items = [
        row,
        ...items.filter(
          (p) => p.id !== row!.id && !(p.kind === row!.kind && (row!.kind === 'home' || row!.kind === 'work')),
        ),
      ];
      await persist();
      emit();
    },
    remove: async (id: string, authed: boolean) => {
      items = items.filter((p) => p.id !== id);
      await persist();
      emit();
      if (authed && !id.startsWith('local:')) {
        await api.removeSavedPlace(id);
      }
    },
  };
}
