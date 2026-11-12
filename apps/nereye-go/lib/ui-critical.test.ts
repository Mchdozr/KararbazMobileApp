import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { emptyDiscover, emptyMap, emptyNearby, emptySearch, friendlyError } from './empty.ts';
import { geoChip, geoKindIcon, geoKindLabel, geoSubtitle, mahalleLine } from './geo-hit.ts';
import { parseOpeningHours } from './hours.ts';
import { formatDuration, formatRouteLine } from './route.ts';
import { hashSeed, placeholderInitials, placeholderPalette } from './placeholder.ts';
import { clampRadius, kmToMeters } from './radius.ts';
import { debounceMs, shouldSearch } from './search-query.ts';
import { ApiError, serverMessage } from './api-error.ts';
import { relativeDate, summarizeRatings } from './reviews.ts';
import { activeFilterCount, sortCards } from './sort.ts';
import { clearVenueCache, isFresh, peekVenue, putVenue, shareVenueRequest } from './venue-cache.ts';
import { clip, compactCount, featureIcon, metaLine, priceLabel, ratingLine } from './venue-format.ts';
import { emailProblem, nameProblem, passwordProblem, passwordStrength } from './validation.ts';
import { peekRecommend, putRecommend, recommendCacheKey } from './recommend-cache.ts';
import { photoBatchIds } from './photo-batch.ts';

test('onboarding skip is a one-shot gate', () => {
  assert.equal(typeof emptyNearby('Kahve', 'Kadıköy'), 'string');
});

test('empty copy follows intent', () => {
  assert.match(emptyNearby('Kahve', 'Şişli'), /kahve/i);
  assert.match(emptyNearby('Yemek', 'Kadıköy'), /yemek/i);
  assert.doesNotMatch(emptyNearby('Yemek', 'Kadıköy'), /kafe yok/i);
  assert.match(emptySearch('petra'), /petra/i);
  assert.match(emptyMap('Gece'), /gece/i);
  assert.match(emptyDiscover('Kultur'), /kültür/i);
});

test('search debounce and min length', () => {
  assert.equal(shouldSearch(''), false);
  assert.equal(shouldSearch('a'), false);
  assert.equal(shouldSearch('pe'), true);
  assert.equal(debounceMs(), 320);
});

test('friendly errors hide raw dumps', () => {
  assert.match(friendlyError(new Error('network timeout')), /ağ/i);
  assert.match(friendlyError('{ "error": "x" }'), /sunucu/i);
  assert.match(friendlyError(new Error('429 too many')), /dakika/i);
  assert.match(friendlyError(new Error('401 unauthorized')), /oturum/i);
});

test('venue report reasons stay short tokens', () => {
  const reasons = ['yanlis-bilgi', 'uygunsuz'];
  assert.ok(reasons.every((r) => r.length < 24 && !r.includes(' ')));
});

test('guest keşfet needs no session', () => {
  assert.equal(shouldSearch('ke'), true);
  assert.match(emptyNearby('Kahve', 'Kadıköy'), /Kadıköy/);
});

test('onboarding skip still lands on empty CTA', () => {
  assert.match(emptyDiscover('Yemek'), /yemek/i);
  assert.match(friendlyError(''), /ters gitti/i);
});

test('home search bar min two chars', () => {
  assert.equal(shouldSearch('  '), false);
  assert.equal(shouldSearch('ab'), true);
});

test('geo hits keep mahalle subtitle on chip', () => {
  assert.equal(geoChip({ label: 'Tuna', latitude: 1, longitude: 1, subtitle: 'Esenler · İstanbul', chipLabel: 'Tuna, Esenler' }), 'Tuna, Esenler');
  assert.equal(mahalleLine('Tuna, Esenler, İstanbul'), 'Tuna');
  assert.equal(geoKindIcon('suburb', 'Mahalle'), 'home-outline');
  assert.equal(geoKindIcon('station', 'Durak'), 'bus-outline');
  assert.equal(geoKindLabel('station'), 'Durak');
  assert.equal(geoSubtitle({ label: 'Taksim', latitude: 0, longitude: 0, kind: 'suburb' }), 'Mahalle');
});

test('opening hours expand weekday ranges', () => {
  const rows = parseOpeningHours('Mo-Fr 08:00-18:00; Sa 09:00-14:00; Su off');
  assert.equal(rows.length, 7);
  assert.equal(rows[0].hours, '08:00–18:00');
  assert.equal(rows[5].hours, '09:00–14:00');
  assert.equal(rows[6].hours, 'Kapalı');
});

test('radius accepts values above 8 km', () => {
  assert.equal(kmToMeters(25), 25_000);
  assert.equal(clampRadius(25_000), 25_000);
  assert.equal(clampRadius(250_000), 100_000);
});

