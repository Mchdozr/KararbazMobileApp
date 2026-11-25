// Kullanım analitiği kuralları. Sunucudaki Nereye.Domain.Analytics.AnalyticsRules aynasıdır; birini
// değiştirirsen diğerini de değiştir. Olaylar kimlik, konum ya da serbest metin taşımaz.
// React Native içe aktarmaz: node ile test edilir.

export type AnalyticsName =
  | 'app_open'
  | 'list_view'
  | 'venue_view'
  | 'directions'
  | 'save'
  | 'share'
  | 'decide_open'
  | 'search';

export type AnalyticsEvent = { name: AnalyticsName; placeId?: string; intent?: string };
export type Consent = 'granted' | 'denied';

const VENUE_EVENTS = new Set(['venue_view', 'directions', 'save', 'share']);
const PLAIN_EVENTS = new Set(['app_open', 'decide_open', 'search']);
const INTENTS = new Set(['Kahve', 'Yemek', 'Eglence', 'Kultur', 'AcikHava', 'Gece']);
const PLACE_ID = /^[A-Za-z0-9:_\-./]{1,128}$/;
export const MAX_BATCH = 50;
const MAX_PENDING = 200;

export function toEvent(name: AnalyticsName, props: { placeId?: string; intent?: string } = {}): AnalyticsEvent | null {
  if (VENUE_EVENTS.has(name)) {
    return props.placeId && PLACE_ID.test(props.placeId) ? { name, placeId: props.placeId } : null;
  }
  if (name === 'list_view') {
    return props.intent && INTENTS.has(props.intent) ? { name, intent: props.intent } : null;
  }
  return PLAIN_EVENTS.has(name) ? { name } : null;
}

export class AnalyticsQueue {
  private items: AnalyticsEvent[] = [];

  get pending() {
    return this.items.length;
  }

  add(event: AnalyticsEvent) {
    if (this.items.length < MAX_PENDING) {
      this.items.push(event);
    }
  }

  take(): AnalyticsEvent[] {
    return this.items.splice(0, MAX_BATCH);
  }

  clear() {
    this.items = [];
  }
}

export function parseConsent(raw: string | null | undefined): Consent | null {
  return raw === 'granted' || raw === 'denied' ? raw : null;
}

/** Karar verilmemişse ve kullanıcı ilk listesini gördüyse bir kez sorulur (açılışta değil). */
export function shouldAskConsent(consent: Consent | null, sawList: boolean): boolean {
  return consent === null && sawList;
}
