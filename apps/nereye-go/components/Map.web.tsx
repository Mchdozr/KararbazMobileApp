// Web önizlemesi: gerçek döşeme yok (public OSM döşemesi kullanılmaz). Pinler bölgeye göre gerçek
// koordinatlarından yerleştirilir; böylece ekranın düzeni ve pinler web'de de görülebilir.
import type { MapMode } from '@/lib/map-config';
import { colors, fonts } from '@/lib/theme';
import { createContext, forwardRef, useContext, useImperativeHandle, useState, type ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View, type StyleProp, type ViewStyle } from 'react-native';

export type Region = { latitude: number; longitude: number; latitudeDelta: number; longitudeDelta: number };

export type MapRef = { animateToRegion: (region: Region, duration?: number) => void };

type Props = {
  style?: StyleProp<ViewStyle>;
  children?: ReactNode;
  initialRegion?: Region;
  onRegionChangeComplete?: (region: Region) => void;
  onPress?: () => void;
  pointerEvents?: 'none' | 'auto' | 'box-none' | 'box-only';
  showAttribution?: boolean;
  mapPadding?: { top: number; right: number; bottom: number; left: number };
};

type Frame = { region: Region; width: number; height: number; pad: { top: number; bottom: number } };
const FrameContext = createContext<Frame | null>(null);

const MapView = forwardRef<MapRef, Props>(function MapView({ style, children, initialRegion, onPress, mapPadding }, ref) {
  const [region, setRegion] = useState<Region>(
    initialRegion ?? { latitude: 41.0, longitude: 29.0, latitudeDelta: 0.05, longitudeDelta: 0.05 },
  );
  const [size, setSize] = useState({ width: 0, height: 0 });
  useImperativeHandle(ref, () => ({ animateToRegion: (r) => setRegion(r) }), []);
  const pad = { top: mapPadding?.top ?? 0, bottom: mapPadding?.bottom ?? 0 };
  return (
    <Pressable
      style={[styles.wrap, style]}
      onPress={onPress}
      accessible={false}
      onLayout={(e) => setSize({ width: e.nativeEvent.layout.width, height: e.nativeEvent.layout.height })}>
      {/* Sokak dokusu: hafif ızgara ve iki "cadde". */}
      {Array.from({ length: 14 }).map((_, i) => (
        <View key={`h${i}`} style={[styles.gridH, { top: `${(i + 1) * 7}%` }]} />
      ))}
      {Array.from({ length: 8 }).map((_, i) => (
        <View key={`v${i}`} style={[styles.gridV, { left: `${(i + 1) * 12}%` }]} />
      ))}
      <View style={styles.roadA} />
      <View style={styles.roadB} />
      <View style={styles.water} />
      <Text style={styles.note}>Web önizlemesi · telefonda gerçek harita</Text>
      <FrameContext.Provider value={{ region, ...size, pad }}>{children}</FrameContext.Provider>
    </Pressable>
  );
});

export default MapView;

/** Native sürümle aynı dışa aktarım: ekranlar platformdan bağımsız çağırabilsin. */
export function currentMapMode(): MapMode {
  return 'web';
}

/** Bölge içindeki koordinatı ekran noktasına çevirir (üst/alt dolgu dışındaki alana). */
export function Marker({
  coordinate,
  children,
  onPress,
  anchor,
  accessibilityLabel,
}: {
  coordinate: { latitude: number; longitude: number };
  children?: ReactNode;
  onPress?: () => void;
  anchor?: { x: number; y: number };
  accessibilityLabel?: string;
  [key: string]: unknown;
}) {
  const frame = useContext(FrameContext);
  if (!frame || !frame.width) {
    return null;
  }
  const { region, width, height, pad } = frame;
  const usable = Math.max(1, height - pad.top - pad.bottom);
  const x = ((coordinate.longitude - region.longitude) / region.longitudeDelta + 0.5) * width;
  const y = pad.top + (0.5 - (coordinate.latitude - region.latitude) / region.latitudeDelta) * usable;
  if (x < 0 || x > width || y < pad.top - 40 || y > height) {
    return null;
  }
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      style={[styles.marker, { left: x, top: y, transform: [{ translateX: '-50%' }, { translateY: anchor?.y === 1 ? '-100%' : '-50%' }] }]}>
      {children}
    </Pressable>
  );
}

export const Polyline = (_: Record<string, unknown>) => null;

const styles = StyleSheet.create({
  wrap: { backgroundColor: '#e8efe6', overflow: 'hidden' },
  gridH: { position: 'absolute', left: 0, right: 0, height: 1, backgroundColor: 'rgba(16,41,31,0.06)' },
  gridV: { position: 'absolute', top: 0, bottom: 0, width: 1, backgroundColor: 'rgba(16,41,31,0.06)' },
  roadA: { position: 'absolute', left: -40, right: -40, top: '38%', height: 10, backgroundColor: '#ffffff', transform: [{ rotate: '-12deg' }] },
  roadB: { position: 'absolute', top: -40, bottom: -40, left: '58%', width: 8, backgroundColor: '#ffffff', transform: [{ rotate: '18deg' }] },
  water: { position: 'absolute', left: -60, bottom: -80, width: 260, height: 260, borderRadius: 130, backgroundColor: '#cfe3ea' },
  note: {
    position: 'absolute',
    alignSelf: 'center',
    top: '46%',
    color: colors.muted,
    fontFamily: fonts.sansMed,
    fontSize: 12,
    opacity: 0.7,
  },
  marker: { position: 'absolute' },
});
