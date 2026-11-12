import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { useEffect, useRef, useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/** Tam ekran fotoğraf galerisi. `index` null ise kapalıdır. */
export function PhotoViewer({
  uris,
  index,
  onClose,
}: {
  uris: string[];
  index: number | null;
  onClose: () => void;
}) {
  const { width, height } = useWindowDimensions();
  const insets = useSafeAreaInsets();
  const ref = useRef<ScrollView>(null);
  const [at, setAt] = useState(0);
  const open = index != null && uris.length > 0;

  useEffect(() => {
    if (index != null) {
      setAt(index);
      requestAnimationFrame(() => ref.current?.scrollTo({ x: index * width, animated: false }));
    }
  }, [index, width]);

  return (
    <Modal visible={open} animationType="fade" transparent statusBarTranslucent onRequestClose={onClose}>
      <View style={styles.bg}>
        <ScrollView
          ref={ref}
          horizontal
          pagingEnabled
          showsHorizontalScrollIndicator={false}
          onMomentumScrollEnd={(e) => setAt(Math.round(e.nativeEvent.contentOffset.x / Math.max(width, 1)))}>
          {uris.map((uri) => (
            <Pressable key={uri} onPress={onClose} style={{ width, height }} accessibilityLabel="Fotoğraf, kapatmak için dokun">
              <Image source={{ uri }} style={{ width, height }} contentFit="contain" transition={150} />
            </Pressable>
          ))}
        </ScrollView>
        <View style={[styles.top, { paddingTop: insets.top + 8 }]} pointerEvents="box-none">
          <Text style={styles.count}>
            {at + 1} / {uris.length}
          </Text>
          <Pressable style={styles.close} onPress={onClose} hitSlop={10} accessibilityRole="button" accessibilityLabel="Kapat">
            <Ionicons name="close" size={22} color={colors.white} />
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: 'rgba(0,0,0,0.95)' },
  top: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    paddingHorizontal: 16,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  count: { color: colors.white, fontFamily: fonts.sansBold, fontSize: 14 },
  close: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: 'rgba(255,255,255,0.16)',
    alignItems: 'center',
    justifyContent: 'center',
  },
});
