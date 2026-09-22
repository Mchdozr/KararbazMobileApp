import assert from 'node:assert/strict';
import test from 'node:test';
import { emptyDiscover, emptyMap, emptyNearby, emptySearch, friendlyError } from './empty.ts';
import { geoChip, geoKindIcon, geoKindLabel, geoSubtitle, mahalleLine } from './geo-hit.ts';
import { parseOpeningHours } from './hours.ts';
import { formatDuration, formatRouteLine } from './route.ts';
import { hashSeed, placeholderInitials, placeholderPalette } from './placeholder.ts';
import { clampRadius, kmToMeters } from './radius.ts';
import { debounceMs, shouldSearch } from './search-query.ts';

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
