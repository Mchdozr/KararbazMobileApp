import { KADIKOY } from '@/lib/constants';
import { clampRadius } from '@/lib/radius';
import type { Preferences } from '@/lib/types';
import * as SecureStore from 'expo-secure-store';
import { useEffect, useState } from 'react';

type State = {
  lat: number;
  lng: number;
  label: string;
  pinned: boolean;
  intent: string;
  openNow: boolean;
  vibes: string[];
  radiusMeters: number;
  minRating: number;
};

const KEY = 'nereye.loc';
const PREF_KEY = 'nereye.filters';

let state: State = {
  lat: KADIKOY.lat,
  lng: KADIKOY.lng,
  label: 'Kadıköy',
  pinned: false,
  intent: 'Kahve',
  openNow: false,
  vibes: [],
  radiusMeters: 1500,
  minRating: 0,
};

let hydrated = false;
const listeners = new Set<() => void>();
function emit() {
  listeners.forEach((l) => l());
}

async function persistLoc() {
  try {
    await SecureStore.setItemAsync(
      KEY,
      JSON.stringify({ lat: state.lat, lng: state.lng, label: state.label, pinned: state.pinned }),
    );
  } catch {
    /* ignore */
  }
}

async function persistPrefs() {
  try {
    await SecureStore.setItemAsync(
      PREF_KEY,
      JSON.stringify({
        intent: state.intent,
        vibes: state.vibes,
        radiusMeters: state.radiusMeters,
        openNow: state.openNow,
        minRating: state.minRating,
      }),
    );
  } catch {
    /* ignore */
  }
}

export function isLocPinned() {
  return state.pinned;
}

export function applyPreferences(p: Preferences) {
  state = {
    ...state,
    radiusMeters: clampRadius(p.defaultRadiusMeters || state.radiusMeters),
    vibes: p.vibes?.length ? p.vibes : state.vibes,
    intent: p.intents[0] || state.intent,
  };
  emit();
  void persistPrefs();
}

export function useFilters() {
  const [, setTick] = useState(0);
  useEffect(() => {
    const listener = () => setTick((n) => n + 1);
    listeners.add(listener);
    if (!hydrated) {
      hydrated = true;
      void (async () => {
        try {
          const raw = await SecureStore.getItemAsync(KEY);
          if (raw) {
            const saved = JSON.parse(raw) as { lat: number; lng: number; label?: string; pinned?: boolean };
            if (typeof saved.lat === 'number' && typeof saved.lng === 'number') {
              state = {
                ...state,
                lat: saved.lat,
                lng: saved.lng,
                label: saved.label || 'Konumun',
                pinned: Boolean(saved.pinned),
              };
            }
          }
          const prefRaw = await SecureStore.getItemAsync(PREF_KEY);
          if (prefRaw) {
            const saved = JSON.parse(prefRaw) as Partial<State>;
            state = {
              ...state,
              intent: saved.intent || state.intent,
              vibes: Array.isArray(saved.vibes) ? saved.vibes : state.vibes,
              radiusMeters: typeof saved.radiusMeters === 'number' ? clampRadius(saved.radiusMeters) : state.radiusMeters,
              openNow: Boolean(saved.openNow),
              minRating: typeof saved.minRating === 'number' ? saved.minRating : 0,
            };
          }
          emit();
        } catch {
          /* ignore */
        }
      })();
    }
    return () => {
      listeners.delete(listener);
    };
  }, []);

  return {
    ...state,
    setLoc: (lat: number, lng: number, label?: string, pinned = true) => {
      state = { ...state, lat, lng, label: label || state.label, pinned };
      emit();
      void persistLoc();
    },
    setIntent: (intent: string) => {
      state = { ...state, intent };
      emit();
      void persistPrefs();
    },
    setOpenNow: (openNow: boolean) => {
      state = { ...state, openNow };
      emit();
      void persistPrefs();
    },
    toggleVibe: (vibe: string) => {
      const vibes = state.vibes.includes(vibe)
        ? state.vibes.filter((x) => x !== vibe)
        : [...state.vibes, vibe];
      state = { ...state, vibes };
      emit();
      void persistPrefs();
    },
    setVibes: (vibes: string[]) => {
      state = { ...state, vibes };
      emit();
      void persistPrefs();
    },
    setRadius: (radiusMeters: number) => {
      state = { ...state, radiusMeters: clampRadius(radiusMeters) };
      emit();
      void persistPrefs();
    },
    setMinRating: (minRating: number) => {
      state = { ...state, minRating };
      emit();
      void persistPrefs();
    },
  };
}
