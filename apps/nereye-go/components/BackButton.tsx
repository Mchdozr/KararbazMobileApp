import { colors } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { Pressable, StyleSheet } from 'react-native';

export function BackButton({ light = false }: { light?: boolean }) {
  const router = useRouter();
  return (
    <Pressable
      onPress={() => (router.canGoBack() ? router.back() : router.replace('/(tabs)'))}
      hitSlop={12}
      style={({ pressed }) => [styles.btn, light ? styles.light : styles.dark, pressed && styles.pressed]}
      accessibilityRole="button"
      accessibilityLabel="Geri">
      <Ionicons name="chevron-back" size={22} color={light ? colors.white : colors.ink} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  btn: { width: 44, height: 44, borderRadius: 22, alignItems: 'center', justifyContent: 'center' },
  dark: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.line },
  light: { backgroundColor: colors.glass, borderWidth: 1, borderColor: colors.glassLine },
  pressed: { opacity: 0.7 },
});
