import { formatDistance } from '@/components/VenueProductCard';
import { geoDistanceMeters, geoKindIcon, geoKindLabel, geoSubtitle, type GeoHit } from '@/lib/geo-hit';
import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { Pressable, StyleSheet, Text, View } from 'react-native';

export function GeoSuggestRow({
  hit,
  onPress,
  distanceMeters,
  last = false,
}: {
  hit: GeoHit;
  onPress: (hit: GeoHit) => void;
  distanceMeters?: number;
  last?: boolean;
}) {
  const kind = geoKindLabel(hit.kind, hit.kindLabel);
  const sub = geoSubtitle(hit);
  return (
    <Pressable
      style={[styles.hit, last ? styles.last : null]}
      onPress={() => onPress(hit)}
      accessibilityRole="button"
      accessibilityLabel={`${hit.label}, ${sub}, ${kind}`}>
      <View style={styles.icon}>
        <Ionicons name={geoKindIcon(hit.kind, kind)} size={20} color={colors.coral} />
      </View>
      <View style={styles.col}>
        <Text style={styles.title}>{hit.label}</Text>
        <Text style={styles.sub} numberOfLines={2}>
          {sub}
        </Text>
      </View>
      <View style={styles.meta}>
        <Text style={styles.kind}>{kind}</Text>
        {distanceMeters != null ? <Text style={styles.dist}>{formatDistance(distanceMeters)}</Text> : null}
      </View>
    </Pressable>
  );
}

export function GeoSuggestGroup({
  title,
  hits,
  from,
  onPress,
}: {
  title: string;
  hits: GeoHit[];
  from?: { lat: number; lng: number };
  onPress: (hit: GeoHit) => void;
}) {
  if (!hits.length) {
    return null;
  }
  return (
    <View style={styles.group}>
      <Text style={styles.groupTitle}>{title}</Text>
      <View style={styles.card}>
        {hits.map((hit, i) => (
          <GeoSuggestRow
            key={`${hit.label}-${hit.subtitle}-${hit.kind}-${hit.latitude}-${hit.longitude}`}
            hit={hit}
            last={i === hits.length - 1}
            distanceMeters={from ? geoDistanceMeters(from.lat, from.lng, hit.latitude, hit.longitude) : undefined}
            onPress={onPress}
          />
        ))}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  group: { marginBottom: 22 },
  groupTitle: {
    fontFamily: fonts.sansBold,
    color: colors.muted,
    fontSize: 12,
    letterSpacing: 1,
    textTransform: 'uppercase',
    marginBottom: 10,
  },
  card: {
    backgroundColor: colors.surface,
    borderRadius: 22,
    borderWidth: 1,
    borderColor: colors.line,
    overflow: 'hidden',
  },
  hit: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 14,
    paddingHorizontal: 16,
    paddingVertical: 16,
    minHeight: 76,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.line,
  },
  last: { borderBottomWidth: 0 },
  icon: {
    width: 44,
    height: 44,
    borderRadius: 14,
    backgroundColor: colors.coralSoft,
    alignItems: 'center',
    justifyContent: 'center',
  },
  col: { flex: 1, minWidth: 0 },
  title: { color: colors.ink, fontFamily: fonts.display, fontSize: 20 },
  sub: { color: colors.muted, marginTop: 4, fontSize: 14, lineHeight: 20 },
  meta: { alignItems: 'flex-end', gap: 6 },
  kind: {
    color: colors.coral,
    fontFamily: fonts.sansBold,
    fontSize: 11,
    letterSpacing: 0.3,
    backgroundColor: colors.coralSoft,
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 999,
    overflow: 'hidden',
  },
  dist: { color: colors.muted, fontSize: 12, fontWeight: '600' },
});
