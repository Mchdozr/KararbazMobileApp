import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { linkAbort, recommendPath } from './request-control.ts';

test('dış iptal isteğin denetleyicisini de durdurur, bağ kaldırılınca durdurmaz', () => {
  const outer = new AbortController();
  const inner = new AbortController();
  const unlink = linkAbort(outer.signal, inner);
  outer.abort();
  assert.equal(inner.signal.aborted, true);

  const outer2 = new AbortController();
  const inner2 = new AbortController();
  linkAbort(outer2.signal, inner2)();
  outer2.abort();
  assert.equal(inner2.signal.aborted, false);
  unlink();
});

test('önceden iptal edilmiş sinyal hemen durdurur', () => {
  const outer = new AbortController();
  outer.abort();
  const inner = new AbortController();
  linkAbort(outer.signal, inner);
  assert.equal(inner.signal.aborted, true);
  assert.doesNotThrow(() => linkAbort(undefined, new AbortController())());
});

test('öneri adresi konumu ~11 m yuvarlar: GPS titremesi aynı adrese düşer', () => {
  const a = recommendPath({ lat: 40.98853, lng: 29.02504, intent: 'Kahve', radiusMeters: 5000, openNow: false, vibes: [], skip: 0, take: 20 });
  const b = recommendPath({ lat: 40.98851, lng: 29.02496, intent: 'Kahve', radiusMeters: 5000, openNow: false, vibes: [], skip: 0, take: 20 });
  assert.equal(a, b);
  assert.equal(a, '/api/recommendations?lat=40.9885&lng=29.025&intent=Kahve&radiusMeters=5000&openNow=false&skip=0&take=20');
  const withVibes = recommendPath({ lat: 41, lng: 29, intent: 'Gece', radiusMeters: 1500, openNow: true, vibes: ['Canli', 'Sakin'], skip: 20, take: 20 });
  assert.ok(withVibes.endsWith('&skip=20&take=20&vibes=Canli,Sakin'));
});

test('iptal edilen istek ham İngilizce metin göstermez ve çevrimdışı sayılmaz', async () => {
  const { friendlyError } = await import('./empty.ts');
  const { isOffline } = await import('./offline.ts');
  const aborted = { name: 'ApiError', status: 0, kind: 'aborted', message: 'aborted' };
  assert.equal(friendlyError(aborted), 'İstek yarıda bırakıldı.');
  assert.equal(isOffline(aborted), false);
});

test('liste ekranları yeni yüklemede önceki isteği ağda durdurur', async () => {
  const { readFileSync } = await import('node:fs');
  const read = (rel: string) => readFileSync(new URL(rel, import.meta.url), 'utf8');
  for (const screen of ['../app/(tabs)/index.tsx', '../app/(tabs)/discover.tsx', '../app/(tabs)/map.tsx']) {
    const src = read(screen);
    assert.match(src, /useLatestRequest\(\)/, screen);
    assert.match(src, /signal/, screen);
  }
  assert.match(read('./api.ts'), /conditional: true/);
});
