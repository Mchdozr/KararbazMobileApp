import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { contrast, darkColors, lightColors, resolveScheme } from './palette.ts';

for (const [name, c] of [['açık', lightColors], ['koyu', darkColors]] as const) {
  test(`${name} palet: metin ve düğmeler WCAG AA (4,5:1)`, () => {
    const pairs: [string, string, string][] = [
      ['ink/bg', c.ink, c.bg],
      ['ink/surface', c.ink, c.surface],
      ['muted/surface', c.muted, c.surface],
      ['muted/bg', c.muted, c.bg],
      ['coral/surface (bağlantı, meta)', c.coral, c.surface],
      ['coral/bg', c.coral, c.bg],
      ['onCoral/coral (birincil düğme)', c.onCoral, c.coral],
      ['danger/surface (hata metni)', c.danger, c.surface],
      ['onDanger/danger (yıkıcı düğme)', c.onDanger, c.danger],
      ['onAccent/accent (canlı rozet)', c.onAccent, c.accent],
      ['ink/coralSoft (bilgi kutusu)', c.ink, c.coralSoft],
      ['ink/dangerSoft (hata kutusu)', c.ink, c.dangerSoft],
      ['white/espresso (koyu bant)', c.white, c.espresso],
    ];
    for (const [label, fg, bg] of pairs) {
      assert.ok(contrast(fg, bg) >= 4.5, `${label}: ${contrast(fg, bg).toFixed(2)}`);
    }
  });
}

test('iki palet aynı belirteçleri taşır', () => {
  assert.deepEqual(Object.keys(darkColors).sort(), Object.keys(lightColors).sort());
});

test('tema seçimi: kullanıcı tercihi sistemi ezer, "sistem" sistemi izler', () => {
  assert.equal(resolveScheme('dark', 'light'), 'dark');
  assert.equal(resolveScheme('light', 'dark'), 'light');
  assert.equal(resolveScheme('system', 'dark'), 'dark');
  assert.equal(resolveScheme(null, 'dark'), 'dark');
  assert.equal(resolveScheme(null, null), 'light');
  assert.equal(resolveScheme('saçma', 'dark'), 'dark');
});

test('kontrast hesabı', () => {
  assert.equal(Math.round(contrast('#000000', '#ffffff')), 21);
  assert.equal(contrast('#777777', '#777777'), 1);
});
