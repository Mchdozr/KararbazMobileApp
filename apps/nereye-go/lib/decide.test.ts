import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { distanceReason, pickForMe, rankForDecision, reasonsFor } from './decide.ts';
import type { VenueCard } from './types.ts';

function card(id: string, extra: Partial<VenueCard> = {}): VenueCard {
  return {
    placeId: id,
    name: id,
    distanceMeters: 300,
    kind: 'Organic',
    nereyeRatingCount: 0,
    vibes: [],
    upcomingEvents: [],
    latitude: 40.99,
    longitude: 29.02,
    ...extra,
  };
}

test('reklamlı kart seçilmez, açık mekan kapalının önüne geçer', () => {
  const cards = [
    card('reklam', { kind: 'Sponsored', openNow: true }),
    card('kapali', { openNow: false, opensAt: '09:00', opensInMinutes: 600 }),
    card('acik', { openNow: true, closesAt: '23:00', closesInMinutes: 300 }),
  ];
  const ranked = rankForDecision(cards).map((c) => c.placeId);
  assert.deepEqual(ranked, ['acik', 'kapali']);
});

test('kapanmak üzere olan mekan geri düşer', () => {
  const cards = [
    card('kapaniyor', { openNow: true, closesAt: '22:00', closesInMinutes: 15 }),
    card('rahat', { openNow: true, closesAt: '24:00', closesInMinutes: 135 }),
  ];
  assert.equal(rankForDecision(cards)[0].placeId, 'rahat');
});

test('Başka öner: görülenler atlanır, hepsi görülünce başa dönülür', () => {
  const cards = [card('a', { openNow: true }), card('b', { openNow: true }), card('c', { openNow: true })];
  const first = pickForMe(cards, [], 600)!;
  assert.equal(first.card.placeId, 'a');
  assert.equal(first.position, 1);
  assert.equal(first.total, 3);
  assert.equal(pickForMe(cards, ['a'], 600)!.card.placeId, 'b');
  assert.equal(pickForMe(cards, ['a', 'b', 'c'], 600)!.card.placeId, 'a');
  assert.equal(pickForMe([], [], 600), null);
});

test('gerekçeler: yürüme süresi, açık saati, puan', () => {
  const reasons = reasonsFor(
    card('x', { distanceMeters: 640, openNow: true, closesAt: '23:00', closesInMinutes: 200, googleRating: 4.6, googleRatingCount: 120 }),
    1200,
  ).map((r) => r.text);
  assert.deepEqual(reasons, ['8 dk yürüme', "23:00'e kadar açık", '4,6 puan · 120 yorum']);
});

test('uzak mekan km ile anlatılır, kategori boşluğu doldurur', () => {
  assert.equal(distanceReason(4200)?.text, '4,2 km uzakta');
  const reasons = reasonsFor(card('y', { distanceMeters: 4200, category: 'Kahve dükkanı' }), 600).map((r) => r.text);
  assert.deepEqual(reasons, ['4,2 km uzakta', 'Kahve dükkanı']);
});
