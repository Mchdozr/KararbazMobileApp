import { colors, fonts, radii } from '@/lib/theme';
import { weatherView, type WeatherInfo } from '@/lib/weather';
import { Ionicons } from '@expo/vector-icons';
import { Pressable, StyleSheet, Text, View } from 'react-native';

/** Hava listeyi etkilediyse kısa açıklama ve (varsa) önerilen niyete geçiş. Sıradan havada görünmez. */
export function WeatherNote({ weather, onSuggest }: { weather: WeatherInfo | null | undefined; onSuggest: (intent: string) => void }) {
  const view = weatherView(weather);
  if (!view) {
    return null;
  }
  return (
    <View style={styles.wrap} accessibilityRole="summary" accessibilityLabel={view.text}>
      <Ionicons name={view.icon} size={22} color={colors.coral} />
      <View style={styles.body}>
        <Text style={styles.text}>{view.text}</Text>
        {view.credit ? <Text style={styles.credit}>{view.credit}</Text> : null}
      </View>
      {view.action ? (
        <Pressable
          onPress={() => onSuggest(view.action!.intent)}
          style={styles.action}
          accessibilityRole="button"
          accessibilityLabel={view.action.label}>
          <Text style={styles.actionText}>{view.action.label}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    marginHorizontal: 20,
    marginTop: 14,
    paddingHorizontal: 14,
    paddingVertical: 10,
    borderRadius: radii.md,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  body: { flex: 1, gap: 2 },
  text: { color: colors.ink, fontFamily: fonts.sans, fontSize: 13, lineHeight: 18 },
  credit: { color: colors.muted, fontFamily: fonts.sans, fontSize: 11, lineHeight: 14 },
  action: { minHeight: 44, justifyContent: 'center', paddingHorizontal: 4 },
  actionText: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 13 },
});
