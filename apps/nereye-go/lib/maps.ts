import { Linking } from 'react-native';

export function directionsUrl(lat: number, lng: number, originLat?: number, originLng?: number) {
  let url = `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}`;
  if (originLat != null && originLng != null) {
    url += `&origin=${originLat},${originLng}`;
  }
  return url;
}

export async function openDirections(
  lat: number,
  lng: number,
  originLat?: number,
  originLng?: number,
) {
  await Linking.openURL(directionsUrl(lat, lng, originLat, originLng));
}
