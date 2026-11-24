import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { memo, useEffect, useState } from 'react';
import { PixelRatio, StyleSheet, useWindowDimensions, View } from 'react-native';
import { api } from '@/lib/api';
import { dropCover } from '@/lib/cover-store';
import { rememberCover, useCover } from '@/lib/covers';
import { variantWidth } from '@/lib/media-width';
import { photoSource } from '@/lib/photos';
import { placeholderPalette } from '@/lib/placeholder';
import { colors, INTENT_ICONS } from '@/lib/theme';

/**
 * Mekan görseli. Öncelik: listenin verdiği kare → cihazda kayıtlı kapak (detayda ya da daha önce görülen
 * gerçek kare) → sunucudan toplu istek. Hiçbiri yoksa niyet ikonlu yer tutucu. Başka bir mekanın stok
 * fotoğrafı asla gösterilmez: kullanıcı yanlış mekanı görmüş olur.
 */
export const VenueImage = memo(function VenueImage({
  placeId,
  photoUrl,
  photoReference,
  name,
  style,
  radius = 18,
  intent,
  active = true,
  onFail,
  widthPt,
}: {
  placeId: string;
  photoUrl?: string | null;
  photoReference?: string | null;
  name?: string | null;
  style?: object;
  radius?: number;
  /** Yer tutucudaki ikon. */
  intent?: string;
  /** false: ekran dışında, henüz sunucuya sorma. */
  active?: boolean;
  /** Görsel indirilemediğinde, denenmiş URI ile çağrılır. */
  onFail?: (uri: string) => void;
  /** Gösterilen genişlik (pt). Verilmezse ekran genişliği. Yerel görselin uygun boyutlu sürümü istenir. */
  widthPt?: number;
}) {
  const screen = useWindowDimensions();
  const widthPx = variantWidth(widthPt ?? screen.width, PixelRatio.get());
  const given = photoUrl || photoReference || null;
  const cover = useCover(placeId);
  const [lazyUrl, setLazyUrl] = useState<string | null>(null);
  // Görsel indirilemezse (kota, silinmiş dosya) boş kare yerine yer tutucu göster.
  const [broken, setBroken] = useState<string | null>(null);
  const raw = given ?? cover ?? lazyUrl;
  const candidate = photoSource(placeId, raw, null, widthPx);
  const uri = candidate && typeof candidate === 'object' ? candidate.uri : null;
  const source = uri && uri === broken ? null : candidate;
  const [a, b] = placeholderPalette(placeId);

  useEffect(() => {
    if (given || cover || !placeId || !active || photoSource(placeId, null)) {
      return;
    }
    let alive = true;
    void api.placePhoto(placeId).then((url) => {
      if (url) {
        rememberCover(placeId, url);
      }
      if (alive) {
        setLazyUrl(url);
      }
    });
    return () => {
      alive = false;
    };
  }, [given, cover, placeId, active]);

  const icon = (intent && INTENT_ICONS[intent]) || 'location';

  return (
    <View style={[styles.wrap, { borderRadius: radius }, style]}>
      {/* Yer tutucu her zaman altta: görsel gelince üstüne yumuşakça biner, gelmezse bu kalır. */}
      <LinearGradient colors={[a, b]} start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }} style={styles.ph}>
        {/* Mekan adı kartta zaten yazılı; burada yalnızca türünü anlatan ikon. */}
        <View style={styles.badge} accessibilityLabel={name ? `${name}, fotoğraf yok` : undefined}>
          <Ionicons name={icon} size={24} color={colors.coral} />
        </View>
      </LinearGradient>
      {source ? (
        <Image
          source={source}
          style={StyleSheet.absoluteFill}
          contentFit="cover"
          transition={220}
          cachePolicy="memory-disk"
          priority={active ? 'high' : 'low'}
          recyclingKey={uri ?? placeId}
          onError={() => {
            setBroken(uri);
            if (uri) {
              if (cover && raw === cover) {
                dropCover(placeId, cover);
              }
              // Çağıran (detay galerisi) görselleri boyutsuz adresle tanır: sürüm eki olmadan bildir.
              const plain = photoSource(placeId, raw);
              onFail?.(plain && typeof plain === 'object' ? plain.uri : uri);
            }
          }}
        />
      ) : null}
    </View>
  );
});

const styles = StyleSheet.create({
  wrap: {
    overflow: 'hidden',
    backgroundColor: colors.surfaceAlt,
    width: '100%',
    height: '100%',
  },
  // İkon üst-orta: alt kısımda kartın adı ve karartma perdesi durur.
  ph: { position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, alignItems: 'center', justifyContent: 'center', paddingBottom: '18%' },
  badge: {
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: 'rgba(255,255,255,0.82)',
    alignItems: 'center',
    justifyContent: 'center',
  },
});
