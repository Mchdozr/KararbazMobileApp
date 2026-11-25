import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { hexKey, pollOptionsFrom, pollShareText, resultRows, timeLeft, type Poll } from './poll.ts';
import type { VenueCard } from './types.ts';

function card(id: string, extra: Partial<VenueCard> = {}): VenueCard {
  return {
    placeId: id,
    name: id.toUpperCase(),
    distanceMeters: 300,
    kind: 'Organic',
    nereyeRatingCount: 0,
    vibes: [],
    upcomingEvents: [],
    latitude: 40.99,
    longitude: 29.02,
    openNow: true,
    ...extra,
  };
}

test('kısa liste: reklam dışı, karar sırasıyla en fazla 4 mekan', () => {
  const opts = pollOptionsFrom([card('reklam', { kind: 'Sponsored' }), card('a'), card('b', { openNow: false }), card('c'), card('d'), card('e')]);
  assert.equal(opts.length, 4);
  assert.ok(!opts.some((o) => o.placeId === 'reklam'));
  assert.equal(opts.at(-1)?.placeId === 'b' || !opts.some((o) => o.placeId === 'b'), true); // kapalı mekan sona düşer
  assert.deepEqual(Object.keys(opts[0]).sort(), ['category', 'latitude', 'longitude', 'name', 'photoUrl', 'placeId']);
});

const poll: Poll = {
  code: 'ab3k9mnp',
  title: 'Bu akşam kahve',
  expiresAt: '2026-11-26T18:00:00Z',
  expired: false,
  options: [
    { placeId: 'a', name: 'Petra', votes: 3, latitude: 40.9, longitude: 29 },
    { placeId: 'b', name: "Walter's", votes: 1, latitude: 40.9, longitude: 29 },
    { placeId: 'c', name: 'Kronotrop', votes: 0, latitude: 40.9, longitude: 29 },
  ],
  totalVotes: 4,
  myVote: 'b',
  winnerPlaceId: 'a',
  appUrl: 'nereyego://poll/ab3k9mnp',
  webUrl: 'https://ornek.app/k/ab3k9mnp',
};

test('sonuç satırları: yüzde, benim oyum, önde olan', () => {
  const rows = resultRows(poll);
  assert.deepEqual(rows.map((r) => r.percent), [75, 25, 0]);
  assert.equal(rows[1].mine, true);
  assert.equal(rows[0].leading, true);
  assert.equal(rows[2].leading, false);
  assert.deepEqual(resultRows({ ...poll, totalVotes: 0, winnerPlaceId: null, options: poll.options.map((o) => ({ ...o, votes: 0 })) }).map((r) => r.percent), [0, 0, 0]);
});

test('paylaşım metni: başlık, seçenekler, web ve uygulama bağlantısı', () => {
  const text = pollShareText(poll);
  assert.match(text, /Bu akşam kahve/);
  assert.match(text, /Petra · Walter's · Kronotrop/);
  assert.match(text, /https:\/\/ornek\.app\/k\/ab3k9mnp/);
  assert.match(text, /nereyego:\/\/poll\/ab3k9mnp/);
  assert.doesNotMatch(pollShareText({ ...poll, webUrl: null }), /https:/);
});

test('kalan süre Türkçe', () => {
  const now = Date.parse('2026-11-26T15:30:00Z');
  assert.equal(timeLeft('2026-11-26T18:00:00Z', now), '2 sa 30 dk kaldı');
  assert.equal(timeLeft('2026-11-26T15:50:00Z', now), '20 dk kaldı');
  assert.equal(timeLeft('2026-11-26T15:00:00Z', now), 'Sona erdi');
});

test('oy anahtarı 32 onaltılık karakter', () => {
  const key = hexKey(Uint8Array.from({ length: 16 }, (_, i) => i * 17));
  assert.equal(key.length, 32);
  assert.match(key, /^[0-9a-f]{32}$/);
});
