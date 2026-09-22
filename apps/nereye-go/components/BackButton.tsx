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
      style={[styles.btn, light ? styles.light : styles.dark]}>
      <Ionicons name="chevron-back" size={22} color={light ? colors.white : colors.ink} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  btn: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },
  dark: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.line },
  light: { backgroundColor: 'rgba(6,78,59,)0.45)' },
});
