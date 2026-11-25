import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { eventStartIso, eventTimeLabel, parseClock } from './tonight.ts';

// 2026-11-24 18:00 İstanbul = 15:00Z
const now = Date.parse('2026-11-24T15:00:00Z');

test('saat etiketi İstanbul saatiyle; sürüyorsa ve gece yarısını geçiyorsa belirtilir', () => {
  assert.equal(eventTimeLabel('2026-11-24T17:30:00Z', '2026-11-24T19:30:00Z', false, now), '20:30');
  assert.equal(eventTimeLabel('2026-11-24T14:00:00Z', '2026-11-24T16:00:00Z', true, now), "Şu an · 19:00'a kadar");
  assert.equal(eventTimeLabel('2026-11-24T22:00:00Z', null, false, now), 'Gece 01:00');
  assert.equal(eventTimeLabel('2026-11-24T14:30:00Z', null, true, now), 'Şu an sürüyor');
});

test('saat girişi SS:DD', () => {
  assert.deepEqual(parseClock('20:30'), { h: 20, m: 30 });
  assert.deepEqual(parseClock('9.15'), { h: 9, m: 15 });
  assert.equal(parseClock('25:00'), null);
  assert.equal(parseClock('akşam'), null);
});

test('sahip formu: gün + saat İstanbul saatinden UTC ISO üretir', () => {
  assert.equal(eventStartIso(0, '20:30', now), '2026-11-24T17:30:00.000Z');
  assert.equal(eventStartIso(0, '01:00', now), '2026-11-24T22:00:00.000Z'); // bu gecenin 01:00'i
  assert.equal(eventStartIso(1, '01:00', now), '2026-11-25T22:00:00.000Z');
  assert.equal(eventStartIso(2, '19:00', now), '2026-11-26T16:00:00.000Z');
  assert.equal(eventStartIso(0, 'x', now), null);
});

test('sahip listesi: geçmiş etkinlikler gizlenir, gün etiketi Türkçe', async () => {
  const { eventDayLabel, upcomingEvents } = await import('./tonight.ts');
  const list = upcomingEvents(
    [
      { id: 'a', title: 'Geçti', startsAt: '2026-11-23T17:00:00Z', endsAt: '2026-11-23T19:00:00Z' },
      { id: 'b', title: 'Sonra', startsAt: '2026-11-26T17:00:00Z' },
      { id: 'c', title: 'Bu akşam', startsAt: '2026-11-24T17:30:00Z', endsAt: '2026-11-24T19:30:00Z' },
    ],
    now,
  );
  assert.deepEqual(list.map((e) => e.id), ['c', 'b']);
  assert.equal(eventDayLabel('2026-11-24T17:30:00Z', now), 'Bugün 20:30');
  assert.equal(eventDayLabel('2026-11-25T17:00:00Z', now), 'Yarın 20:00');
  assert.equal(eventDayLabel('2026-11-26T17:00:00Z', now), '26.11 20:00');
});
