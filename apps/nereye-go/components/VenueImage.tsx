import { Image } from 'expo-image';
import { StyleSheet, View } from 'react-native';
import { photoSource } from '@/lib/photos';
import { colors, INTENT_ICONS, INTENT_PASTELS } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';

export function VenueImage({
  placeId,
  photoUrl,
  photoReference,
  intent,
  style,
  radius = 18,
}: {
  placeId: string;
  photoUrl?: string | null;
  photoReference?: string | null;
  intent?: string;
  style?: object;
  radius?: number;
}) {
  const source = photoSource(placeId, photoUrl, photoReference);
  const icon = INTENT_ICONS[intent ?? 'Kahve'] ?? 'cafe';
  const bg = INTENT_PASTELS[intent ?? 'Kahve'] ?? colors.coralSoft;
  return (
    <View style={[styles.wrap, { borderRadius: radius }, style]}>
      {source ? (
        <Image source={source} style={StyleSheet.absoluteFill} contentFit="cover" transition={220} cachePolicy="memory-disk" />
      ) : (
        <View style={[styles.ph, { backgroundColor: bg }]}>
          <Ionicons name={icon} size={28} color={colors.coral} />
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    overflow: 'hidden',
    backgroundColor: colors.line,
    width: '100%',
    height: '100%',
  },
  ph: { flex: 1, alignItems: 'center', justifyContent: 'center' },
});
