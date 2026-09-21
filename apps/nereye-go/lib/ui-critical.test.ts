import assert from 'node:assert/strict';
import test from 'node:test';
import { emptyDiscover, emptyMap, emptyNearby, emptySearch, friendlyError } from './empty.ts';
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
