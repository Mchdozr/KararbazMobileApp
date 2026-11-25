// Mekan sahibi istatistiğinin görünümü. React Native içe aktarmaz: node ile test edilir.
import type { VenueStats } from './types.ts';

export type StatBar = { label: string; value: number; ratio: number };
export type StatTile = { label: string; value: number; note?: string };

/** Son n günün görüntülenme çubukları; en yüksek gün 1. */
export function statBars(days: VenueStats['days'], n = 14): StatBar[] {
  const last = days.slice(-n);
  const max = Math.max(0, ...last.map((d) => d.views));
  return last.map((d) => {
    const [, month, dayOfMonth] = d.day.slice(0, 10).split('-');
    return { label: `${dayOfMonth}.${month}`, value: d.views, ratio: max > 0 ? d.views / max : 0 };
  });
}

export function statTiles(stats: VenueStats): StatTile[] {
  const share = stats.views > 0 ? Math.round((stats.directions / stats.views) * 100) : null;
  return [
    { label: 'Görüntülenme', value: stats.views },
    { label: 'Yol tarifi', value: stats.directions, note: share != null ? `dönüşüm %${share}` : undefined },
    { label: 'Kaydetme', value: stats.saves },
  ];
}
