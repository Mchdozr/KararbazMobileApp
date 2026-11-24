import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { dativeTime, locativeTime, openLine } from './open-status.ts';

test('yönelme eki saatin okunuşuna uyar', () => {
  assert.equal(dativeTime('23:00'), "23:00'e");
  assert.equal(dativeTime('22:00'), "22:00'ye");
  assert.equal(dativeTime('06:00'), "06:00'ya");
  assert.equal(dativeTime('00:00'), "00:00'a");
  assert.equal(dativeTime('23:30'), "23:30'a");
  assert.equal(dativeTime('19:00'), "19:00'a");
  assert.equal(dativeTime('20:00'), "20:00'ye");
  assert.equal(dativeTime('21:45'), "21:45'e");
});

test('bulunma eki sert ünsüzde -te/-ta olur', () => {
  assert.equal(locativeTime('09:00'), "09:00'da");
  assert.equal(locativeTime('08:00'), "08:00'de");
  assert.equal(locativeTime('04:00'), "04:00'te");
  assert.equal(locativeTime('13:00'), "13:00'te");
  assert.equal(locativeTime('17:40'), "17:40'ta");
  assert.equal(locativeTime('10:30'), "10:30'da");
  assert.equal(locativeTime('06:00'), "06:00'da");
});

test('açık mekan: kapanış saati, son 45 dakikada uyarı', () => {
  assert.deepEqual(openLine({ openNow: true, closesAt: '23:00', closesInMinutes: 180 }, 1200), {
    text: "23:00'e kadar açık",
    tone: 'open',
    short: 'Açık',
  });
  const soon = openLine({ openNow: true, closesAt: '23:00', closesInMinutes: 30 }, 1350);
  assert.equal(soon?.tone, 'soon');
  assert.equal(soon?.text, 'Yakında kapanıyor · 23:00');
  assert.equal(openLine({ openNow: true }, 600)?.text, 'Şu an açık');
});

test('kapalı mekan: bugün, yarın ya da yalnızca "kapalı"', () => {
  assert.equal(openLine({ openNow: false, opensAt: '09:00', opensInMinutes: 40 }, 500)?.text, '40 dk sonra açılıyor');
  assert.equal(openLine({ openNow: false, opensAt: '18:00', opensInMinutes: 300 }, 780)?.text, "18:00'de açılıyor");
  assert.equal(openLine({ openNow: false, opensAt: '09:00', opensInMinutes: 600 }, 1380)?.text, "Yarın 09:00'da açılıyor");
  assert.equal(openLine({ openNow: false, opensAt: '09:00', opensInMinutes: 4000 }, 600)?.text, 'Şu an kapalı');
  assert.equal(openLine({ openNow: false }, 600)?.text, 'Şu an kapalı');
});

test('bilinmiyorsa satır yok', () => {
  assert.equal(openLine({}, 600), null);
  assert.equal(openLine({ openNow: null }, 600), null);
});
