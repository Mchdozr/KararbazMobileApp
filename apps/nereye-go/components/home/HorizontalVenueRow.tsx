import { VenueProductCard } from '@/components/VenueProductCard';
import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import type { VenueCard } from '@/lib/types';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

export function HorizontalVenueRow({
  title,
  subtitle,
  items,
  onSeeAll,
  intent,
  compact = false,
}: {
  title: string;
  subtitle?: string;
  items: VenueCard[];
  onSeeAll?: () => void;
  intent?: string;
  /** Tüm kartlar aynı boyda; ilk kart öne çıkarılmaz. */
  compact?: boolean;
}) {
  if (items.length === 0) {
    return null;
  }

  return (
    <View style={styles.wrap}>
      <View style={styles.head}>
        <View style={{ flex: 1 }}>
          <Text style={styles.title}>{title}</Text>
          {subtitle ? <Text style={styles.sub}>{subtitle}</Text> : null}
        </View>
        {onSeeAll ? (
          <Pressable
            onPress={onSeeAll}
            hitSlop={6}
            style={styles.allBtn}
            accessibilityRole="button"
            accessibilityLabel={`${title}, tümünü gör`}>
            <Text style={styles.all}>Tümü</Text>
            <Ionicons name="arrow-forward" size={14} color={colors.accent} />
          </Pressable>
        ) : null}
      </View>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.row}>
        {items.slice(0, 8).map((card, i) => (
          <VenueProductCard
            key={card.placeId}
            card={card}
            width={!compact && i === 0 ? 220 : 176}
            featured={!compact && i === 0}
            intent={intent}
            loadPhoto={i < 4}
          />
        ))}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { marginTop: 30 },
  head: {
    paddingHorizontal: 20,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-end',
    marginBottom: 12,
  },
  title: { fontFamily: fonts.display, fontSize: 22, color: colors.ink },
  sub: { color: colors.muted, marginTop: 2, fontSize: 13, fontFamily: fonts.sans },
  allBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    minHeight: 44,
    paddingHorizontal: 12,
    borderRadius: 999,
    backgroundColor: colors.accentSoft,
  },
  all: { color: colors.accent, fontFamily: fonts.sansBold, fontSize: 13 },
  // Kart gölgeleri kesilmesin diye alt boşluk.
  row: { paddingHorizontal: 20, gap: 14, paddingBottom: 12, paddingTop: 2 },
});
