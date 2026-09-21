import { INTENTS, intentLabel } from '@/lib/constants';
import { INTENT_PHOTOS } from '@/lib/photos';
import { colors } from '@/lib/theme';
import { Image } from 'expo-image';
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
            style={styles.item}
            accessibilityRole="button"
            accessibilityLabel={intentLabel(intent)}
            accessibilityState={{ selected: on }}>
            <View style={[styles.avatar, on && styles.avatarOn]}>
              <Image source={INTENT_PHOTOS[intent]} style={StyleSheet.absoluteFill} contentFit="cover" />
            </View>
            <Text style={[styles.label, on && styles.labelOn]}>{intentLabel(intent)}</Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  row: { paddingHorizontal: 20, gap: 16, paddingTop: 8, paddingBottom: 4 },
  item: { alignItems: 'center', width: 76 },
  avatar: {
    width: 68,
    height: 68,
    borderRadius: 34,
    overflow: 'hidden',
    backgroundColor: colors.line,
    borderWidth: 2,
    borderColor: 'transparent',
  },
  avatarOn: { borderColor: colors.coral },
  label: { marginTop: 8, fontSize: 12, fontWeight: '600', color: colors.muted },
  labelOn: { color: colors.ink, fontWeight: '800' },
});
