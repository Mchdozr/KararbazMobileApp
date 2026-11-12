import { Image } from 'expo-image';
import { memo, useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { api } from '@/lib/api';
import { photoSource } from '@/lib/photos';
import { placeholderInitials, placeholderPalette, placeholderShapes } from '@/lib/placeholder';
import { colors, fonts } from '@/lib/theme';

export const VenueImage = memo(function VenueImage({
  placeId,
  photoUrl,
  photoReference,
  name,
  style,
  radius = 18,
  fallback,
  active = false,
}: {
  placeId: string;
  photoUrl?: string | null;
  photoReference?: string | null;
  name?: string | null;
  style?: object;
  radius?: number;
  fallback?: number;
  active?: boolean;
}) {
  const given = photoSource(placeId, photoUrl, photoReference);
  const [lazyUrl, setLazyUrl] = useState<string | null>(null);
  // Görsel indirilemezse (kota, silinmiş dosya) boş kare yerine yer tutucu göster.
  const [broken, setBroken] = useState<string | null>(null);
  const candidate = given ?? photoSource(placeId, lazyUrl);
  const uri = candidate && typeof candidate === 'object' ? candidate.uri : null;
  const source = uri && uri === broken ? null : candidate;
  const [a, b] = placeholderPalette(placeId);
  const shapes = placeholderShapes(placeId);

  useEffect(() => {
    if (given || !placeId || !active) {
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
  }, [given, placeId, active]);

  return (
    <View style={[styles.wrap, { borderRadius: radius }, style]}>
      {source ? (
        <Image
          source={source}
          style={StyleSheet.absoluteFill}
          contentFit="cover"
          transition={180}
          cachePolicy="memory-disk"
          recyclingKey={uri ?? placeId}
          onError={() => setBroken(uri)}
        />
      ) : fallback ? (
        <Image source={fallback} style={StyleSheet.absoluteFill} contentFit="cover" />
      ) : (
        <View style={[styles.ph, { backgroundColor: a }]}>
          {shapes.map((s, i) => (
            <View
              key={`${placeId}-${i}`}
              style={[
                styles.blob,
                {
                  top: s.top,
                  left: s.left,
                  width: s.size,
                  height: s.size,
                  opacity: s.opacity,
                  backgroundColor: i === 1 ? colors.white : b,
                },
              ]}
            />
          ))}
          <Text style={styles.initials}>{placeholderInitials(name, placeId)}</Text>
        </View>
      )}
    </View>
  );
});

const styles = StyleSheet.create({
  wrap: {
    overflow: 'hidden',
    backgroundColor: colors.line,
    width: '100%',
    height: '100%',
  },
  ph: { flex: 1, alignItems: 'center', justifyContent: 'center' },
  blob: { position: 'absolute', borderRadius: 999 },
  initials: {
    fontFamily: fonts.display,
    fontSize: 28,
    color: colors.espresso,
    letterSpacing: 1,
  },
});
