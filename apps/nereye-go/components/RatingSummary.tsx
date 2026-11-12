import { Stars } from '@/components/Stars';
import type { RatingSummary as Summary } from '@/lib/reviews';
import { colors, fonts } from '@/lib/theme';
import { StyleSheet, Text, View } from 'react-native';

export function RatingSummary({ summary }: { summary: Summary }) {
  if (!summary.count || summary.average == null) {
    return null;
  }
  const max = Math.max(...summary.buckets, 1);
  return (
    <View
      style={styles.wrap}
      accessible
      accessibilityLabel={`Ortalama ${summary.average.toFixed(1)}, ${summary.count} yorum`}>
      <View style={styles.left}>
        <Text style={styles.avg}>{summary.average.toFixed(1)}</Text>
        <Stars value={summary.average} />
        <Text style={styles.count}>{summary.count} yorum</Text>
      </View>
      <View style={styles.bars}>
        {[5, 4, 3, 2, 1].map((star) => (
          <View key={star} style={styles.barRow}>
            <Text style={styles.barLabel}>{star}</Text>
            <View style={styles.track}>
              <View style={[styles.fill, { width: `${(summary.buckets[star - 1] / max) * 100}%` }]} />
            </View>
          </View>
        ))}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    flexDirection: 'row',
    gap: 18,
    alignItems: 'center',
    backgroundColor: colors.bg,
    borderRadius: 18,
    padding: 16,
    marginTop: 12,
  },
  left: { alignItems: 'center', gap: 4, minWidth: 84 },
  avg: { fontFamily: fonts.display, fontSize: 40, color: colors.ink, lineHeight: 44 },
  count: { color: colors.muted, fontSize: 12, fontFamily: fonts.sans },
  bars: { flex: 1, gap: 5 },
  barRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  barLabel: { width: 10, color: colors.muted, fontSize: 12, fontFamily: fonts.sansMed },
  track: { flex: 1, height: 6, borderRadius: 3, backgroundColor: colors.line, overflow: 'hidden' },
  fill: { height: 6, borderRadius: 3, backgroundColor: colors.gold },
});
