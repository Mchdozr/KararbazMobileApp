import { api } from '@/lib/api';
import type { SavedPlace } from '@/lib/types';
import * as SecureStore from 'expo-secure-store';
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
    await SecureStore.setItemAsync(KEY, JSON.stringify(items));
  } catch {
    /* ignore */
  }
}

function localId() {
  return `local:${Date.now().toString(36)}`;
}

export async function hydrateSavedPlaces(authed: boolean) {
  if (!hydrated) {
    hydrated = true;
    try {
      const raw = await SecureStore.getItemAsync(KEY);
      if (raw) {
        items = JSON.parse(raw) as SavedPlace[];
      }
    } catch {
      items = [];
    }
  }
  if (authed) {
    const remote = await api.savedPlaces();
    if (remote.length) {
      items = remote;
      await persist();
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
