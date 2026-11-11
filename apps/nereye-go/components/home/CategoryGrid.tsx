import { INTENTS, intentLabel } from '@/lib/constants';
import { colors } from '@/lib/theme';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

export function CategoryGrid({
  selected,
  onSelect,
}: {
  selected: string;
  onSelect: (intent: string) => void;
}) {
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.row}>
      {INTENTS.map((intent) => {
        const on = selected === intent;
        return (
          <Pressable
            key={intent}
            onPress={() => onSelect(intent)}
            style={[styles.chip, on && styles.chipOn]}
            accessibilityRole="button"
            accessibilityLabel={intentLabel(intent)}
            accessibilityState={{ selected: on }}>
            <View style={[styles.dot, on && styles.dotOn]} />
            <Text style={[styles.label, on && styles.labelOn]}>{intentLabel(intent)}</Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  row: { paddingHorizontal: 20, gap: 10, paddingTop: 8, paddingBottom: 4 },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 14,
    paddingVertical: 12,
    borderRadius: 16,
    backgroundColor: colors.espresso,
    borderWidth: 1,
    borderColor: colors.line,
  },
  chipOn: { borderColor: colors.coral, backgroundColor: colors.coralSoft },
  dot: { width: 8, height: 8, borderRadius: 99, backgroundColor: colors.sage },
  dotOn: { backgroundColor: colors.coral },
  label: { fontSize: 13, fontWeight: '600', color: colors.ink },
  labelOn: { color: colors.ink, fontWeight: '800' },
});
