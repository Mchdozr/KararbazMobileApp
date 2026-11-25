import { INTENTS, intentLabel } from '@/lib/constants';
import { INTENT_PHOTOS } from '@/lib/photos';
import { colors, fonts } from '@/lib/theme';
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
            accessibilityState={{ selected: on }} aria-selected={on}>
            <View style={[styles.ring, on && styles.ringOn]}>
              <View style={styles.avatar}>
                <Image source={INTENT_PHOTOS[intent]} style={StyleSheet.absoluteFill} contentFit="cover" />
              </View>
            </View>
            <Text style={[styles.label, on && styles.labelOn]}>{intentLabel(intent)}</Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  row: { paddingHorizontal: 20, gap: 14, paddingTop: 18, paddingBottom: 4 },
  item: { alignItems: 'center', width: 76 },
  // Seçili niyet: fotoğrafla halka arasında ince bir boşluk (hikâye halkası gibi).
  ring: {
    width: 72,
    height: 72,
    borderRadius: 36,
    padding: 3,
    borderWidth: 2,
    borderColor: 'transparent',
  },
  ringOn: { borderColor: colors.accent },
  avatar: {
    flex: 1,
    borderRadius: 32,
    overflow: 'hidden',
    backgroundColor: colors.line,
  },
  label: { marginTop: 6, fontSize: 12, fontFamily: fonts.sansMed, color: colors.muted },
  labelOn: { color: colors.ink, fontFamily: fonts.sansBold },
});
