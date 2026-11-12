import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { useEffect, useState } from 'react';
import { AccessibilityInfo, Pressable, StyleSheet, Text } from 'react-native';
import Animated, { FadeInDown, FadeOutDown } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

type Tone = 'info' | 'success' | 'error';
type Toast = { id: number; message: string; tone: Tone; action?: { label: string; onPress: () => void } };

let current: Toast | null = null;
let seq = 0;
let timer: ReturnType<typeof setTimeout> | null = null;
const listeners = new Set<() => void>();

function emit() {
  listeners.forEach((l) => l());
}

function show(message: string, tone: Tone, action?: Toast['action']) {
  if (timer) {
    clearTimeout(timer);
  }
  current = { id: ++seq, message, tone, action };
  emit();
  AccessibilityInfo.announceForAccessibility?.(message);
  timer = setTimeout(dismiss, action ? 5000 : 2600);
}

function dismiss() {
  if (timer) {
    clearTimeout(timer);
    timer = null;
  }
  current = null;
  emit();
}

/** Kısa, engellemeyen geri bildirim. Ekran bağımsız: her yerden çağrılabilir. */
export const toast = {
  info: (message: string, action?: Toast['action']) => show(message, 'info', action),
  success: (message: string, action?: Toast['action']) => show(message, 'success', action),
  error: (message: string, action?: Toast['action']) => show(message, 'error', action),
  dismiss,
};

const ICON: Record<Tone, 'information-circle' | 'checkmark-circle' | 'alert-circle'> = {
  info: 'information-circle',
  success: 'checkmark-circle',
  error: 'alert-circle',
};

export function ToastHost() {
  const insets = useSafeAreaInsets();
  const [, setTick] = useState(0);

  useEffect(() => {
    const l = () => setTick((n) => n + 1);
    listeners.add(l);
    return () => {
      listeners.delete(l);
    };
  }, []);

  if (!current) {
    return null;
  }
  const t = current;

  return (
    <Animated.View
      key={t.id}
      entering={FadeInDown.duration(180)}
      exiting={FadeOutDown.duration(160)}
      style={[styles.wrap, { bottom: insets.bottom + 84 }]}
      pointerEvents="box-none">
      <Pressable
        style={[styles.card, t.tone === 'error' && styles.error]}
        onPress={dismiss}
        accessibilityRole="alert"
        accessibilityLabel={t.message}>
        <Ionicons name={ICON[t.tone]} size={20} color={t.tone === 'success' ? colors.coralSoft : colors.white} />
        <Text style={styles.text}>{t.message}</Text>
        {t.action ? (
          <Pressable
            onPress={() => {
              t.action?.onPress();
              dismiss();
            }}
            hitSlop={10}
            accessibilityRole="button"
            accessibilityLabel={t.action.label}>
            <Text style={styles.action}>{t.action.label}</Text>
          </Pressable>
        ) : null}
      </Pressable>
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  wrap: { position: 'absolute', left: 16, right: 16, alignItems: 'center', zIndex: 50 },
  card: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    backgroundColor: colors.espresso,
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 18,
    maxWidth: 520,
    shadowColor: '#04382b',
    shadowOpacity: 0.25,
    shadowRadius: 16,
    shadowOffset: { width: 0, height: 8 },
    elevation: 8,
  },
  error: { backgroundColor: colors.danger },
  text: { color: colors.white, fontFamily: fonts.sansMed, fontSize: 14, flexShrink: 1 },
  action: { color: colors.gold, fontFamily: fonts.sansBold, fontSize: 14, marginLeft: 4 },
});
