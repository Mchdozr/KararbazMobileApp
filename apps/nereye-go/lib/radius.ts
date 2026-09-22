export const RADIUS_MIN_M = 200;
export const RADIUS_MAX_M = 100_000;
export const RADIUS_DEFAULT_M = 1500;

export function clampRadius(meters: number) {
  if (!Number.isFinite(meters) || meters <= 0) {
    return RADIUS_DEFAULT_M;
  }
  return Math.min(RADIUS_MAX_M, Math.max(RADIUS_MIN_M, Math.round(meters)));
}

export function kmToMeters(km: number) {
  return clampRadius(km * 1000);
}

export function metersToKm(meters: number) {
  return Math.round((meters / 1000) * 10) / 10;
}

export function radiusLabel(meters: number) {
  const km = metersToKm(meters);
  return km >= 10 ? `${Math.round(km)} km` : `${km} km`;
}
