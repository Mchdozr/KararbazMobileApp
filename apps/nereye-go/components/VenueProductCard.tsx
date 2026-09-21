import { VenueImage } from '@/components/VenueImage';
import { vibeText } from '@/lib/constants';
import { colors, fonts, radii } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { useRouter } from 'expo-router';
import { LinearGradient } from 'expo-linear-gradient';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import Animated, { useAnimatedStyle, useSharedValue, withTiming } from 'react-native-reanimated';

export function formatDistance(meters: number) {
  return meters >= 1000 ? `${(meters / 1000).toFixed(1)} km` : `${Math.round(meters)} m`;
}

function eventClock(iso?: string) {
  if (!iso) {
    return null;
  }
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) {
    return null;
  }
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
}

const AnimatedPressable = Animated.createAnimatedComponent(Pressable);

export function VenueProductCard({
  card,
  width,
  featured = false,
  intent,
}: {
  card: VenueCard;
  width?: number;
  featured?: boolean;
  intent?: string;
}) {
  const router = useRouter();
  const scale = useSharedValue(1);
  const style = useAnimatedStyle(() => ({ transform: [{ scale: scale.value }] }));
  const rating = card.nereyeRating != null ? card.nereyeRating.toFixed(1) : null;
  const photoH = featured ? 210 : 168;
  const vibes = card.vibes.slice(0, 2);
  const event = card.upcomingEvents[0];
  const clock = eventClock(event?.startsAt);

  return (
    <AnimatedPressable
      onPressIn={() => {
        scale.value = withTiming(0.97, { duration: 180 });
      }}
      onPressOut={() => {
        scale.value = withTiming(1, { duration: 220 });
      }}
      onPress={() => router.push({ pathname: '/venue/[id]', params: { id: card.placeId } })}
      style={[styles.card, width ? { width } : { width: '100%' }, style]}
      accessibilityRole="button"
      accessibilityLabel={`${card.name}${rating ? `, puan ${rating}` : ''}`}>
      <View style={[styles.photo, { height: photoH }]}>
        <VenueImage
          placeId={card.placeId}
          photoUrl={card.photoUrl}
          photoReference={card.photoReference}
          intent={intent}
          style={StyleSheet.absoluteFill}
          radius={radii.md}
        />
        <LinearGradient colors={['transparent', 'rgba(28,20,16,0.72)']} style={styles.fade} />
        {card.kind === 'Sponsored' ? <Text style={styles.boost}>Öne çıkan</Text> : null}
        <View style={styles.badges}>
          <Text style={styles.pill}>{formatDistance(card.distanceMeters)}</Text>
          {card.openNow === true ? <Text style={[styles.pill, styles.open]}>Açık</Text> : null}
          {card.openNow === false ? <Text style={[styles.pill, styles.closed]}>Kapalı</Text> : null}
        </View>
        <Text numberOfLines={2} style={styles.onPhoto}>
          {card.name}
        </Text>
      </View>
      <View style={styles.metaRow}>
        <Text style={styles.star}>{rating ? `★ ${rating}` : 'Yeni'}</Text>
        {card.address ? (
          <Text numberOfLines={1} style={styles.addr}>
            {card.address}
          </Text>
        ) : null}
      </View>
      {vibes.length > 0 || clock ? (
        <View style={styles.chips}>
          {vibes.map((v) => (
            <Text key={v} style={styles.chip}>
              {vibeText(v)}
            </Text>
          ))}
          {clock ? <Text style={styles.chip}>{clock}</Text> : null}
        </View>
      ) : null}
      {card.note ? (
        <Text numberOfLines={1} style={styles.note}>
          {card.note}
        </Text>
      ) : null}
    </AnimatedPressable>
  );
}

const styles = StyleSheet.create({
  card: { marginBottom: 4 },
  photo: {
    borderRadius: radii.md,
    overflow: 'hidden',
    backgroundColor: colors.line,
  },
  fade: {
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
    height: 88,
  },
  boost: {
    position: 'absolute',
    top: 10,
    left: 10,
    backgroundColor: colors.coral,
    color: colors.white,
    fontSize: 11,
    fontWeight: '700',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: radii.pill,
    overflow: 'hidden',
  },
  badges: {
    position: 'absolute',
    top: 10,
    right: 10,
    flexDirection: 'row',
    gap: 6,
  },
  pill: {
    color: colors.white,
    backgroundColor: 'rgba(28,20,16,0.45)',
    fontSize: 11,
    fontWeight: '700',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: radii.pill,
    overflow: 'hidden',
  },
  open: { backgroundColor: colors.sage },
  closed: { backgroundColor: colors.coral },
  onPhoto: {
    position: 'absolute',
    left: 12,
    right: 12,
    bottom: 12,
    color: colors.white,
    fontFamily: fonts.display,
    fontSize: 18,
    fontWeight: '700',
  },
  metaRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 8, paddingHorizontal: 2 },
  star: { color: colors.gold, fontWeight: '800', fontSize: 13 },
  addr: { flex: 1, color: colors.muted, fontSize: 12 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 6, paddingHorizontal: 2 },
  chip: {
    backgroundColor: colors.surface,
    color: colors.ink,
    fontSize: 11,
    fontWeight: '700',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: radii.pill,
    overflow: 'hidden',
    borderWidth: 1,
    borderColor: colors.line,
  },
  note: { color: colors.ink, fontSize: 13, lineHeight: 18, marginTop: 6, paddingHorizontal: 2, opacity: 0.86 },
});
