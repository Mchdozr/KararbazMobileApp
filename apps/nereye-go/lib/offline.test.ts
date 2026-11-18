import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { isOffline, offlineBanner, savedListName, toSaved, usableSaved } from './offline.ts';

const read = (path: string) => readFileSync(new URL(path, import.meta.url), 'utf8');
const card = (i: number) => ({ placeId: `p${i}`, name: `Mekan ${i}` }) as never;

test('only connection failures count as offline, server answers do not', () => {
  assert.equal(isOffline({ name: 'ApiError', kind: 'network', status: 0 }), true);
  assert.equal(isOffline({ name: 'ApiError', kind: 'timeout', status: 0 }), true);
  assert.equal(isOffline({ name: 'ApiError', kind: 'http', status: 500 }), false);
  assert.equal(isOffline(new Error('x')), false);
  assert.equal(isOffline(null), false);
});

test('a saved list is used only for the same intent and never invented', () => {
  const saved = toSaved('Kultur', 'Kadıköy', Array.from({ length: 30 }, (_, i) => card(i)), new Date('2026-11-18T09:05:00'));
  assert.equal(saved.items.length, 20);
  assert.equal(usableSaved(saved, 'Kultur'), saved);
  assert.equal(usableSaved(saved, 'Yemek'), null);
  assert.equal(usableSaved(null, 'Kultur'), null);
  assert.equal(usableSaved({ ...saved, items: [] }, 'Kultur'), null);
  assert.notEqual(savedListName('Kultur'), savedListName('Yemek'));
});

test('offline band says when the list is from', () => {
  const text = offlineBanner({ savedAt: new Date('2026-11-18T09:05:00').toISOString(), label: 'Kadıköy' }, new Date('2026-11-18T12:00:00'));
  assert.match(text, /^Çevrimdışısın\./);
  assert.match(text, /Kadıköy için bugün 09:05/);
  const old = offlineBanner({ savedAt: new Date('2026-11-16T21:30:00').toISOString(), label: 'Moda' }, new Date('2026-11-18T12:00:00'));
  assert.match(old, /16\.11 21:30/);
});

test('home "more" list is windowed (FlatList), not mapped inside a ScrollView', () => {
  const home = read('../app/(tabs)/index.tsx');
  assert.match(home, /<FlatList/);
  assert.doesNotMatch(home, /organic\.map\(/);
  assert.doesNotMatch(home, /<ScrollView/);
});

test('large cached data never goes to SecureStore (2 KB limit)', () => {
  const storage = read('./storage.ts');
  assert.match(storage, /export async function setLargeJson/);
  const large = storage.slice(storage.indexOf('export async function setLargeJson'));
  assert.doesNotMatch(large, /SecureStore/);
});
