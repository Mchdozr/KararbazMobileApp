export function formatDuration(seconds: number) {
  const m = Math.max(1, Math.round(seconds / 60));
  if (m >= 60) {
    const h = Math.floor(m / 60);
    const rest = m % 60;
    return rest ? `${h} sa ${rest} dk` : `${h} sa`;
  }
  return `${m} dk`;
}

export function formatRouteLine(distanceMeters: number, durationSeconds: number) {
  const dist =
    distanceMeters >= 1000 ? `${(distanceMeters / 1000).toFixed(1)} km` : `${Math.round(distanceMeters)} m`;
  return `${formatDuration(durationSeconds)} · ${dist}`;
}
