import { t } from './i18n.ts';
export function formatDuration(seconds: number) {
  const m = Math.max(1, Math.round(seconds / 60));
  if (m >= 60) {
    const h = Math.floor(m / 60);
    const rest = m % 60;
    return rest ? t('{h} sa {m} dk', { h, m: rest }) : t('{h} sa', { h });
  }
  return t('{m} dk', { m });
}

export function formatRouteLine(distanceMeters: number, durationSeconds: number) {
  const dist =
    distanceMeters >= 1000 ? `${(distanceMeters / 1000).toFixed(1)} km` : `${Math.round(distanceMeters)} m`;
  return `${formatDuration(durationSeconds)} · ${dist}`;
}
