export type TravelMode = 'foot' | 'driving';

export function directionsUrl(
  lat: number,
  lng: number,
  originLat?: number,
  originLng?: number,
  mode: TravelMode = 'driving',
) {
  const travelmode = mode === 'foot' ? 'walking' : 'driving';
  let url = `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}&travelmode=${travelmode}`;
  if (originLat != null && originLng != null) {
    url += `&origin=${originLat},${originLng}`;
  }
  return url;
}

export function appleDirectionsUrl(
  lat: number,
  lng: number,
  originLat?: number,
  originLng?: number,
  mode: TravelMode = 'driving',
) {
  const dirflg = mode === 'foot' ? 'w' : 'd';
  let url = `http://maps.apple.com/?daddr=${lat},${lng}&dirflg=${dirflg}`;
  if (originLat != null && originLng != null) {
    url += `&saddr=${originLat},${originLng}`;
  }
  return url;
}
