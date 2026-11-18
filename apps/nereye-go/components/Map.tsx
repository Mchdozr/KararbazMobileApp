// Native harita. Web'de aynı ad `Map.web.tsx` dosyasına çözülür.
// Döşeme her zaman Google'dan gelir (PROVIDER_GOOGLE); public OSM döşemesi kullanılmaz.
import { MAP_ATTRIBUTION, mapMode, type MapMode } from '@/lib/map-config';
import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import Constants, { ExecutionEnvironment } from 'expo-constants';
import { forwardRef, useImperativeHandle, useRef, type ComponentProps } from 'react';
import { Platform, StyleSheet, Text, View } from 'react-native';
import RNMapView, { PROVIDER_GOOGLE } from 'react-native-maps';

export { Marker, Polyline } from 'react-native-maps';
export type { Region } from 'react-native-maps';

export type MapRef = Pick<RNMapView, 'animateToRegion'>;

type Props = Omit<ComponentProps<typeof RNMapView>, 'provider'> & {
  /** Atıf haritanın içinde mi görünsün? Üstüne alt sayfa binen ekranlar atfı kendi koyar. */
  showAttribution?: boolean;
};

export function currentMapMode(): MapMode {
  const extra = (Constants.expoConfig?.extra ?? {}) as { googleMapsEnabled?: boolean };
  return mapMode(Platform.OS, Boolean(extra.googleMapsEnabled), Constants.executionEnvironment === ExecutionEnvironment.StoreClient);
}

const MapView = forwardRef<MapRef, Props>(function MapView({ style, showAttribution = true, ...rest }, ref) {
  const inner = useRef<RNMapView>(null);
  useImperativeHandle(ref, () => ({
    animateToRegion: (region, duration) => inner.current?.animateToRegion(region, duration),
  }));
  const mode = currentMapMode();
  if (mode !== 'google') {
    return <MapUnavailable style={style} mode={mode} />;
  }
  return (
    <View style={style}>
      <RNMapView ref={inner} {...rest} provider={PROVIDER_GOOGLE} style={StyleSheet.absoluteFill} />
      {showAttribution ? (
        <Text style={styles.attr} pointerEvents="none" numberOfLines={1}>
          {MAP_ATTRIBUTION}
        </Text>
      ) : null}
    </View>
  );
});

function MapUnavailable({ style, mode }: { style: Props['style']; mode: MapMode }) {
  return (
    <View style={[styles.empty, style]} accessibilityRole="text">
      <Ionicons name="map-outline" size={28} color={colors.sage} />
      <Text style={styles.emptyTitle}>{mode === 'no-key' ? 'Harita anahtarı yok' : 'Harita bu cihazda yok'}</Text>
      <Text style={styles.emptyBody}>
        {mode === 'no-key' ? 'Bu derlemeye harita anahtarı verilmedi. Mekan listesi çalışmaya devam eder.' : 'Mekan listesini kullanabilirsin.'}
      </Text>
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
    backgroundColor: 'rgba(255,255,255,0.8)',
    paddingHorizontal: 4,
    borderRadius: 4,
    overflow: 'hidden',
  },
  empty: { backgroundColor: colors.coralSoft, alignItems: 'center', justifyContent: 'center', gap: 6, padding: 20 },
  emptyTitle: { color: colors.espresso, fontFamily: fonts.sansBold, fontSize: 14 },
  emptyBody: { color: colors.espresso, fontFamily: fonts.sans, fontSize: 13, textAlign: 'center' },
});
