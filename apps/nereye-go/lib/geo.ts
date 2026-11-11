import * as Location from 'expo-location';
import { KADIKOY } from '@/lib/constants';

export type GeoFix = { lat: number; lng: number; label: string };

export async function reverseLabel(lat: number, lng: number) {
  try {
    const rows = await Location.reverseGeocodeAsync({ latitude: lat, longitude: lng });
    const row = rows[0];
    return row?.district || row?.subregion || row?.city || row?.name || 'Konumun';
  } catch {
    return 'Konumun';
  }
}

export async function detectLocation(): Promise<GeoFix | null> {
  const perm = await Location.requestForegroundPermissionsAsync();
  if (perm.status !== 'granted') {
    return null;
  }
  let pos: Location.LocationObject | null = null;
  try {
    pos = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.High });
  } catch {
    try {
      pos = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
    } catch {
      pos = null;
    }
  }
  if (!pos) {
    return null;
  }
  const lat = pos.coords.latitude;
  const lng = pos.coords.longitude;
  return { lat, lng, label: await reverseLabel(lat, lng) };
}

export function isKadikoy(lat: number, lng: number) {
  return Math.abs(lat - KADIKOY.lat) < 0.004 && Math.abs(lng - KADIKOY.lng) < 0.004;
}
