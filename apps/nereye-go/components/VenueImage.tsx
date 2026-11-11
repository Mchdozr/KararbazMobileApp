import { Image } from 'expo-image';
import { useEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { fallbackPhoto } from '@/lib/dish-photo';
import { useFilters } from '@/lib/filters';
import { photoSource } from '@/lib/photos';
import { colors } from '@/lib/theme';

export function VenueImage({
  placeId,
  photoUrl,
  photoReference,
  name,
  cuisine,
  intent,
  style,
  radius = 18,
}: {
  placeId: string;
  photoUrl?: string | null;
  photoReference?: string | null;
  name?: string | null;
  cuisine?: string | null;
  intent?: string;
  style?: object;
  radius?: number;
}) {
  const filters = useFilters();
  const source = photoSource(placeId, photoUrl, photoReference);
  const [failed, setFailed] = useState(false);
  const fallback = fallbackPhoto(placeId, name, cuisine, intent || filters.intent);

  useEffect(() => {
    setFailed(false);
  }, [placeId, photoUrl, photoReference]);

  return (
    <View style={[styles.wrap, { borderRadius: radius }, style]}>
      <Image source={fallback} style={StyleSheet.absoluteFill} contentFit="cover" cachePolicy="memory-disk" />
      {source && !failed ? (
        <Image
          source={source}
          style={StyleSheet.absoluteFill}
          contentFit="cover"
          transition={220}
          cachePolicy="memory-disk"
          onError={() => setFailed(true)}
        />
      ) : null}
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
});
