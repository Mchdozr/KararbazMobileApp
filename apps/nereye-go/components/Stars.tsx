import { haptic } from '@/lib/haptics';
import { STAR_LABELS } from '@/lib/reviews';
import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { Pressable, StyleSheet, Text, View } from 'react-native';

export function Stars({ value, size = 14 }: { value: number; size?: number }) {
  const rounded = Math.round(value * 2) / 2;
  return (
    <View style={styles.row} accessibilityRole="image" accessibilityLabel={`${value.toFixed(1)} yıldız`}>
      {[1, 2, 3, 4, 5].map((n) => (
        <Ionicons
          key={n}
          name={rounded >= n ? 'star' : rounded >= n - 0.5 ? 'star-half' : 'star-outline'}
          size={size}
          color={colors.gold}
        />
      ))}
    </View>
  );
}

export function StarInput({ value, onChange }: { value: number; onChange: (stars: number) => void }) {
  return (
    <View style={styles.inputWrap}>
      <View style={styles.inputRow} accessibilityRole="adjustable" accessibilityLabel="Puan" accessibilityValue={{ text: `${value} yıldız` }}>
        {[1, 2, 3, 4, 5].map((n) => (
          <Pressable
            key={n}
            onPress={() => {
              haptic.tap();
              onChange(n);
            }}
            hitSlop={6}
            style={styles.starBtn}
            accessibilityRole="button"
            accessibilityLabel={`${n} yıldız, ${STAR_LABELS[n]}`}
            accessibilityState={{ selected: n === value }}>
            <Ionicons name={n <= value ? 'star' : 'star-outline'} size={38} color={n <= value ? colors.gold : colors.line} />
          </Pressable>
        ))}
      </View>
      <Text style={styles.caption}>{STAR_LABELS[value] || 'Puanını seç'}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', gap: 1 },
  inputWrap: { alignItems: 'center', gap: 6 },
  inputRow: { flexDirection: 'row', gap: 4 },
  starBtn: { width: 48, height: 48, alignItems: 'center', justifyContent: 'center' },
  caption: { fontFamily: fonts.sansBold, color: colors.ink, fontSize: 15 },
});
