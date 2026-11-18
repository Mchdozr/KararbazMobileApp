import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { forwardRef, useState, type ComponentProps } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View, type TextInputInstance, type TextInputProps } from 'react-native';

type Props = TextInputProps & {
  label: string;
  error?: string | null;
  hint?: string;
  icon?: ComponentProps<typeof Ionicons>['name'];
  /** Şifre alanı: göster/gizle düğmesi ekler. */
  secret?: boolean;
  counter?: number;
};

export const TextField = forwardRef<TextInputInstance, Props>(function TextField(
  { label, error, hint, icon, secret = false, counter, style, onFocus, onBlur, multiline, value, ...rest },
  ref,
) {
  const [focused, setFocused] = useState(false);
  const [hidden, setHidden] = useState(true);
  const length = typeof value === 'string' ? value.length : 0;

  return (
    <View style={styles.wrap}>
      <Text style={styles.label}>{label}</Text>
      <View style={[styles.box, multiline && styles.boxMulti, focused && styles.focus, error ? styles.bad : null]}>
        {icon ? <Ionicons name={icon} size={18} color={error ? colors.danger : colors.muted} style={multiline ? styles.iconTop : null} /> : null}
        <TextInput
          ref={ref}
          value={value}
          style={[styles.input, multiline && styles.inputMulti, style]}
          placeholderTextColor={colors.muted}
          secureTextEntry={secret && hidden}
          multiline={multiline}
          textAlignVertical={multiline ? 'top' : 'center'}
          accessibilityLabel={label}
          onFocus={(e) => {
            setFocused(true);
            onFocus?.(e);
          }}
          onBlur={(e) => {
            setFocused(false);
            onBlur?.(e);
          }}
          {...rest}
        />
        {secret ? (
          <Pressable
            onPress={() => setHidden((h) => !h)}
            hitSlop={12}
            accessibilityRole="button"
            accessibilityLabel={hidden ? 'Şifreyi göster' : 'Şifreyi gizle'}>
            <Ionicons name={hidden ? 'eye-outline' : 'eye-off-outline'} size={20} color={colors.muted} />
          </Pressable>
        ) : null}
      </View>
      <View style={styles.foot}>
        {error ? (
          <Text style={styles.error} accessibilityLiveRegion="polite">
            {error}
          </Text>
        ) : hint ? (
          <Text style={styles.hint}>{hint}</Text>
        ) : (
          <View />
        )}
        {counter ? (
          <Text style={[styles.hint, length > counter && styles.error]}>
            {length}/{counter}
          </Text>
        ) : null}
      </View>
    </View>
  );
});

const styles = StyleSheet.create({
  wrap: { gap: 6 },
  label: { fontFamily: fonts.sansBold, fontSize: 13, color: colors.ink },
  box: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    minHeight: 52,
    borderRadius: 14,
    borderWidth: 1.5,
    borderColor: colors.line,
    // Beyaz kart içinde alan zeminden ayrışsın.
    backgroundColor: colors.bg,
    paddingHorizontal: 14,
  },
  boxMulti: { alignItems: 'flex-start', paddingVertical: 12 },
  iconTop: { marginTop: 2 },
  focus: { borderColor: colors.coral, backgroundColor: colors.surface },
  bad: { borderColor: colors.danger },
  input: { flex: 1, color: colors.ink, fontFamily: fonts.sans, fontSize: 16, paddingVertical: 12 },
  inputMulti: { minHeight: 96, paddingVertical: 0 },
  foot: { flexDirection: 'row', justifyContent: 'space-between', gap: 12, minHeight: 16 },
  hint: { color: colors.muted, fontSize: 12, fontFamily: fonts.sans },
  error: { color: colors.danger, fontSize: 12, fontFamily: fonts.sansMed, flexShrink: 1 },
});
