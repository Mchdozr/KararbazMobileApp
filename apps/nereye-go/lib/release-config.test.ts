import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { buildConfig, cleartextOf, TARGET_SDK, variantOf } from '../app.config.ts';
import { apiCandidates, DEV_LAN_API, resolveApiUrl } from './release-config.ts';

const base = JSON.parse(readFileSync(new URL('../app.json', import.meta.url), 'utf8')).expo;
const prod = (url?: string) => ({ EAS_BUILD_PROFILE: 'production', EXPO_PUBLIC_API_URL: url, GOOGLE_MAPS_ANDROID_API_KEY: 'test-maps-key-not-real' });

test('production build fails without an API URL', () => {
  assert.throws(() => buildConfig(base, prod(undefined)), /EXPO_PUBLIC_API_URL tanımlı değil/);
  assert.throws(() => buildConfig(base, prod('   ')), /tanımlı değil/);
  assert.throws(() => buildConfig(base, { APP_VARIANT: 'production' }), /tanımlı değil/);
});

test('production build fails with a plain http URL', () => {
  assert.throws(() => buildConfig(base, prod('http://192.168.1.248:5079')), /https ile başlamalı/);
  assert.throws(() => buildConfig(base, prod('http://api.example.test')), /https ile başlamalı/);
  assert.throws(() => buildConfig(base, prod('https://')), /https ile başlamalı/);
});

test('production config has cleartext off and no arbitrary loads', () => {
  const cfg = buildConfig(base, prod('https://api.example.test'));
  assert.equal(cleartextOf(cfg), false);
  assert.equal((cfg.ios?.infoPlist as Record<string, unknown>)?.NSAppTransportSecurity, undefined);
  assert.equal((cfg.extra as Record<string, unknown>).variant, 'production');
  assert.equal(cfg.plugins?.filter((p) => Array.isArray(p) && p[0] === 'expo-build-properties').length, 1);
});

test('target SDK is pinned and never below 35', () => {
  assert.ok(TARGET_SDK >= 35);
});

test('preview is also a release build: https required, cleartext off', () => {
  assert.equal(variantOf({ EAS_BUILD_PROFILE: 'preview' }), 'preview');
  assert.throws(() => buildConfig(base, { EAS_BUILD_PROFILE: 'preview' }), /preview/);
  assert.equal(cleartextOf(buildConfig(base, { EAS_BUILD_PROFILE: 'preview', EXPO_PUBLIC_API_URL: 'https://a.test' })), false);
});

test('development variant may use the LAN over http', () => {
  const cfg = buildConfig(base, {});
  assert.equal(variantOf({}), 'development');
  assert.equal(cleartextOf(cfg), true);
  assert.deepEqual((cfg.ios?.infoPlist as Record<string, unknown>).NSAppTransportSecurity, { NSAllowsArbitraryLoads: true });
});

test('app.json alone never enables cleartext', () => {
  assert.equal(base.android.usesCleartextTraffic, undefined);
  assert.equal(JSON.stringify(base.plugins).includes('usesCleartextTraffic'), false);
  assert.equal(base.ios.infoPlist.NSAppTransportSecurity, undefined);
});

test('eas.json production carries no API host and no tunnel', () => {
  const eas = readFileSync(new URL('../eas.json', import.meta.url), 'utf8');
  assert.doesNotMatch(eas, /trycloudflare|EXPO_PUBLIC_API_URL|http:\/\//);
});

test('runtime address: __DEV__ falls back to the LAN, release never does', () => {
  assert.equal(resolveApiUrl(true, undefined), DEV_LAN_API);
  assert.equal(resolveApiUrl(true, 'http://10.0.2.2:5079/'), 'http://10.0.2.2:5079');
  assert.equal(resolveApiUrl(false, undefined), '');
  assert.equal(resolveApiUrl(false, 'http://192.168.1.248:5079'), '');
  assert.equal(resolveApiUrl(false, 'https://api.example.test/'), 'https://api.example.test');
  // Yerel web önizlemesi: yalnızca aynı makine. LAN ya da tünel HTTP'si hâlâ reddedilir.
  assert.equal(resolveApiUrl(false, 'http://localhost:5079'), 'http://localhost:5079');
  assert.equal(resolveApiUrl(false, 'http://127.0.0.1:5079/'), 'http://127.0.0.1:5079');
  assert.equal(resolveApiUrl(false, 'http://localhost.evil.test'), '');
  assert.equal(resolveApiUrl(false, 'http://10.0.2.2:5079'), '');
});

test('emulator and localhost candidates exist only in __DEV__', () => {
  const dev = apiCandidates(true, DEV_LAN_API, 'http://192.168.1.50:5079');
  assert.ok(dev.includes('http://10.0.2.2:5079') && dev.includes('http://localhost:5079'));
  assert.ok(dev.includes('http://192.168.1.50:5079'));
  assert.deepEqual(apiCandidates(false, 'https://api.example.test', 'http://192.168.1.50:5079'), ['https://api.example.test']);
  assert.deepEqual(apiCandidates(false, '', 'http://10.0.2.2:5079'), []);
});

test('the developer server field cannot open outside __DEV__', () => {
  const src = readFileSync(new URL('../app/auth.tsx', import.meta.url), 'utf8');
  // Alanı açan dokunuş işleyicisi __DEV__ değilse hemen döner.
  assert.match(src, /if \(!__DEV__\) \{\s*return;\s*\}\s*taps\.current \+= 1;/);
  // Alan yalnızca __DEV__ iken çizilir ve girilen adres yalnızca __DEV__ iken kullanılır.
  for (const m of src.matchAll(/showServer \?/g)) {
    assert.equal(src.slice(m.index! - 11, m.index), '__DEV__ && ', 'showServer koşulsuz çiziliyor');
  }
  assert.match(src, /if \(__DEV__ && showServer && base\.trim\(\)/);
  assert.equal([...src.matchAll(/setShowServer\(/g)].length, 1);
});
