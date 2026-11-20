import assert from 'node:assert/strict';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { buildConfig } from '../app.config.ts';
import { mapAttribution, mapMode } from './map-config.ts';

const repo = fileURLToPath(new URL('../../../', import.meta.url));
const SKIP = new Set(['node_modules', '.git', 'bin', 'obj', '.expo', 'dist', 'web-build', 'android', 'ios', 'build', '.dart_tool']);
const TEXT = /\.(ts|tsx|js|jsx|json|cs|razor|md|yml|yaml|xml|gradle|kts|plist|html|css|cshtml|config|env|example)$/i;

function* files(dir: string): Generator<string> {
  for (const name of readdirSync(dir)) {
    if (SKIP.has(name)) {
      continue;
    }
    const path = join(dir, name);
    const st = statSync(path);
    if (st.isDirectory()) {
      yield* files(path);
    } else if (TEXT.test(name) && st.size < 2_000_000) {
      yield path;
    }
  }
}

const sources = [...files(repo)].map((path) => ({ path: relative(repo, path), text: readFileSync(path, 'utf8') }));

test('no public OSM tile server anywhere in the repo', () => {
  // Aranan adres parçalı yazılır: bu test dosyası kendini bulmasın.
  const host = ['tile', 'openstreetmap', 'org'].join('.');
  const hits = sources.filter((f) => f.text.includes(host)).map((f) => f.path);
  assert.deepEqual(hits, []);
});

test('no Google API key is committed in source', () => {
  const hits = sources.filter((f) => /AIza[0-9A-Za-z_-]{35}/.test(f.text)).map((f) => f.path);
  assert.deepEqual(hits, []);
});

test('a real map is drawn on every phone', () => {
  assert.equal(mapMode('android', true, false), 'google');
  assert.equal(mapMode('android', false, true), 'google'); // Expo Go kendi anahtarını taşır
  assert.equal(mapMode('android', false, false), 'no-key');
  // iPhone: Apple Haritalar anahtarsız çalışır (Expo Go dahil); iOS anahtarı verilirse Google.
  assert.equal(mapMode('ios', false, true), 'apple');
  assert.equal(mapMode('ios', false, false), 'apple');
  assert.equal(mapMode('ios', false, false, true), 'google');
  assert.equal(mapMode('web', true, false), 'web');
  assert.equal(mapAttribution('apple'), 'Harita © Apple · Mekan verisi © OpenStreetMap katkıcıları');
  assert.doesNotMatch(readFileSync(new URL('../components/Map.tsx', import.meta.url), 'utf8'), /bu cihazda yok/);
});

test('an Android production build without a maps key does not compile', () => {
  const base = JSON.parse(readFileSync(new URL('../app.json', import.meta.url), 'utf8')).expo;
  const env = { APP_VARIANT: 'production', EXPO_PUBLIC_API_URL: 'https://api.example.test' };
  assert.throws(() => buildConfig(base, env), /GOOGLE_MAPS_ANDROID_API_KEY/);
  const ok = buildConfig(base, { ...env, GOOGLE_MAPS_ANDROID_API_KEY: 'test-maps-key-not-real', GOOGLE_MAPS_IOS_API_KEY: 'test-ios-key-not-real' });
  assert.equal(ok.ios?.config?.googleMapsApiKey, 'test-ios-key-not-real');
  assert.equal((ok.extra as Record<string, unknown>).googleMapsIosEnabled, true);
  assert.equal(JSON.stringify(ok.extra).includes('test-ios-key-not-real'), false);
});

test('maps key comes only from the build environment and never lands in extra', () => {
  const base = JSON.parse(readFileSync(new URL('../app.json', import.meta.url), 'utf8')).expo;
  const none = buildConfig(base, {});
  assert.equal(none.android?.config?.googleMaps, undefined);
  assert.equal((none.extra as Record<string, unknown>).googleMapsEnabled, false);

  const key = 'test-maps-key-not-real';
  const withKey = buildConfig(base, { GOOGLE_MAPS_ANDROID_API_KEY: key });
  assert.equal(withKey.android?.config?.googleMaps?.apiKey, key);
  assert.equal((withKey.extra as Record<string, unknown>).googleMapsEnabled, true);
  assert.equal(JSON.stringify(withKey.extra).includes(key), false);
});

test('map screens no longer reference UrlTile', () => {
  for (const f of sources.filter((s) => s.path.includes('apps/nereye-go/') && /\.(tsx?)$/.test(s.path) && !s.path.endsWith('.test.ts'))) {
    assert.doesNotMatch(f.text, /UrlTile/, f.path);
  }
});

test('native and web map modules export the same names', () => {
  const native = readFileSync(new URL('../components/Map.tsx', import.meta.url), 'utf8');
  const web = readFileSync(new URL('../components/Map.web.tsx', import.meta.url), 'utf8');
  for (const pattern of [/export function currentMapMode/, /export default MapView/, /export type MapRef/, /\bRegion\b/, /\bMarker\b/]) {
    assert.match(native, pattern);
    assert.match(web, pattern);
  }
});
