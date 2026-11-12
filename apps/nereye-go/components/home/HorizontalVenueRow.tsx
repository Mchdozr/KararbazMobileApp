import { VenueProductCard } from '@/components/VenueProductCard';
import { colors, fonts } from '@/lib/theme';
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
          <Pressable onPress={onSeeAll} hitSlop={12} accessibilityRole="button" accessibilityLabel={`${title}, tümünü gör`}>
            <Text style={styles.all}>Tümü</Text>
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
          />
        ))}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { marginTop: 26 },
  head: {
    paddingHorizontal: 20,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-end',
    marginBottom: 12,
  },
  title: { fontFamily: fonts.display, fontSize: 24, color: colors.ink },
  sub: { color: colors.muted, marginTop: 2, fontSize: 13 },
  all: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 14, paddingVertical: 6 },
  row: { paddingHorizontal: 20, gap: 12, paddingBottom: 4 },
});
