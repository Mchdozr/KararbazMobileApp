import { haptic } from '@/lib/haptics';
import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import type { ComponentProps } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, type StyleProp, type ViewStyle } from 'react-native';

type Variant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger';

export function Button({
  label,
  onPress,
  variant = 'primary',
  icon,
  loading = false,
  disabled = false,
  compact = false,
  style,
  accessibilityLabel,
  accessibilityHint,
}: {
  label: string;
  onPress: () => void;
  variant?: Variant;
  icon?: ComponentProps<typeof Ionicons>['name'];
  loading?: boolean;
  disabled?: boolean;
  compact?: boolean;
  style?: StyleProp<ViewStyle>;
  accessibilityLabel?: string;
  accessibilityHint?: string;
}) {
  const off = disabled || loading;
  const fg =
    variant === 'primary'
      ? colors.onCoral
      : variant === 'danger'
        ? colors.onDanger
        : variant === 'secondary'
        ? colors.ink
        : variant === 'ghost'
          ? colors.coral
          : colors.ink;

  return (
    <Pressable
      onPress={() => {
        haptic.tap();
        onPress();
      }}
      disabled={off}
      style={({ pressed }) => [
        styles.base,
        compact && styles.compact,
        styles[variant],
        pressed && styles.pressed,
        off && styles.off,
        style,
      ]}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel ?? label}
      accessibilityHint={accessibilityHint}
      accessibilityState={{ disabled: off, busy: loading }}>
      {loading ? (
        <ActivityIndicator color={fg} />
      ) : (
        <>
          {icon ? <Ionicons name={icon} size={compact ? 16 : 18} color={fg} /> : null}
          <Text style={[styles.text, compact && styles.textCompact, { color: fg }]} numberOfLines={1}>
            {label}
          </Text>
        </>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    minHeight: 52,
    borderRadius: 16,
    paddingHorizontal: 18,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
  },
  compact: { minHeight: 44, borderRadius: 14, paddingHorizontal: 14 },
  primary: { backgroundColor: colors.coral },
  secondary: { backgroundColor: colors.coralSoft },
  outline: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.line },
  ghost: { backgroundColor: 'transparent' },
  danger: { backgroundColor: colors.danger },
  pressed: { opacity: 0.82, transform: [{ scale: 0.985 }] },
  off: { opacity: 0.55 },
  text: { fontFamily: fonts.sansBold, fontSize: 16 },
  textCompact: { fontSize: 14 },
});
