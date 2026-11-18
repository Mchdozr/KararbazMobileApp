import assert from 'node:assert/strict';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { buildConfig } from '../app.config.ts';
import { mapMode } from './map-config.ts';

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

test('Google content is only drawn on a Google map', () => {
  assert.equal(mapMode('android', true, false), 'google');
  assert.equal(mapMode('android', false, true), 'google'); // Expo Go kendi anahtarını taşır
  assert.equal(mapMode('android', false, false), 'no-key');
  assert.equal(mapMode('ios', true, false), 'unsupported');
  assert.equal(mapMode('web', true, false), 'unsupported');
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
