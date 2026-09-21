import { colors, fonts } from '@/lib/theme';
import { Pressable, StyleSheet, Text, View } from 'react-native';

export function ErrorBanner({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <View style={styles.wrap}>
      <Text style={styles.text}>{message}</Text>
      {onRetry ? (
        <Pressable onPress={onRetry} accessibilityRole="button" accessibilityLabel="Tekrar dene" hitSlop={8}>
          <Text style={styles.retry}>Tekrar dene</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    marginHorizontal: 16,
    marginTop: 12,
    backgroundColor: colors.coralSoft,
    borderRadius: 16,
    padding: 12,
    gap: 6,
  },
  text: { color: colors.espresso, fontFamily: fonts.sans },
  retry: { color: colors.coral, fontWeight: '800' },
});
