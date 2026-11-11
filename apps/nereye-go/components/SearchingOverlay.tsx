import { noteFor } from '@/lib/copy';
import { colors, fonts, INTENT_ICONS } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

export function SearchingOverlay({
  visible,
  intent,
  query,
}: {
  visible: boolean;
  intent: string;
  query?: string;
}) {
  const [tick, setTick] = useState(0);

  useEffect(() => {
    if (!visible) {
      return;
    }
    setTick(0);
    const id = setInterval(() => setTick((n) => n + 1), 1700);
    return () => clearInterval(id);
  }, [visible, intent, query]);

  if (!visible) {
    return null;
  }

  return (
    <View pointerEvents="none" style={styles.wrap}>
      <View style={styles.card}>
        <View style={styles.icon}>
          <Ionicons name={INTENT_ICONS[intent] ?? 'cafe'} size={18} color={colors.coral} />
        </View>
        <Text style={styles.text}>{query?.trim() ? `“${query.trim()}” aranıyor…` : noteFor(intent, tick)}</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 88,
    alignItems: 'center',
  },
  card: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    backgroundColor: colors.espresso,
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 22,
    maxWidth: '88%',
    shadowColor: '#000000',
    shadowOpacity: 0.22,
    shadowRadius: 18,
    shadowOffset: { width: 0, height: 8 },
    elevation: 8,
  },
  icon: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: 'rgba(255,251,246,0.12)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  text: { color: colors.white, fontFamily: fonts.display, fontSize: 16, flexShrink: 1 },
});
