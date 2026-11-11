import { Linking, Platform } from 'react-native';
import { appleDirectionsUrl, directionsUrl, type TravelMode } from '@/lib/maps-url';

export async function openDirections(
  lat: number,
  lng: number,
  originLat?: number,
  originLng?: number,
  mode: TravelMode = 'driving',
) {
  const url = Platform.OS === 'ios'
    ? appleDirectionsUrl(lat, lng, originLat, originLng, mode)
    : directionsUrl(lat, lng, originLat, originLng, mode);
  await Linking.openURL(url);
}
