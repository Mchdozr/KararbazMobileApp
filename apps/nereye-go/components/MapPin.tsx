import { Marker } from '@/components/Map';
import { colors, fonts, INTENT_ICONS } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { memo, useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

/**
 * Markalı harita pini: niyet ikonlu yeşil yuvarlak. Seçili pin amber, büyük ve adıyla görünür.
 * Özel görünümlü pinlerde `tracksViewChanges` sürekli açık kalırsa harita yavaşlar; ikon yazı tipi
 * çizildikten sonra kapatılır, seçim değişince kısa süre yeniden açılır.
 */
export const VenuePin = memo(function VenuePin({
  latitude,
  longitude,
  intent,
  name,
  selected,
  onPress,
}: {
  latitude: number;
  longitude: number;
  intent?: string;
  name: string;
  selected: boolean;
  onPress: () => void;
}) {
  const [track, setTrack] = useState(true);
  useEffect(() => {
    setTrack(true);
    const t = setTimeout(() => setTrack(false), 700);
    return () => clearTimeout(t);
  }, [selected]);
  const icon = (intent && INTENT_ICONS[intent]) || 'location';
  return (
    <Marker
      coordinate={{ latitude, longitude }}
      onPress={onPress}
      tracksViewChanges={track}
      anchor={{ x: 0.5, y: 1 }}
      zIndex={selected ? 10 : 1}
      accessibilityLabel={name}>
      <View style={styles.wrap} pointerEvents="none">
        {selected ? (
          <Text style={styles.label} numberOfLines={1}>
            {name}
          </Text>
        ) : null}
        <View style={[styles.dot, selected && styles.dotOn]}>
          <Ionicons name={icon} size={selected ? 18 : 14} color={selected ? colors.onAccent : colors.onCoral} />
        </View>
        <View style={[styles.tip, selected && styles.tipOn]} />
      </View>
    </Marker>
  );
});

/** Kullanıcının konumu: nabız halkalı nokta. */
export function MePin({ latitude, longitude }: { latitude: number; longitude: number }) {
  const [track, setTrack] = useState(true);
  useEffect(() => {
    const t = setTimeout(() => setTrack(false), 700);
    return () => clearTimeout(t);
  }, []);
  return (
    <Marker coordinate={{ latitude, longitude }} anchor={{ x: 0.5, y: 0.5 }} tracksViewChanges={track} accessibilityLabel="Arama noktası">
      <View style={styles.meHalo} pointerEvents="none">
        <View style={styles.me} />
      </View>
    </Marker>
  );
}

const styles = StyleSheet.create({
  wrap: { alignItems: 'center' },
  label: {
    maxWidth: 160,
    marginBottom: 4,
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 12,
    overflow: 'hidden',
    backgroundColor: colors.surface,
    color: colors.ink,
    fontFamily: fonts.sansBold,
    fontSize: 12,
  },
  dot: {
    width: 30,
    height: 30,
    borderRadius: 15,
    backgroundColor: colors.coral,
    borderWidth: 2,
    borderColor: colors.white,
    alignItems: 'center',
    justifyContent: 'center',
  },
  dotOn: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.accent },
  tip: {
    width: 0,
    height: 0,
    marginTop: -1,
    borderLeftWidth: 5,
    borderRightWidth: 5,
    borderTopWidth: 7,
    borderLeftColor: 'transparent',
    borderRightColor: 'transparent',
    borderTopColor: colors.coral,
  },
  tipOn: { borderTopColor: colors.accent },
  meHalo: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: 'rgba(11,93,69,0.18)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  me: { width: 14, height: 14, borderRadius: 7, backgroundColor: colors.coral, borderWidth: 3, borderColor: colors.white },
});
