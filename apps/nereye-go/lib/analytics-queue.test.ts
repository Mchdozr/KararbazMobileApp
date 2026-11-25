import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { AnalyticsQueue, parseConsent, shouldAskConsent, toEvent } from './analytics-queue.ts';

test('olaylar sunucudaki AnalyticsRules aynası: bilinmeyen olay ve eksik anahtar atılır', () => {
  assert.deepEqual(toEvent('venue_view', { placeId: 'nereye:petra' }), { name: 'venue_view', placeId: 'nereye:petra' });
  assert.deepEqual(toEvent('list_view', { intent: 'Kahve' }), { name: 'list_view', intent: 'Kahve' });
  assert.deepEqual(toEvent('app_open'), { name: 'app_open' });
  assert.equal(toEvent('venue_view'), null);
  assert.equal(toEvent('venue_view', { placeId: 'boşluk var' }), null);
  assert.equal(toEvent('list_view', { intent: 'Uydurma' }), null);
  assert.equal(toEvent('email_login' as never), null);
});

test('kuyruk 50lik paketler verir, 200 olayı aşmaz', () => {
  const q = new AnalyticsQueue();
  for (let i = 0; i < 260; i++) {
    q.add({ name: 'app_open' });
  }
  assert.equal(q.pending, 200);
  assert.equal(q.take().length, 50);
  assert.equal(q.pending, 150);
  q.clear();
  assert.equal(q.pending, 0);
});

test('rıza: yalnızca açık "granted" izin verir; bir kez sorulur', () => {
  assert.equal(parseConsent('granted'), 'granted');
  assert.equal(parseConsent('denied'), 'denied');
  assert.equal(parseConsent(null), null);
  assert.equal(parseConsent('evet'), null);
  assert.equal(shouldAskConsent(null, true), true);
  assert.equal(shouldAskConsent(null, false), false); // liste gelmeden sorma
  assert.equal(shouldAskConsent('denied', true), false);
  assert.equal(shouldAskConsent('granted', true), false);
});

test('rıza yoksa hiçbir olay kuyruğa girmez; ret kuyruğu siler; açılış ve Profil bağlı', async () => {
  const { readFileSync } = await import('node:fs');
  const read = (rel: string) => readFileSync(new URL(rel, import.meta.url), 'utf8');
  const analytics = read('./analytics.ts');
  const trackBody = analytics.slice(analytics.indexOf('export function track('), analytics.indexOf('async function flush'));
  assert.match(trackBody, /if \(consent !== 'granted'\) \{\s*return;/);
  assert.match(analytics, /if \(!granted\) \{\s*queue\.clear\(\);/);
  assert.match(read('../app/_layout.tsx'), /hydrateAnalytics\(\)/);
  assert.match(read('../app/(tabs)/profile.tsx'), /setAnalyticsConsent\(v/);
  assert.match(read('../app/(tabs)/index.tsx'), /<AnalyticsPrompt/);
});
