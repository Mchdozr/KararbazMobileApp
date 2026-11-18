import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { promoContactUrl } from './promo-contact.ts';

const read = (path: string) => readFileSync(new URL(path, import.meta.url), 'utf8');

test('owner screen never charges: no checkout or campaign creation call', () => {
  const owner = read('../app/owner/[id].tsx');
  assert.doesNotMatch(owner, /checkout|createPromotion|sandbox/i);
  assert.doesNotMatch(read('./api.ts'), /\/checkout|createPromotion/);
});

test('promotion contact link is https or mailto, otherwise the button stays disabled', () => {
  assert.equal(promoContactUrl(undefined), null);
  assert.equal(promoContactUrl(''), null);
  assert.equal(promoContactUrl('http://example.test/iletisim'), null);
  assert.equal(promoContactUrl('javascript:alert(1)'), null);
  assert.equal(promoContactUrl('https://example.test/iletisim'), 'https://example.test/iletisim');
  assert.equal(promoContactUrl('mailto:ekip@example.test'), 'mailto:ekip@example.test');
  const owner = read('../app/owner/[id].tsx');
  assert.match(owner, /disabled=\{!promoContact\}/);
  assert.match(owner, /İletişim bağlantısı henüz yok\./);
});

test('sign-up sends terms acceptance, and the terms box starts unchecked', () => {
  const auth = read('../app/auth.tsx');
  assert.match(auth, /useState\(false\);\s*\n/);
  assert.match(auth, /const \[terms, setTerms\] = useState\(false\)/);
  assert.match(read('./api.ts'), /JSON\.stringify\(\{ email, password, displayName, acceptTerms \}\)/);
});

test('unused sensitive permissions are blocked and the gallery is never asked for', () => {
  const app = JSON.parse(read('../app.json')).expo;
  for (const p of ['CAMERA', 'RECORD_AUDIO', 'READ_EXTERNAL_STORAGE', 'WRITE_EXTERNAL_STORAGE', 'READ_MEDIA_IMAGES', 'READ_MEDIA_VIDEO', 'SYSTEM_ALERT_WINDOW', 'ACCESS_BACKGROUND_LOCATION']) {
    assert.ok(app.android.blockedPermissions.includes(`android.permission.${p}`), p);
  }
  const picker = app.plugins.find((p: unknown) => Array.isArray(p) && p[0] === 'expo-image-picker');
  assert.equal(picker[1].cameraPermission, false);
  assert.equal(picker[1].microphonePermission, false);
  for (const f of ['../app/review/[id].tsx', '../app/owner/[id].tsx', '../app/(tabs)/profile.tsx']) {
    assert.doesNotMatch(read(f), /requestMediaLibraryPermissionsAsync|launchCameraAsync/, f);
  }
});

test('share uses the app scheme, never a map website, and adds https only when a human set one', async () => {
  const { venueShareText } = await import('./share-text.ts');
  const text = venueShareText('nereye:fazil', 'Fazıl Bey', 'Kadıköy', undefined);
  assert.match(text, /^nereyego:\/\/venue\/nereye%3Afazil$/m);
  assert.doesNotMatch(text, /openstreetmap\.org|google\.com\/maps|https?:\/\//);
  assert.doesNotMatch(venueShareText('x', 'A', null, 'http://insecure.test'), /http:\/\//);
  assert.match(venueShareText('x', 'A', null, 'https://ornek.test/'), /^https:\/\/ornek\.test\/venue\/x$/m);
  assert.doesNotMatch(read('../app/venue/[id].tsx'), /openstreetmap\.org/);
});

test('CI runs both dotnet test projects and the client checks, and never publishes', () => {
  const ci = read('../../../.github/workflows/ci.yml');
  assert.match(ci, /dotnet test tests\/Nereye\.Domain\.Tests/);
  assert.match(ci, /dotnet test tests\/Nereye\.Api\.Tests/);
  assert.match(ci, /npm ci && npm test && npm run typecheck/);
  assert.doesNotMatch(ci, /eas (build|submit)|play-console|r0adkll|upload-google-play/i);
  assert.match(read('../AGENTS.md'), /v58\.0\.0/);
});
