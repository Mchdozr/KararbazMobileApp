import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { ALL, clearCoversForTest, COVER_LIMIT, dropCover, getCover, restoreCovers, setCover, snapshotCovers, subscribeCovers } from './cover-store.ts';

const read = (path: string) => readFileSync(new URL(path, import.meta.url), 'utf8');

test('a cover seen once is reused, change notifies only that card, and the store stays bounded', () => {
  clearCoversForTest();
  const seen: string[] = [];
  const off = subscribeCovers((id) => seen.push(id));
  assert.equal(setCover('osm:node/1', '/api/media/google?name=places%2FA%2Fphotos%2F1'), true);
  assert.equal(setCover('osm:node/1', '/api/media/google?name=places%2FA%2Fphotos%2F1'), false);
  assert.equal(setCover('osm:node/2', '  '), false);
  assert.deepEqual(seen, ['osm:node/1']);
  assert.equal(getCover('osm:node/1'), '/api/media/google?name=places%2FA%2Fphotos%2F1');

  for (let i = 0; i < COVER_LIMIT + 50; i += 1) {
    setCover(`p${i}`, `https://img.test/${i}.jpg`);
  }
  assert.equal(snapshotCovers().length, COVER_LIMIT);
  assert.equal(getCover('p0'), null);
  off();
});

test('a broken cover is dropped and restore never overwrites a newer cover', () => {
  clearCoversForTest();
  setCover('a', 'https://img.test/new.jpg');
  const seen: string[] = [];
  const off = subscribeCovers((id) => seen.push(id));
  restoreCovers([['a', 'https://img.test/old.jpg'], ['b', 'https://img.test/b.jpg'], ['bad'], 7]);
  assert.equal(getCover('a'), 'https://img.test/new.jpg');
  assert.equal(getCover('b'), 'https://img.test/b.jpg');
  assert.deepEqual(seen, [ALL]);
  dropCover('b', 'https://img.test/other.jpg');
  assert.equal(getCover('b'), 'https://img.test/b.jpg');
  dropCover('b', 'https://img.test/b.jpg');
  assert.equal(getCover('b'), null);
  off();
});

test("cards never show another venue's stock photo while the real one loads", () => {
  const image = read('../components/VenueImage.tsx');
  assert.doesNotMatch(image, /INTENT_PHOTOS|fallback/);
  assert.match(image, /useCover\(placeId\)/);
  assert.doesNotMatch(read('../components/VenueProductCard.tsx'), /INTENT_PHOTOS/);
  assert.match(read('../app/venue/[id].tsx'), /rememberCover\(detail\.placeId/);
});
