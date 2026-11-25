import { api } from '@/lib/api';
import { AnalyticsQueue, parseConsent, toEvent, type AnalyticsName, type Consent } from '@/lib/analytics-queue';
import { getItem, setItem } from '@/lib/storage';
import { useSyncExternalStore } from 'react';
import { AppState } from 'react-native';

/**
 * Onaylı kullanım analitiği. Kullanıcı açıkça izin vermedikçe hiçbir olay kuyruğa bile girmez.
 * Olaylar kimlik ve konum taşımaz (lib/analytics-queue.ts); sunucu günlük sayaç tutar.
 */
const KEY = 'analytics.consent';
const queue = new AnalyticsQueue();
const listeners = new Set<() => void>();
let consent: Consent | null = null;
let loaded = false;
let timer: ReturnType<typeof setTimeout> | null = null;

function emit() {
  listeners.forEach((l) => l());
}

export async function hydrateAnalytics() {
  consent = parseConsent(await getItem(KEY));
  loaded = true;
  emit();
  AppState.addEventListener('change', (s) => {
    if (s !== 'active') {
      void flush();
    }
  });
}

/** Rıza kararı: cihazda saklanır; oturum varsa sunucudaki rıza günlüğüne de yazılır. Reddedilirse kuyruk silinir. */
export async function setAnalyticsConsent(granted: boolean, signedIn: boolean) {
  consent = granted ? 'granted' : 'denied';
  await setItem(KEY, consent);
  if (!granted) {
    queue.clear();
  }
  if (signedIn) {
    void api.consent('analytics', granted);
  }
  emit();
}

export function track(name: AnalyticsName, props: { placeId?: string; intent?: string } = {}) {
  if (consent !== 'granted') {
    return;
  }
  const event = toEvent(name, props);
  if (!event) {
    return;
  }
  queue.add(event);
  if (!timer) {
    timer = setTimeout(() => {
      timer = null;
      void flush();
    }, 15_000);
  }
}

async function flush() {
  if (consent !== 'granted' || queue.pending === 0) {
    return;
  }
  // Gönderilemeyen paket bırakılır: analitik için yeniden deneme gerekmez.
  await api.sendEvents(queue.take());
}

/** 'loading': cihazdaki karar henüz okunmadı (izin kartı bu sırada görünmesin). */
export type ConsentView = Consent | 'unset' | 'loading';
const view = (): ConsentView => (!loaded ? 'loading' : (consent ?? 'unset'));
const subscribe = (l: () => void) => {
  listeners.add(l);
  return () => {
    listeners.delete(l);
  };
};

export function useAnalyticsConsent(): ConsentView {
  return useSyncExternalStore(subscribe, view, view);
}
