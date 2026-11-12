import { colors, radii } from '@/lib/theme';
import { useEffect } from 'react';
import { StyleSheet, View, type DimensionValue, type StyleProp, type ViewStyle } from 'react-native';
import Animated, {
  Easing,
  useAnimatedStyle,
  useReducedMotion,
  useSharedValue,
  withRepeat,
  withTiming,
} from 'react-native-reanimated';

/** İçerik gelene kadar yerini tutan nabız atan blok. Azaltılmış hareket açıksa sabit durur. */
export function Skeleton({
  width = '100%',
  height = 16,
  radius = 10,
  style,
}: {
  width?: DimensionValue;
  height?: DimensionValue;
  radius?: number;
  style?: StyleProp<ViewStyle>;
}) {
  const reduce = useReducedMotion();
  const pulse = useSharedValue(0.55);

  useEffect(() => {
    if (reduce) {
      return;
    }
    pulse.value = withRepeat(withTiming(1, { duration: 820, easing: Easing.inOut(Easing.quad) }), -1, true);
  }, [pulse, reduce]);

  const animated = useAnimatedStyle(() => ({ opacity: pulse.value }));
  return (
    <Animated.View
      style={[styles.block, { width, height, borderRadius: radius }, animated, style]}
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
    />
  );
}

export function VenueCardSkeleton({ width, photoHeight = 168 }: { width?: DimensionValue; photoHeight?: number }) {
  return (
    <View style={{ width: width ?? '100%' }}>
      <Skeleton height={photoHeight} radius={radii.md} />
      <Skeleton width="55%" height={13} style={{ marginTop: 10 }} />
      <Skeleton width="80%" height={11} style={{ marginTop: 8 }} />
    </View>
  );
}

export function VenueRowSkeleton({ count = 3 }: { count?: number }) {
  return (
    <View style={styles.row} accessibilityLabel="Yükleniyor" accessibilityRole="progressbar">
      {Array.from({ length: count }, (_, i) => (
        <VenueCardSkeleton key={i} width={i === 0 ? 220 : 176} photoHeight={i === 0 ? 210 : 168} />
      ))}
    </View>
  );
}

export function VenueGridSkeleton({ count = 6 }: { count?: number }) {
  return (
    <View style={styles.grid} accessibilityLabel="Yükleniyor" accessibilityRole="progressbar">
      {Array.from({ length: count }, (_, i) => (
        <View key={i} style={styles.cell}>
          <VenueCardSkeleton />
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  block: { backgroundColor: colors.line },
  row: { flexDirection: 'row', gap: 12, paddingHorizontal: 20, overflow: 'hidden' },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 12, paddingHorizontal: 20 },
  cell: { width: '47%' },
});