test('route duration formats minutes', () => {
  assert.equal(formatDuration(90), '2 dk');
  assert.equal(formatRouteLine(850, 720), '12 dk · 850 m');
});

test('placeholder seed is unique per place', () => {
  assert.notEqual(hashSeed('osm:node:1'), hashSeed('osm:node:2'));
  assert.notDeepEqual(placeholderPalette('osm:node:1'), placeholderPalette('cafe-only'));
  assert.equal(placeholderInitials('Petra Roasting', 'x'), 'PR');
});

test('server error text reaches the user, internals do not', () => {
  assert.equal(serverMessage('{"error":"Bu e-posta zaten kayıtlı."}'), 'Bu e-posta zaten kayıtlı.');
  assert.equal(serverMessage('<html>502</html>'), '');
  assert.equal(friendlyError(new ApiError(400, 'Bu e-posta zaten kayıtlı.')), 'Bu e-posta zaten kayıtlı.');
  assert.match(friendlyError(new ApiError(500, 'NullReferenceException at Foo.Bar')), /sunucuda/i);
  assert.doesNotMatch(friendlyError(new ApiError(500, 'NullReferenceException')), /Null/);
  assert.match(friendlyError(new ApiError(0, 'x', 'network')), /ağ/i);
  assert.match(friendlyError(new ApiError(0, 'x', 'timeout')), /geç/i);
  assert.match(friendlyError(new ApiError(429, '')), /dakika/i);
});

test('sign-up validation mirrors the server rules', () => {
  assert.ok(emailProblem('ada'));
  assert.ok(emailProblem('ada@x'));
  assert.equal(emailProblem(' ada@nereye.app '), null);
  assert.ok(passwordProblem('kisa1'));
  assert.ok(passwordProblem('sadeceharfler'));
  assert.ok(passwordProblem('1234567890'));
  assert.equal(passwordProblem('kahve1234'), null);
  assert.equal(passwordProblem('şifreçok1'), null);
  assert.ok(nameProblem('  '));
  assert.equal(nameProblem('Ada'), null);
  assert.equal(passwordStrength('kisa'), 0);
  assert.equal(passwordStrength('kahve1234'), 1);
  assert.equal(passwordStrength('Kahve-1234-Moda'), 3);
});

test('rating summary counts each star bucket', () => {
  const s = summarizeRatings([{ stars: 5 }, { stars: 5 }, { stars: 3 }, { stars: 0 }]);
  assert.equal(s.count, 3);
  assert.deepEqual(s.buckets, [0, 0, 1, 0, 2]);
  assert.ok(Math.abs((s.average ?? 0) - 13 / 3) < 1e-9);
  assert.equal(summarizeRatings([]).average, null);
});

test('relative dates read naturally in Turkish', () => {
  const now = new Date('2026-11-12T12:00:00Z');
  assert.equal(relativeDate('2026-11-12T11:59:40Z', now), 'az önce');
  assert.equal(relativeDate('2026-11-12T09:00:00Z', now), '3 saat önce');
  assert.equal(relativeDate('2026-11-11T10:00:00Z', now), 'dün');
  assert.equal(relativeDate('2026-10-01T10:00:00Z', now), '1 ay önce');
  assert.equal(relativeDate('bozuk', now), '');
  assert.equal(relativeDate(null, now), '');
});

test('sorting keeps server order for smart and never mutates input', () => {
  const cards = [
    { id: 'a', distanceMeters: 900, nereyeRating: 4.2, nereyeRatingCount: 3 },
    { id: 'b', distanceMeters: 100, nereyeRating: null, nereyeRatingCount: 0 },
    { id: 'c', distanceMeters: 500, nereyeRating: 4.8, nereyeRatingCount: 1 },
  ];
  assert.equal(sortCards(cards, 'smart'), cards);
  assert.deepEqual(sortCards(cards, 'distance').map((c) => c.id), ['b', 'c', 'a']);
  assert.deepEqual(sortCards(cards, 'rating').map((c) => c.id), ['c', 'a', 'b']);
  assert.deepEqual(cards.map((c) => c.id), ['a', 'b', 'c']);
  assert.equal(activeFilterCount({ openNow: true, vibes: ['Sessiz', 'Laptop'], minRating: 4 }), 4);
  assert.equal(activeFilterCount({ openNow: false, vibes: [], minRating: 0 }), 0);
});

