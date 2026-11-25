import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { Pressable, StyleSheet, Text, View } from 'react-native';

export function ErrorBanner({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <View style={styles.wrap} accessibilityRole="alert">
      <Ionicons name="cloud-offline-outline" size={20} color={colors.danger} />
      <Text style={styles.text}>{message}</Text>
      {onRetry ? (
        <Pressable onPress={onRetry} style={styles.retry} accessibilityRole="button" accessibilityLabel="Tekrar dene" hitSlop={8}>
          <Text style={styles.retryText}>Tekrar dene</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    marginHorizontal: 20,
    marginTop: 12,
    backgroundColor: colors.dangerSoft,
    borderRadius: 16,
    paddingVertical: 12,
    paddingHorizontal: 14,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  text: { flex: 1, color: colors.ink, fontFamily: fonts.sansMed, fontSize: 14, lineHeight: 19 },
  retry: { minHeight: 36, justifyContent: 'center', paddingHorizontal: 4 },
  retryText: { color: colors.danger, fontFamily: fonts.sansBold, fontSize: 14 },
});
