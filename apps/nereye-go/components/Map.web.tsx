import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { forwardRef, useImperativeHandle, type ReactNode } from 'react';
import { StyleSheet, Text, View, type StyleProp, type ViewStyle } from 'react-native';

export type MapRef = { animateToRegion: (region: Region, duration?: number) => void };

export type Region = { latitude: number; longitude: number; latitudeDelta: number; longitudeDelta: number };

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

/** Web yedeği: etkileşimli harita yerine sade bir yer tutucu. Liste ve kartlar aynen çalışır. */
const MapView = forwardRef<MapRef, Props>(function MapView({ style }, ref) {
  useImperativeHandle(ref, () => ({ animateToRegion: () => {} }), []);
  return (
    <View style={[styles.wrap, style]}>
      <Ionicons name="map-outline" size={28} color={colors.sage} />
      <Text style={styles.text}>Etkileşimli harita mobil uygulamada</Text>
    </View>
  );
});

export default MapView;
export const Marker = (_: Record<string, unknown>) => null;
export const Polyline = (_: Record<string, unknown>) => null;

const styles = StyleSheet.create({
  wrap: { backgroundColor: colors.coralSoft, alignItems: 'center', justifyContent: 'center', gap: 8 },
  text: { color: colors.espresso, fontFamily: fonts.sansMed, fontSize: 13 },
});