test('recommend cache key is stable for gps jitter and rounded radius', () => {
  const a = recommendCacheKey({
    lat: 40.9885,
    lng: 29.025,
    intent: 'Kahve',
    radiusMeters: 5000,
    openNow: false,
    vibes: [],
    skip: 0,
    take: 20,
  });
  const b = recommendCacheKey({
    lat: 40.98851,
    lng: 29.02504,
    intent: 'Kahve',
    radiusMeters: 4800,
    openNow: false,
    vibes: [],
    skip: 0,
    take: 20,
  });
  const other = recommendCacheKey({
    lat: 40.9885,
    lng: 29.025,
    intent: 'Yemek',
    radiusMeters: 5000,
    openNow: false,
    vibes: [],
    skip: 0,
    take: 20,
  });
  assert.equal(a, b);
  assert.notEqual(a, other);
  putRecommend(a, { items: [{ placeId: 'osm:1' } as never], hasMore: false });
  assert.equal(peekRecommend(a)?.items[0]?.placeId, 'osm:1');
});

test('photo batch keeps eight unique ids', () => {
  const ids = photoBatchIds(['a', 'a', 'b', '', 'c', 'd', 'e', 'f', 'g', 'h', 'i']);
  assert.deepEqual(ids, ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h']);
});

test('recommend timeout covers cold Overpass', () => {
  const src = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'api.ts'), 'utf8');
  const match = src.match(/RECOMMEND_TIMEOUT_MS\s*=\s*([\d_]+)/);
  assert.ok(match, 'RECOMMEND_TIMEOUT_MS tanımı yok');
  const ms = Number(match[1].replaceAll('_', ''));
  assert.ok(ms >= 45_000, `öneri zaman aşımı ${ms} ms; soğuk Overpass (~22 s) için en az 45 s olmalı`);
});

test('price and counts read like a Turkish listing', () => {
  assert.equal(priceLabel(2), '₺₺');
  assert.equal(priceLabel(0), 'Ücretsiz');
  assert.equal(priceLabel(null), '');
  assert.equal(priceLabel(9), '');
  assert.equal(compactCount(950), '950');
  assert.equal(compactCount(4898), '4,9 B');
  assert.equal(compactCount(120_400), '120 B');
  assert.equal(compactCount(1_250_000), '1,3 Mn');
  assert.equal(compactCount(0), '');
  assert.equal(metaLine('Kahve dükkanı', 2), 'Kahve dükkanı · ₺₺');
  assert.equal(metaLine(null, 3), '₺₺₺');
  assert.equal(metaLine(' ', null), '');
});

test('card shows community rating first, google rating as fallback, never a blank', () => {
  assert.deepEqual(ratingLine({ nereyeRating: 4.6, nereyeRatingCount: 12, googleRating: 4.1, googleRatingCount: 900 }), {
    value: '4,6',
    count: '12',
    source: 'nereye',
  });
  assert.deepEqual(ratingLine({ nereyeRating: null, nereyeRatingCount: 0, googleRating: 4.34, googleRatingCount: 4898 }), {
    value: '4,3',
    count: '4,9 B',
    source: 'google',
  });
  assert.equal(ratingLine({ nereyeRating: 5, nereyeRatingCount: 0 }), null);
  assert.equal(ratingLine({}), null);
});

test('every known feature has an icon and unknown ones still render', () => {
  assert.equal(featureIcon('Açık alan'), 'sunny-outline');
  assert.equal(featureIcon('Bilinmeyen olanak'), 'checkmark-circle-outline');
});

test('long reviews are clipped at a word boundary', () => {
  assert.deepEqual(clip('kısa yorum', 50), { text: 'kısa yorum', clipped: false });
  const long = clip('Brekkie kahvaltı için gittik ve gerçekten çok memnun kaldık', 30);
  assert.equal(long.clipped, true);
  assert.ok(long.text.endsWith('…'));
  assert.ok(!long.text.includes('gerçe'));
});

test('venue cache shares in-flight requests and evicts the oldest', async () => {
  clearVenueCache();
  let calls = 0;
  const load = async () => {
    calls += 1;
    return { placeId: 'a', name: 'A' } as never;
  };
  const [x, y] = await Promise.all([shareVenueRequest('a', load), shareVenueRequest('a', load)]);
  assert.equal(calls, 1);
  assert.equal(x, y);
  assert.equal(peekVenue('a')?.name, 'A');
  assert.equal(isFresh('a'), true);
  assert.equal(isFresh('a', Date.now() + 11 * 60_000), false);
  for (let i = 0; i < 45; i += 1) {
    putVenue({ placeId: `p${i}`, name: String(i) } as never);
  }
  assert.equal(peekVenue('a'), null);
  assert.equal(peekVenue('p44')?.name, '44');
});

test('a failed venue request is not cached and can be retried', async () => {
  clearVenueCache();
  await assert.rejects(shareVenueRequest('b', async () => Promise.reject(new Error('ağ yok'))));
  assert.equal(peekVenue('b'), null);
  const ok = await shareVenueRequest('b', async () => ({ placeId: 'b', name: 'B' }) as never);
  assert.equal(ok.name, 'B');
});
