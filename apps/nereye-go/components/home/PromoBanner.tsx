import { colors, fonts } from '@/lib/theme';
import { Pressable, StyleSheet, Text, View } from 'react-native';

export function PromoBanner({ onOpenNow }: { onOpenNow: () => void }) {
  return (
    <Pressable style={styles.wrap} onPress={onOpenNow} accessibilityRole="button" accessibilityLabel="Açık mekanlar">
      <View style={styles.copy}>
        <Text style={styles.title}>Bu akşam nereye?</Text>
        <Text style={styles.sub}>Konumuna göre açık mekanlar</Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  wrap: {
    marginHorizontal: 20,
    marginTop: 18,
    borderRadius: 22,
    overflow: 'hidden',
    backgroundColor: colors.coral,
    paddingHorizontal: 18,
    paddingVertical: 18,
  },
  copy: { gap: 6 },
  title: { fontFamily: fonts.display, color: colors.white, fontSize: 20, lineHeight: 24 },
  sub: { color: colors.white, fontSize: 13, opacity: 0.92 },
});
