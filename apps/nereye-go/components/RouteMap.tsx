import { colors } from '@/lib/theme';
import MapView, { Marker, Polyline, UrlTile } from 'react-native-maps';
import { StyleSheet } from 'react-native';

export function RouteMap({
  mode,
  walk,
  drive,
  venueLat,
  venueLng,
  originLat,
  originLng,
}: {
  mode: 'foot' | 'driving';
  walk: { points: { latitude: number; longitude: number }[] } | null;
  drive: { points: { latitude: number; longitude: number }[] } | null;
  venueLat: number;
  venueLng: number;
  originLat: number;
  originLng: number;
}) {
  const route = mode === 'foot' ? walk : drive;
  if (!route?.points.length) {
    return null;
  }
  return (
    <MapView
      style={styles.map}
      pointerEvents="none"
      initialRegion={{
        latitude: venueLat,
        longitude: venueLng,
        latitudeDelta: 0.02,
        longitudeDelta: 0.02,
      }}>
      <UrlTile urlTemplate="https://tile.openstreetmap.org/{z}/{x}/{y}.png" maximumZ={19} />
      <Polyline coordinates={route.points} strokeColor={colors.coral} strokeWidth={4} />
      <Marker coordinate={{ latitude: originLat, longitude: originLng }} pinColor={colors.coral} />
      <Marker coordinate={{ latitude: venueLat, longitude: venueLng }} pinColor={colors.espresso} />
    </MapView>
  );
}

const styles = StyleSheet.create({
  map: { height: 180, borderRadius: 16, marginTop: 12, overflow: 'hidden' },
});
