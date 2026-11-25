// Native harita. Web'de aynı ad `Map.web.tsx` dosyasına çözülür.
// Android: Google haritası. iOS: Apple Haritalar (anahtarsız, Expo Go dahil); iOS anahtarı varsa Google.
// Public OSM döşemesi hiçbir durumda kullanılmaz.
import { mapAttribution, mapMode, type MapMode } from '@/lib/map-config';
import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import Constants, { ExecutionEnvironment } from 'expo-constants';
import { forwardRef, useImperativeHandle, useRef, type ComponentProps } from 'react';
import { Platform, StyleSheet, Text, View } from 'react-native';
import RNMapView, { PROVIDER_DEFAULT, PROVIDER_GOOGLE } from 'react-native-maps';

export { Marker, Polyline } from 'react-native-maps';
export type { Region } from 'react-native-maps';

export type MapRef = Pick<RNMapView, 'animateToRegion'>;

type Props = Omit<ComponentProps<typeof RNMapView>, 'provider'> & {
  /** Atıf haritanın içinde mi görünsün? Üstüne alt sayfa binen ekranlar atfı kendi koyar. */
  showAttribution?: boolean;
};

export function currentMapMode(): MapMode {
  const extra = (Constants.expoConfig?.extra ?? {}) as { googleMapsEnabled?: boolean; googleMapsIosEnabled?: boolean };
  return mapMode(
    Platform.OS,
    Boolean(extra.googleMapsEnabled),
    Constants.executionEnvironment === ExecutionEnvironment.StoreClient,
    Boolean(extra.googleMapsIosEnabled),
  );
}

const MapView = forwardRef<MapRef, Props>(function MapView({ style, showAttribution = true, ...rest }, ref) {
  const inner = useRef<RNMapView>(null);
  useImperativeHandle(ref, () => ({
    animateToRegion: (region, duration) => inner.current?.animateToRegion(region, duration),
  }));
  const mode = currentMapMode();
  if (mode === 'no-key') {
    return <MapUnavailable style={style} />;
  }
  return (
    <View style={style}>
      <RNMapView
        ref={inner}
        {...rest}
        provider={mode === 'google' ? PROVIDER_GOOGLE : PROVIDER_DEFAULT}
        style={StyleSheet.absoluteFill}
      />
      {showAttribution ? (
        <Text style={styles.attr} pointerEvents="none" numberOfLines={1}>
          {mapAttribution(mode)}
        </Text>
      ) : null}
    </View>
  );
});

/** Yalnızca anahtarsız Android önizleme/geliştirme derlemesi (production bu durumda derlenmez). */
function MapUnavailable({ style }: { style: Props['style'] }) {
  return (
    <View style={[styles.empty, style]} accessibilityRole="text">
      <Ionicons name="map-outline" size={28} color={colors.sage} />
      <Text style={styles.emptyTitle}>Harita bu derlemede kapalı</Text>
      <Text style={styles.emptyBody}>Derlemeye harita anahtarı verilmedi. Mekanlar aşağıda listeleniyor.</Text>
    </View>
  );
}

export default MapView;

const styles = StyleSheet.create({
  attr: {
    position: 'absolute',
    right: 6,
    bottom: 4,
    maxWidth: '70%',
    fontSize: 10,
    color: colors.ink,
    backgroundColor: colors.surface,
    paddingHorizontal: 4,
    borderRadius: 4,
    overflow: 'hidden',
  },
  empty: { backgroundColor: colors.coralSoft, alignItems: 'center', justifyContent: 'center', gap: 6, padding: 20 },
  emptyTitle: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 14 },
  emptyBody: { color: colors.ink, fontFamily: fonts.sans, fontSize: 13, textAlign: 'center' },
});
