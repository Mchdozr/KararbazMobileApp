import { Image } from 'expo-image';
import { useEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { api } from '@/lib/api';
import { photoSource, servicePhoto } from '@/lib/photos';
import { colors } from '@/lib/theme';

export function VenueImage({
  placeId,
  photoUrl,
  photoReference,
  name,
  intent,
  style,
  radius = 18,
}: {
  placeId: string;
  photoUrl?: string | null;
  photoReference?: string | null;
  name?: string | null;
  intent?: string;
  style?: object;
  radius?: number;
}) {
  const given = photoSource(placeId, photoUrl, photoReference);
  const [lazyUrl, setLazyUrl] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);
  const source = given ?? photoSource(placeId, lazyUrl);
  const fallback = servicePhoto(name, intent, placeId);

  useEffect(() => {
    if (given || !placeId) {
      return;
    }
    let alive = true;
    void api.placePhoto(placeId).then((url) => {
      if (alive) {
        setLazyUrl(url);
      }
    });
    return () => {
      alive = false;
    };
  }, [given, placeId]);

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
