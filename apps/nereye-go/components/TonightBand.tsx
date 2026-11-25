import { VenueImage } from '@/components/VenueImage';
import { track } from '@/lib/analytics';
import { distanceReason } from '@/lib/decide';
import { colors, fonts, radii, shadows } from '@/lib/theme';
import { eventTimeLabel } from '@/lib/tonight';
import type { TonightEvent } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

/** Ana sayfa: yakındaki bu akşamın etkinlikleri. Etkinlik yoksa hiç görünmez. */
export function TonightBand({ events }: { events: readonly TonightEvent[] }) {
  const router = useRouter();
  if (events.length === 0) {
    return null;
  }
  return (
    <View style={styles.wrap}>
      <View style={styles.head}>
        <Ionicons name="musical-notes" size={18} color={colors.coral} />
        <Text style={styles.title}>Bu akşam yakında</Text>
      </View>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.row}>
        {events.map((e) => {
          const when = eventTimeLabel(e.startsAt, e.endsAt, e.live);
          const far = distanceReason(e.distanceMeters)?.text;
          return (
            <Pressable
              key={e.id}
              style={styles.card}
              onPress={() => {
                track('venue_view', { placeId: e.placeId });
                router.push({ pathname: '/venue/[id]', params: { id: e.placeId, name: e.venueName, photo: e.photoUrl ?? '' } });
              }}
              accessibilityRole="button"
              accessibilityLabel={`${e.title}, ${e.venueName}, ${when}${far ? `, ${far}` : ''}`}>
              <View style={styles.photo}>
                <VenueImage placeId={e.placeId} photoUrl={e.photoUrl} name={e.venueName} style={StyleSheet.absoluteFill} radius={0} widthPt={220} />
                <View style={[styles.when, e.live && styles.live]}>
                  <Text style={[styles.whenText, e.live && styles.liveText]}>{when}</Text>
                </View>
              </View>
              <View style={styles.body}>
                <Text style={styles.eventTitle} numberOfLines={1}>
                  {e.title}
                </Text>
                <Text style={styles.venue} numberOfLines={1}>
                  {e.venueName}
                  {far ? ` · ${far}` : ''}
                </Text>
              </View>
            </Pressable>
          );
        })}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { marginTop: 22 },
  head: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 20, marginBottom: 10 },
  title: { color: colors.ink, fontFamily: fonts.display, fontSize: 20 },
  row: { paddingHorizontal: 20, gap: 12 },
  card: { width: 220, borderRadius: radii.md, backgroundColor: colors.surface, overflow: 'hidden', ...shadows.card },
  photo: { height: 110, backgroundColor: colors.line },
  when: {
    position: 'absolute',
    left: 10,
    top: 10,
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: radii.pill,
    backgroundColor: colors.coral,
  },
  live: { backgroundColor: colors.accent },
  liveText: { color: colors.onAccent },
  whenText: { color: colors.onCoral, fontFamily: fonts.sansBold, fontSize: 12 },
  body: { padding: 12, gap: 2 },
  eventTitle: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 15 },
  venue: { color: colors.muted, fontFamily: fonts.sans, fontSize: 12 },
});
