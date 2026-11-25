import { toggleFavorite, useFavorites } from '@/lib/favorites';
import { useSession } from '@/lib/session';
import { colors } from '@/lib/theme';
import { toast } from '@/lib/toast';
import type { VenueCard } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { Pressable, StyleSheet, type StyleProp, type ViewStyle } from 'react-native';
import Animated, { useAnimatedStyle, useSharedValue, withSequence, withSpring } from 'react-native-reanimated';

export function FavoriteButton({
  venue,
  size = 18,
  style,
  plain = false,
}: {
  venue: Pick<VenueCard, 'placeId' | 'name'> & Partial<VenueCard>;
  size?: number;
  style?: StyleProp<ViewStyle>;
  /** Fotoğraf üstü cam zemin yerine düz ikon. */
  plain?: boolean;
}) {
  const { session } = useSession();
  const router = useRouter();
  const favorites = useFavorites();
  const saved = favorites.has(venue.placeId);
  const scale = useSharedValue(1);
  const pop = useAnimatedStyle(() => ({ transform: [{ scale: scale.value }] }));

  function press() {
    if (!session) {
      toast.info('Kaydetmek için giriş yap', { label: 'Giriş', onPress: () => router.push('/auth') });
      return;
    }
    scale.value = withSequence(withSpring(1.28, { damping: 8 }), withSpring(1));
    void toggleFavorite(venue);
  }

  return (
    <Pressable
      onPress={press}
      hitSlop={10}
      style={[plain ? styles.plain : styles.glass, style]}
      accessibilityRole="button"
      accessibilityLabel={saved ? `${venue.name} kayıttan çıkar` : `${venue.name} kaydet`}
      accessibilityState={{ selected: saved }} aria-selected={saved}>
      <Animated.View style={pop}>
        <Ionicons
          name={saved ? 'heart' : 'heart-outline'}
          size={size}
          color={plain ? (saved ? colors.danger : colors.ink) : colors.white}
        />
      </Animated.View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  glass: {
    width: 34,
    height: 34,
    borderRadius: 17,
    backgroundColor: colors.glass,
    alignItems: 'center',
    justifyContent: 'center',
  },
  plain: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center' },
});
