import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { statBars, statTiles } from './venue-stats.ts';

const day = (d: string, views: number, directions = 0, saves = 0) => ({ day: d, views, directions, saves });

test('son 14 gün çubukları en yükseğe göre ölçeklenir, boş gün sıfır yüksekliktir', () => {
  const days = Array.from({ length: 30 }, (_, i) => day(`2026-11-${String(i + 1).padStart(2, '0')}`, i === 29 ? 8 : i === 28 ? 4 : 0));
  const bars = statBars(days, 14);
  assert.equal(bars.length, 14);
  assert.equal(bars[13].ratio, 1);
  assert.equal(bars[12].ratio, 0.5);
  assert.equal(bars[0].ratio, 0);
  assert.equal(bars[13].label, '30.11');
  assert.equal(statBars([], 14).length, 0);
});

test('özet kutuları Türkçe ve dönüşüm oranı ile', () => {
  const tiles = statTiles({ views: 120, directions: 30, saves: 7, shares: 2, days: [] });
  assert.deepEqual(tiles.map((t) => t.label), ['Görüntülenme', 'Yol tarifi', 'Kaydetme']);
  assert.equal(tiles[1].note, 'dönüşüm %25');
  assert.equal(statTiles({ views: 0, directions: 0, saves: 0, shares: 0, days: [] })[1].note, undefined);
});
