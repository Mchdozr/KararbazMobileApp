// "Benim için seç": öneri listesinden tek mekan ve kısa gerekçeleri. Sunucunun sıralaması temel alınır,
// şu anki duruma göre (açık mı, kapanmak üzere mi, yürünür mü) yeniden tartılır. Reklamlı kart seçilmez.
// React Native içe aktarmaz: node ile test edilir.
import { decimal, t } from './i18n.ts';
import { openLine } from './open-status.ts';
import type { VenueCard } from './types.ts';
import { ratingLine } from './venue-format.ts';

export type ReasonIcon = 'walk-outline' | 'car-outline' | 'time-outline' | 'star' | 'pricetag-outline';
export type Reason = { icon: ReasonIcon; text: string };
export type Decision = { card: VenueCard; reasons: Reason[]; position: number; total: number };

/** Yürüme hızı (dakikada metre), rotadaki ortalama yürüyüşle uyumlu (~4,8 km/sa). */
export const WALK_METERS_PER_MINUTE = 80;
const WALKABLE_METERS = 2500;

function ratingValue(card: VenueCard): number | null {
  if (card.nereyeRating != null && card.nereyeRatingCount > 0) {
    return card.nereyeRating;
  }
  return card.googleRating ?? null;
}

/** Sunucu sırası + şu anki durum. Yüksek olan önce. */
export function decisionScore(card: VenueCard, index: number, total: number): number {
  // Sunucu sırası temel, ama anlık durum (açık/kapanıyor) onu geçebilsin diye ağırlığı sınırlı.
  let score = 0.6 * (total > 1 ? 1 - index / total : 1);
  if (card.openNow === true) {
    score += 0.35;
    if (card.closesInMinutes != null && card.closesInMinutes <= 30) {
      score -= 0.45; // varınca kapanmış olur
    }
  } else if (card.openNow === false) {
    score -= card.opensInMinutes != null && card.opensInMinutes <= 30 ? 0.15 : 0.7;
  } else {
    score += 0.1;
  }
  if (card.distanceMeters >= 0 && card.distanceMeters <= 15 * WALK_METERS_PER_MINUTE) {
    score += 0.1;
  }
  const rating = ratingValue(card);
  if (rating != null && rating >= 4.5) {
    score += 0.1;
  }
  return score;
}

/** Karar sırası: reklamlar dışarıda, puan en yüksekten düşüğe; eşitlikte sunucu sırası korunur. */
export function rankForDecision(cards: readonly VenueCard[]): VenueCard[] {
  const organic = cards.filter((c) => c.kind !== 'Sponsored');
  return organic
    .map((card, i) => ({ card, i, s: decisionScore(card, i, organic.length) }))
    .sort((a, b) => b.s - a.s || a.i - b.i)
    .map((x) => x.card);
}

export function distanceReason(meters: number): Reason | null {
  if (!(meters >= 0)) {
    return null;
  }
  if (meters <= WALKABLE_METERS) {
    return { icon: 'walk-outline', text: t('{n} dk yürüme', { n: Math.max(1, Math.round(meters / WALK_METERS_PER_MINUTE)) }) };
  }
  return { icon: 'car-outline', text: t('{km} km uzakta', { km: decimal(meters / 1000) }) };
}

export function reasonsFor(card: VenueCard, nowMinutes: number): Reason[] {
  const out: Reason[] = [];
  const dist = distanceReason(card.distanceMeters);
  if (dist) {
    out.push(dist);
  }
  const open = openLine(card, nowMinutes);
  if (open) {
    out.push({ icon: 'time-outline', text: open.text });
  }
  const rated = ratingLine(card);
  if (rated) {
    out.push({
      icon: 'star',
      text: rated.count ? t('{value} puan · {count} yorum', { value: rated.value, count: rated.count }) : t('{value} puan', { value: rated.value }),
    });
  }
  if (out.length < 3 && card.category) {
    out.push({ icon: 'pricetag-outline', text: t(card.category) });
  }
  return out.slice(0, 3);
}

/**
 * Sıradaki seçim. seen: bu oturumda gösterilenler; hepsi görüldüyse baştan başlar.
 * Seçilecek mekan yoksa null.
 */
export function pickForMe(cards: readonly VenueCard[], seen: readonly string[], nowMinutes: number): Decision | null {
  const ranked = rankForDecision(cards);
  if (ranked.length === 0) {
    return null;
  }
  const seenSet = new Set(seen);
  let index = ranked.findIndex((c) => !seenSet.has(c.placeId));
  if (index < 0) {
    index = 0;
  }
  const card = ranked[index];
  return { card, reasons: reasonsFor(card, nowMinutes), position: index + 1, total: ranked.length };
}
