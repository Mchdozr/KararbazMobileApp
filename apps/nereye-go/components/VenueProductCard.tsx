import { FavoriteButton } from '@/components/FavoriteButton';
import { CARD_IMAGE_PT } from '@/lib/media-width';
import { VenueImage } from '@/components/VenueImage';
import { vibeText } from '@/lib/constants';
import { api } from '@/lib/api';
import { metaLine, ratingLine } from '@/lib/venue-format';
import { minutesOfDay, openLine } from '@/lib/open-status';
import { mahalleLine } from '@/lib/geo-hit';
import { colors, fonts, gradients, radii, shadows } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import type { VenueCard } from '@/lib/types';
import { useRouter } from 'expo-router';
import { LinearGradient } from 'expo-linear-gradient';
import { memo } from 'react';
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

const AnimatedView = Animated.createAnimatedComponent(View);

export const VenueProductCard = memo(function VenueProductCard({
  card,
  width,
  featured = false,
  intent,
  loadPhoto = false,
}: {
  card: VenueCard;
  width?: number;
  featured?: boolean;
  intent?: string;
  loadPhoto?: boolean;
}) {
  const router = useRouter();
  const scale = useSharedValue(1);
  const style = useAnimatedStyle(() => ({ transform: [{ scale: scale.value }] }));
  const rated = ratingLine(card);
  const rating = rated?.value ?? null;
  const meta = metaLine(card.category, card.priceLevel);
  const photoH = featured ? 210 : 168;
  const vibes = card.vibes.slice(0, 2);
  const event = card.upcomingEvents[0];
  const clock = eventClock(event?.startsAt);
  const hours = openLine(card, minutesOfDay());

  const a11yLabel = [
    card.name,
    card.category,
    rating ? `puan ${rating}` : null,
    card.distanceMeters >= 0 ? formatDistance(card.distanceMeters) : null,
    hours ? hours.text.toLocaleLowerCase('tr-TR') : null,
  ]
    .filter(Boolean)
    .join(', ');

  return (
    <AnimatedView style={[styles.card, width ? { width } : { width: '100%' }, style]}>
      <Pressable
        onPressIn={() => {
          scale.value = withTiming(0.97, { duration: 180 });
          // Parmak daha kalkmadan detay isteği yola çıksın.
          api.prefetchVenue(card.placeId);
        }}
        onPressOut={() => {
          scale.value = withTiming(1, { duration: 220 });
        }}
        onPress={() => router.push({ pathname: '/venue/[id]', params: { id: card.placeId, name: card.name, photo: card.photoUrl ?? card.photoReference ?? '' } })}
        accessibilityRole="button"
        accessibilityHint="Mekan detayını açar"
        accessibilityLabel={a11yLabel}>
        <View style={[styles.photo, { height: photoH }]}>
          <VenueImage
            placeId={card.placeId}
            photoUrl={card.photoUrl}
            photoReference={card.photoReference}
            name={card.name}
            intent={intent}
            style={StyleSheet.absoluteFill}
            radius={radii.md}
            active={loadPhoto}
            widthPt={CARD_IMAGE_PT}
          />
          <LinearGradient colors={gradients.photo} style={styles.fade} />
          <View style={styles.badges}>
            {card.kind === 'Sponsored' ? <Text style={[styles.pill, styles.boost]}>Öne çıkan</Text> : null}
            {card.distanceMeters >= 0 ? <Text style={styles.pill}>{formatDistance(card.distanceMeters)}</Text> : null}
            {hours ? (
              <Text style={[styles.pill, hours.tone === 'open' ? styles.open : hours.tone === 'soon' ? styles.soon : styles.closed]}>
                {hours.short}
              </Text>
            ) : null}
          </View>
          <Text numberOfLines={2} style={styles.onPhoto}>
            {card.name}
          </Text>
        </View>
        <View style={styles.metaRow}>
          {rated ? (
            <View style={styles.rating}>
              <Ionicons name="star" size={12} color={colors.gold} />
              <Text style={styles.star}>{rated.value}</Text>
              {rated.count ? <Text style={styles.count}>({rated.count})</Text> : null}
            </View>
          ) : (
            <Text style={styles.fresh}>Yeni</Text>
          )}
          {meta ? (
            <Text numberOfLines={1} style={styles.meta}>
              {meta}
            </Text>
          ) : card.address ? (
            <Text numberOfLines={1} style={styles.addr}>
              {mahalleLine(card.address) || card.address}
            </Text>
          ) : null}
        </View>
        {meta && card.address ? (
          <Text numberOfLines={1} style={styles.where}>
            {mahalleLine(card.address) || card.address}
          </Text>
        ) : null}
        {hours && (card.closesAt || card.opensAt) ? (
          <View style={styles.hoursRow}>
            <View style={[styles.hoursDot, { backgroundColor: hours.tone === 'open' ? colors.sage : hours.tone === 'soon' ? colors.gold : colors.muted }]} />
            <Text numberOfLines={1} style={[styles.hoursText, hours.tone === 'soon' ? styles.hoursSoon : null]}>
              {hours.text}
            </Text>
          </View>
        ) : null}
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
      </Pressable>
      <FavoriteButton venue={card} style={styles.heart} />
    </AnimatedView>
  );
});

const styles = StyleSheet.create({
  // Beyaz kart: fotoğraf üstte, bilgiler altta; zeminden hafif gölgeyle ayrılır.
  card: {
    marginBottom: 4,
    position: 'relative',
    backgroundColor: colors.surface,
    borderRadius: radii.md + 4,
    padding: 6,
    paddingBottom: 12,
    ...shadows.card,
  },
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
  boost: { backgroundColor: colors.accent, color: colors.onAccent },
  badges: {
    position: 'absolute',
    top: 10,
    left: 10,
    right: 52,
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 6,
  },
  heart: { position: 'absolute', top: 14, right: 14, zIndex: 2 },
  pill: {
    color: colors.white,
    backgroundColor: colors.glass,
    fontSize: 11,
    fontWeight: '700',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: radii.pill,
    overflow: 'hidden',
  },
  open: { backgroundColor: colors.sage },
  soon: { backgroundColor: colors.gold },
  closed: { backgroundColor: colors.scrim },
  hoursRow: { flexDirection: 'row', alignItems: 'center', gap: 6, marginTop: 4, paddingHorizontal: 6 },
  hoursDot: { width: 7, height: 7, borderRadius: 4 },
  hoursText: { flex: 1, color: colors.muted, fontFamily: fonts.sansMed, fontSize: 12 },
  hoursSoon: { color: colors.ink },
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
  metaRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 10, paddingHorizontal: 6 },
  rating: { flexDirection: 'row', alignItems: 'center', gap: 3 },
  star: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 13 },
  count: { color: colors.muted, fontSize: 12 },
  fresh: { color: colors.sage, fontFamily: fonts.sansBold, fontSize: 12 },
  addr: { flex: 1, color: colors.muted, fontSize: 12 },
  meta: { flex: 1, color: colors.ink, fontFamily: fonts.sansMed, fontSize: 12 },
  where: { color: colors.muted, fontSize: 12, marginTop: 3, paddingHorizontal: 6 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 8, paddingHorizontal: 6 },
  chip: {
    backgroundColor: colors.surfaceAlt,
    color: colors.ink,
    fontSize: 11,
    fontWeight: '700',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: radii.pill,
    overflow: 'hidden',
  },
  note: { color: colors.muted, fontSize: 13, lineHeight: 18, marginTop: 6, paddingHorizontal: 6 },
});
