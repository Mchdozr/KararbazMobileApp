import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { weatherView } from './weather.ts';

test('hava notu: ikon, metin ve önerilen niyet düğmesi', () => {
  const rain = weatherView({ kind: 'Rain', temperatureC: 12, note: 'Yağmur yağıyor (12°). …', suggestIntent: 'Kultur' });
  assert.equal(rain?.icon, 'rainy-outline');
  assert.equal(rain?.action?.intent, 'Kultur');
  assert.equal(rain?.action?.label, 'Kültüre bak');
  const nice = weatherView({ kind: 'Clear', temperatureC: 22, note: 'Hava güzel (22°).', suggestIntent: 'AcikHava' });
  assert.equal(nice?.icon, 'sunny-outline');
  assert.equal(nice?.action?.label, 'Açık havaya bak');
  assert.equal(weatherView({ kind: 'Cloudy', temperatureC: 11, note: null, suggestIntent: null }), null);
  assert.equal(weatherView(null), null);
  assert.equal(weatherView({ kind: 'Snow', temperatureC: -1, note: 'Kar yağıyor (-1°).', suggestIntent: null })?.action, undefined);
});

test('hava notu: veri kaynağı atfı (CC BY) yalnızca kaynak varsa', () => {
  const met = weatherView({ kind: 'Rain', temperatureC: 9, note: 'Yağmur yağıyor (9°).', suggestIntent: null, source: 'MET Norway' });
  assert.equal(met?.credit, 'Kaynak: MET Norway');
  const fake = weatherView({ kind: 'Rain', temperatureC: 9, note: 'Yağmur yağıyor (9°).', suggestIntent: null });
  assert.equal(fake?.credit, undefined);
});
