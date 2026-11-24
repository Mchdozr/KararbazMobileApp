import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { CrashQueue, toReport } from './crash-queue.ts';
import { scrub } from './scrub.ts';

test('ayıklama: e-posta, anahtar, koordinat, telefon ve sorgu dizesi gitmez (sunucudaki ErrorScrubber aynası)', () => {
  const raw =
    'Fetch failed for deniz.kaya@example.com at /api/recommendations?lat=40.98853&lng=29.02504 ' +
    'Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0In0.abcDEF123_- tel 05321234567 (40.9885, 29.0250)';
  const clean = scrub(raw);
  for (const leaked of ['deniz.kaya', 'eyJhbGci', '40.98853', '29.0250', '05321234567']) {
    assert.ok(!clean.includes(leaked), leaked);
  }
  assert.ok(clean.includes('<e-posta>'));
  assert.ok(clean.includes('/api/recommendations?<sorgu>'));
  const stack = "TypeError: undefined is not an object (evaluating 'card.name')\n    at VenueProductCard (index.bundle:1:23456)";
  assert.equal(scrub(stack), stack);
  assert.equal(scrub(undefined), '');
});

test('rapor: Error ve Error olmayan değerler, alan sınırları', () => {
  const r = toReport(new TypeError('x'.repeat(900)), { fatal: true, screen: '/venue/[id]', platform: 'ios', appVersion: '1.4.0' });
  assert.equal(r.message.length, 500);
  assert.ok(r.message.startsWith('TypeError: '));
  assert.equal(r.fatal, true);
  assert.equal(r.screen, '/venue/[id]');
  assert.equal(toReport('düz metin', { fatal: false, platform: 'web' }).message, 'düz metin');
  assert.equal(toReport({ weird: 1 }, { fatal: false, platform: 'web' }).message, 'Bilinmeyen hata');
});

test('kuyruk: aynı hata oturumda en fazla 3 kez, toplam 30, gönderim 10lu paketler', () => {
  const q = new CrashQueue();
  for (let i = 0; i < 5; i++) {
    q.add(toReport(new Error('aynı'), { fatal: false, platform: 'android' }));
  }
  assert.equal(q.pending, 3);
  for (let i = 0; i < 40; i++) {
    q.add(toReport(new Error('farklı ' + 'abcdefghijklmnopqrstuvwxyz'[i % 26] + i), { fatal: false, platform: 'android' }));
  }
  assert.equal(q.pending, 30);
  assert.equal(q.take().length, 10);
  assert.equal(q.pending, 20);
});

test('kuyruk: gönderilemeyen paket geri konur, sınır aşılmaz', () => {
  const q = new CrashQueue();
  q.add(toReport(new Error('a'), { fatal: false, platform: 'web' }));
  const batch = q.take();
  q.restore(batch);
  assert.equal(q.pending, 1);
});

test('uygulama açılışta hata raporlamayı kurar, ErrorBoundary rapor gönderir', async () => {
  const { readFileSync } = await import('node:fs');
  const read = (rel: string) => readFileSync(new URL(rel, import.meta.url), 'utf8');
  assert.match(read('../app/_layout.tsx'), /installCrashReporting\(\)/);
  assert.match(read('../components/AppErrorBoundary.tsx'), /reportError\(error/);
  // Ekran adı yol kalıbıdır (useSegments): mekan kimliği gibi değerler rapora girmez.
  assert.match(read('../app/_layout.tsx'), /useSegments\(\)/);
});
