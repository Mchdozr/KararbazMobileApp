import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import type { VenueCard } from './types.ts';
import { widgetSnapshot, WIDGET_FILE } from './widget-data.ts';

function card(id: string, extra: Partial<VenueCard> = {}): VenueCard {
  return { placeId: id, name: id.toUpperCase(), distanceMeters: 400, kind: 'Organic', nereyeRatingCount: 0, vibes: [], upcomingEvents: [], latitude: 40.99, longitude: 29.02, ...extra };
}

test('ana ekran bileşeni: reklamsız, açık olan ilk 3 mekan; kısa durum ve mesafe', () => {
  const snap = widgetSnapshot(
    [
      card('reklam', { kind: 'Sponsored', openNow: true }),
      card('kapali', { openNow: false }),
      card('a', { openNow: true, closesAt: '23:00', closesInMinutes: 200, distanceMeters: 640 }),
      card('b', { openNow: true }),
      card('c', { openNow: null }),
      card('d', { openNow: true }),
    ],
    { label: 'Kadıköy', intent: 'Kahve', now: Date.parse('2026-11-24T17:00:00Z'), nowMinutes: 1200 },
  );
  assert.equal(WIDGET_FILE, 'widget-nearby');
  assert.equal(snap.title, 'Kahve · Kadıköy');
  assert.deepEqual(snap.items.map((i) => i.placeId), ['a', 'b', 'd']);
  assert.equal(snap.items[0].meta, "23:00'e kadar açık · 8 dk yürüme");
  assert.equal(snap.updatedAt, Date.parse('2026-11-24T17:00:00Z'));
  assert.equal(snap.updatedLabel, 'Güncelleme 20:00');
});

test('açık mekan yoksa liste boş döner (bileşen "uygulamayı aç" der)', () => {
  const snap = widgetSnapshot([card('x', { openNow: false })], { label: 'Moda', intent: 'Gece', now: 0, nowMinutes: 600 });
  assert.deepEqual(snap.items, []);
});
