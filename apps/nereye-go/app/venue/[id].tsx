import { Avatar } from '@/components/Avatar';
import { Button } from '@/components/Button';
import { EmptyState } from '@/components/EmptyState';
import { FavoriteButton } from '@/components/FavoriteButton';
import { PhotoViewer } from '@/components/PhotoViewer';
import { RatingSummary } from '@/components/RatingSummary';
import { Skeleton } from '@/components/Skeleton';
import { Stars } from '@/components/Stars';
import { VenueImage } from '@/components/VenueImage';
import { api, mediaUrl } from '@/lib/api';
import { vibeText } from '@/lib/constants';
import { friendlyError } from '@/lib/empty';
import { useFilters } from '@/lib/filters';
import { haptic } from '@/lib/haptics';
import { parseOpeningHours } from '@/lib/hours';
import { openDirections, placeUrl } from '@/lib/maps';
import { photoSource } from '@/lib/photos';
import { rememberVenue } from '@/lib/recent-venues';
import { relativeDate, summarizeRatings } from '@/lib/reviews';
import { formatRouteLine } from '@/lib/route';
import { useSession } from '@/lib/session';
import { shareVenue } from '@/lib/share';
import { colors, fonts, radii } from '@/lib/theme';
import { toast } from '@/lib/toast';
import { peekVenue } from '@/lib/venue-cache';
import { displayPhotos, uniqueStrings } from '@/lib/venue-photos';
import { clip, compactCount, featureIcon, metaLine, priceWord } from '@/lib/venue-format';
import type { ExternalReview, ReviewItem, RouteMode, RouteResult, VenueDetail } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { Stack, useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useCallback, useEffect, useMemo, useState, type ComponentProps } from 'react';
import {
  Alert,
  Linking,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  useWindowDimensions,
  View,
} from 'react-native';
import MapView, { Marker, Polyline } from '@/components/Map';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

const HERO = 320;

/** Tam ekran görüntüleyici. Yorum fotoğrafıysa reviewId ve sunucu yolları (bildirim için) taşır. */
type ViewerState = { uris: string[]; index: number; reviewId?: string; paths?: string[] };

function Action({
  icon,
  label,
  onPress,
}: {
  icon: ComponentProps<typeof Ionicons>['name'];
  label: string;
  onPress: () => void;
}) {
  return (
    <Pressable
      style={({ pressed }) => [styles.action, pressed && { opacity: 0.7 }]}
      onPress={() => {
        haptic.tap();
        onPress();
      }}
      accessibilityRole="button"
      accessibilityLabel={label}>
      <View style={styles.actionIcon}>
        <Ionicons name={icon} size={20} color={colors.coral} />
      </View>
      <Text style={styles.actionText} numberOfLines={1}>
        {label}
      </Text>
    </Pressable>
  );
}

async function open(url: string) {
  try {
    await Linking.openURL(url);
  } catch {
    toast.error('Bağlantı açılamadı.');
  }
}

export default function Venue() {
  const params = useLocalSearchParams<{ id: string; name?: string; photo?: string }>();
  const id = (Array.isArray(params.id) ? params.id[0] : params.id) || '';
  const hintedName = Array.isArray(params.name) ? params.name[0] : params.name;
  // Kartta görünen (ve cihazda önbellekte olan) fotoğraf: galeri indirilemezse yedek.
  const hintedPhoto = (Array.isArray(params.photo) ? params.photo[0] : params.photo) || null;
  const filters = useFilters();
  const { session } = useSession();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const [detail, setDetail] = useState<VenueDetail | null>(null);
  const [reviews, setReviews] = useState<ReviewItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [attempt, setAttempt] = useState(0);
  const [photoIx, setPhotoIx] = useState(0);
  const [viewer, setViewer] = useState<ViewerState | null>(null);
  const [walk, setWalk] = useState<RouteResult | null>(null);
  const [drive, setDrive] = useState<RouteResult | null>(null);
  const [mode, setMode] = useState<RouteMode>('foot');
  const [hoursOpen, setHoursOpen] = useState(false);
  const [allFeatures, setAllFeatures] = useState(false);
  // İndirilemeyen kareler (kota, silinmiş dosya): listeden düşer, yerine sıradaki gelir.
  const [failedPhotos, setFailedPhotos] = useState<ReadonlySet<string>>(() => new Set());
  const markFailed = useCallback((uri: string) => {
    setFailedPhotos((prev) => (prev.has(uri) ? prev : new Set(prev).add(uri)));
  }, []);
  const owner = Boolean(session?.roles?.includes('VenueOwner'));
  const userId = session?.userId ?? null;
  const { lat, lng } = filters;

  useEffect(() => {
    if (!id) {
      return;
    }
    let alive = true;
    // Önbellekte varsa (kart dokunulurken çekildi ya da daha önce açıldı) anında göster, arkada tazele.
    const cached = peekVenue(id);
    setFailedPhotos(new Set());
    setPhotoIx(0);
    setDetail(cached);
    setLoading(!cached);
    setError(null);
    api
      .venue(id)
      .then((v) => {
        if (!alive) {
          return;
        }
        setDetail(v);
        rememberVenue({
          placeId: v.placeId,
          name: v.name,
          address: v.address,
          photoUrl: v.photoUrl,
          nereyeRating: v.nereyeRating,
          latitude: v.latitude,
          longitude: v.longitude,
        });
      })
      .catch((e) => {
        // Elde eski veri varsa sayfayı hata ekranına çevirme.
        if (alive && !cached) {
          setError(friendlyError(e));
        }
      })
      .finally(() => {
        if (alive) {
          setLoading(false);
        }
      });
    return () => {
      alive = false;
    };
  }, [id, userId, attempt]);

  // Rota ayrı yüklenir: sayfayı bekletmez, konum değişince mekanı yeniden indirmez.
  const destLat = detail?.latitude;
  const destLng = detail?.longitude;
  useEffect(() => {
    if (destLat == null || destLng == null) {
      return;
    }
    let alive = true;
    void Promise.all([api.route(lat, lng, destLat, destLng, 'foot'), api.route(lat, lng, destLat, destLng, 'driving')]).then(
      ([foot, driving]) => {
        if (alive) {
          setWalk(foot ?? null);
          setDrive(driving ?? null);
        }
      },
    );
    return () => {
      alive = false;
    };
  }, [destLat, destLng, lat, lng]);

  // Yorum ekranından dönünce liste tazelensin.
  useFocusEffect(
    useCallback(() => {
      if (!id) {
        return;
      }
      let alive = true;
      api
        .reviews(id)
        .then((r) => {
          if (alive) {
            setReviews(r ?? []);
          }
        })
        .catch(() => {});
      return () => {
        alive = false;
      };
    }, [id]),
  );

  const summary = useMemo(() => summarizeRatings(reviews), [reviews]);
  const mine = useMemo(() => (userId ? reviews.find((r) => r.userId === userId) ?? null : null), [reviews, userId]);
  const others = useMemo(() => reviews.filter((r) => r !== mine), [reviews, mine]);

  function writeReview() {
    if (!session) {
      toast.info('Yorum yazmak için giriş yap', { label: 'Giriş', onPress: () => router.push('/auth') });
      return;
    }
    router.push({ pathname: '/review/[id]', params: { id: id! } });
  }

  function report(reviewId?: string, authorId?: string, authorName?: string) {
    if (!session) {
      toast.info('Bildirmek için giriş yap', { label: 'Giriş', onPress: () => router.push('/auth') });
      return;
    }
    const send = (reason: string) => {
      api
        .report(id!, reason, reviewId)
        .then(() => toast.success('Bildirimin alındı. Teşekkürler.'))
        .catch((e) => toast.error(friendlyError(e)));
    };
    const buttons: { text: string; style?: 'cancel' | 'destructive'; onPress?: () => void }[] = [
      { text: 'Vazgeç', style: 'cancel' },
      { text: 'Yanlış bilgi', onPress: () => send('yanlis-bilgi') },
      { text: 'Uygunsuz içerik', onPress: () => send('uygunsuz') },
    ];
    if (authorId && authorId !== userId) {
      buttons.push({ text: 'Kullanıcıyı engelle', style: 'destructive', onPress: () => block(authorId, authorName) });
    }
    Alert.alert(reviewId ? 'Yorumu bildir' : 'Mekanı bildir', 'Neden bildiriyorsun?', buttons);
  }

  function block(authorId: string, authorName?: string) {
    Alert.alert(
      'Kullanıcıyı engelle',
      `${authorName || 'Bu kullanıcı'} yazdıklarını artık görmeyeceksin. Engeli Hesap ve güvenlik ekranından kaldırabilirsin.`,
      [
        { text: 'Vazgeç', style: 'cancel' },
        {
          text: 'Engelle',
          style: 'destructive',
          onPress: () => {
            const before = reviews;
            setReviews(reviews.filter((r) => r.userId !== authorId));
            api
              .block(authorId)
              .then(() => toast.success('Kullanıcı engellendi'))
              .catch((e) => {
                setReviews(before);
                toast.error(friendlyError(e));
              });
          },
        },
      ],
    );
  }

  function reportPhoto(path: string) {
    if (!session) {
      toast.info('Bildirmek için giriş yap', { label: 'Giriş', onPress: () => router.push('/auth') });
      return;
    }
    Alert.alert('Fotoğrafı bildir', 'Bu fotoğraf uygunsuz mu?', [
      { text: 'Vazgeç', style: 'cancel' },
      {
        text: 'Bildir',
        style: 'destructive',
        onPress: () => {
          api
            .report(id!, 'uygunsuz-fotograf', viewer?.reviewId, path)
            .then(() => toast.success('Bildirimin alındı. Teşekkürler.'))
            .catch((e) => toast.error(friendlyError(e)));
        },
      },
    ]);
  }


  function removeMine() {
    if (!mine) {
      return;
    }
    Alert.alert('Yorumu sil', 'Yorumun ve puanın kaldırılacak.', [
      { text: 'Vazgeç', style: 'cancel' },
      {
        text: 'Sil',
        style: 'destructive',
        onPress: () => {
          const before = reviews;
          setReviews(reviews.filter((r) => r.id !== mine.id));
          api
            .deleteReview(mine.id)
            .then(() => toast.success('Yorumun silindi'))
            .catch((e) => {
              setReviews(before);
              toast.error(friendlyError(e));
            });
        },
      },
    ]);
  }

  if (!detail) {
    return (
      <View style={styles.bg}>
        <Stack.Screen options={{ headerRight: undefined }} />
        {loading ? (
          <View accessibilityLabel="Mekan yükleniyor" accessibilityRole="progressbar">
            <Skeleton height={180} radius={0} />
            <View style={styles.skel}>
              <Text style={styles.h}>{hintedName || 'Mekan'}</Text>
              <Skeleton width="45%" height={14} />
              <Skeleton height={64} radius={16} style={{ marginTop: 12 }} />
            </View>
          </View>
        ) : (
          <View style={[styles.center, { paddingTop: insets.top + 80 }]}>
            <EmptyState
              icon={error ? 'cloud-offline-outline' : 'help-circle-outline'}
              title={error ?? 'Mekan bulunamadı.'}
              action={error ? 'Tekrar dene' : 'Geri dön'}
              onAction={() => (error ? setAttempt((n) => n + 1) : router.back())}
            />
          </View>
        )}
      </View>
    );
  }

  // Tekilleştirme tek yerde ve photoSource'un ürettiği URI üzerinden: kapak, şerit ve tam ekran aynı listeyi kullanır.
  const resolvePhoto = (raw: string) => {
    const src = photoSource(detail.placeId, raw);
    return src && typeof src === 'object' ? src.uri : null;
  };
  const own = displayPhotos([...detail.photoUrls, detail.photoUrl], resolvePhoto, failedPhotos);
  // Galerinin tamamı indirilemezse kapak boş kalmasın: kartta görünen (önbellekteki) kareye düş.
  const photos = own.length ? own : displayPhotos([hintedPhoto], resolvePhoto, failedPhotos);
  const photoUris = photos.map((p) => p.uri);
  const week = parseOpeningHours(detail.openingHours);
  const today = week[(new Date().getDay() + 6) % 7];
  const mapLink = detail.mapsUri || placeUrl(detail.latitude, detail.longitude);
  const route = mode === 'foot' ? walk : drive;
  const slides: (string | null)[] = photos.length ? photos.map((p) => p.raw) : [null];
  // Bir kare listeden düşünce gösterge son karenin ötesinde kalmasın.
  const shownIx = Math.min(photoIx, slides.length - 1);
  const features = detail.amenityChips ?? [];
  const external = detail.externalReviews ?? [];

  return (
    <View style={styles.bg}>
      <StatusBar style="dark" />
      <Stack.Screen
        options={{
          headerRight: () => (
            <View style={styles.headRight}>
              <Pressable
                style={styles.glass}
                onPress={() => void shareVenue(detail.placeId, detail.name, detail.address)}
                hitSlop={8}
                accessibilityRole="button"
                accessibilityLabel="Paylaş">
                <Ionicons name="share-outline" size={19} color={colors.white} />
              </Pressable>
              <FavoriteButton venue={detail} size={20} style={styles.glass} />
            </View>
          ),
        }}
      />
      <ScrollView contentContainerStyle={{ paddingBottom: insets.bottom + 96 }} showsVerticalScrollIndicator={false}>
        <View style={[styles.hero, { width }]}>
          <ScrollView
            horizontal
            pagingEnabled
            showsHorizontalScrollIndicator={false}
            onMomentumScrollEnd={(e) => setPhotoIx(Math.round(e.nativeEvent.contentOffset.x / Math.max(width, 1)))}
            style={{ width, height: HERO }}>
            {slides.map((url, i) => (
              <Pressable
                key={url ?? `ph-${i}`}
                style={{ width, height: HERO }}
                disabled={!photoUris.length}
                onPress={() => setViewer({ uris: photoUris, index: Math.min(i, photoUris.length - 1) })}
                accessibilityRole="imagebutton"
                accessibilityLabel={`${detail.name} fotoğrafı ${i + 1}, büyüt`}>
                {Math.abs(i - shownIx) <= 1 ? (
                  <VenueImage
                    placeId={detail.placeId}
                    photoUrl={url}
                    name={detail.name}
                    style={StyleSheet.absoluteFill}
                    radius={0}
                    active
                    onFail={markFailed}
                  />
                ) : null}
                <LinearGradient
                  colors={['rgba(4,56,43,0.45)', 'transparent', 'rgba(4,56,43,0.5)']}
                  locations={[0, 0.35, 1]}
                  style={StyleSheet.absoluteFill}
                />
              </Pressable>
            ))}
          </ScrollView>
          {slides.length > 1 ? (
            <View style={styles.dots} pointerEvents="none">
              {slides.map((_, i) => (
                <View key={`dot-${i}`} style={[styles.dot, i === shownIx ? styles.dotOn : null]} />
              ))}
              <Text style={styles.count}>
                {shownIx + 1} / {slides.length}
              </Text>
            </View>
          ) : null}
        </View>

        <View style={styles.body}>
          <Text style={styles.h} accessibilityRole="header">
            {detail.name}
          </Text>
          {metaLine(detail.category, detail.priceLevel) ? (
            <Text
              style={styles.kind}
              accessibilityLabel={[detail.category, priceWord(detail.priceLevel)].filter(Boolean).join(', ')}>
              {metaLine(detail.category, detail.priceLevel)}
            </Text>
          ) : null}
          {detail.address ? <Text style={styles.addr}>{detail.address}</Text> : null}

          <View style={styles.statusRow}>
            {detail.openNow != null ? (
              <View style={[styles.status, detail.openNow ? styles.open : styles.closed]}>
                <View style={[styles.statusDot, { backgroundColor: detail.openNow ? colors.sage : colors.danger }]} />
                <Text style={[styles.statusText, { color: detail.openNow ? colors.sage : colors.danger }]}>
                  {detail.openNow ? 'Şu an açık' : 'Şu an kapalı'}
                </Text>
              </View>
            ) : null}
            {detail.nereyeRating != null ? (
              <View style={styles.ratingInline}>
                <Ionicons name="star" size={14} color={colors.gold} />
                <Text style={styles.ratingText}>{detail.nereyeRating.toFixed(1)}</Text>
                <Text style={styles.ratingCount}>({detail.nereyeRatingCount})</Text>
              </View>
            ) : null}
            {detail.googleRating != null ? (
              <View
                style={styles.ratingInline}
                accessible
                accessibilityLabel={`Google puanı ${detail.googleRating.toFixed(1)}, ${detail.googleRatingCount ?? 0} değerlendirme`}>
                <Ionicons name="logo-google" size={13} color={colors.muted} />
                <Text style={styles.ratingText}>{detail.googleRating.toFixed(1).replace('.', ',')}</Text>
                {detail.googleRatingCount ? <Text style={styles.ratingCount}>({compactCount(detail.googleRatingCount)})</Text> : null}
              </View>
            ) : null}
            {detail.nereyeRating == null && detail.googleRating == null ? <Text style={styles.fresh}>Henüz puanlanmadı</Text> : null}
            {detail.claimed ? (
              <View style={styles.verified}>
                <Ionicons name="checkmark-circle" size={14} color={colors.sage} />
                <Text style={styles.verifiedText}>İşletme onaylı</Text>
              </View>
            ) : null}
          </View>

          <View style={styles.actions}>
            <Action
              icon="navigate"
              label="Yol tarifi"
              onPress={() => void openDirections(detail.latitude, detail.longitude, lat, lng).catch(() => toast.error('Harita açılamadı.'))}
            />
            {detail.phone ? <Action icon="call" label="Ara" onPress={() => void open(`tel:${detail.phone!.replace(/\s/g, '')}`)} /> : null}
            {detail.website ? (
              <Action
                icon="globe-outline"
                label="Web"
                onPress={() => void open(/^https?:\/\//i.test(detail.website!) ? detail.website! : `https://${detail.website}`)}
              />
            ) : null}
            <Action icon="map-outline" label="Harita" onPress={() => void open(mapLink)} />
            <Action
              icon="share-social-outline"
              label="Paylaş"
              onPress={() => void shareVenue(detail.placeId, detail.name, detail.address)}
            />
          </View>

          {detail.note ? <Text style={styles.note}>{detail.note}</Text> : null}
          {detail.ownerDescription && detail.ownerDescription !== detail.note ? (
            <Text style={styles.desc}>{detail.ownerDescription}</Text>
          ) : null}

          {photoUris.length > 1 ? (
            <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.gallery}>
              {photoUris.map((uri, i) => (
                <Pressable
                  key={uri}
                  onPress={() => setViewer({ uris: photoUris, index: i })}
                  accessibilityRole="imagebutton"
                  accessibilityLabel={`Fotoğraf ${i + 1} / ${photoUris.length}, büyüt`}>
                  <Image
                    source={{ uri }}
                    style={styles.galleryPhoto}
                    contentFit="cover"
                    transition={150}
                    cachePolicy="memory-disk"
                    onError={() => markFailed(uri)}
                  />
                </Pressable>
              ))}
            </ScrollView>
          ) : null}

          {detail.vibes.length ? (
            <View style={styles.pills}>
              {detail.vibes.map((v) => (
                <Text key={`v-${v}`} style={styles.vibe}>
                  {vibeText(v)}
                </Text>
              ))}
            </View>
          ) : null}

          {features.length ? (
            <View style={styles.box}>
              <Text style={styles.boxTitle}>Öne çıkanlar</Text>
              <View style={styles.features}>
                {(allFeatures ? features : features.slice(0, 6)).map((f) => (
                  <View key={f} style={styles.feature}>
                    <Ionicons name={featureIcon(f) as ComponentProps<typeof Ionicons>['name']} size={18} color={colors.coral} />
                    <Text style={styles.featureText} numberOfLines={2}>
                      {f}
                    </Text>
                  </View>
                ))}
              </View>
              {features.length > 6 ? (
                <Pressable
                  onPress={() => setAllFeatures((v) => !v)}
                  style={styles.more}
                  accessibilityRole="button"
                  accessibilityState={{ expanded: allFeatures }}>
                  <Text style={styles.link}>{allFeatures ? 'Daha az göster' : `Tümünü göster (${features.length})`}</Text>
                </Pressable>
              ) : null}
            </View>
          ) : null}

          {detail.events.length ? (
            <View style={styles.box}>
              <Text style={styles.boxTitle}>Yaklaşan etkinlikler</Text>
              {detail.events.map((e) => (
                <View key={e.id} style={styles.eventRow}>
                  <Ionicons name="calendar-outline" size={16} color={colors.coral} />
                  <Text style={styles.eventTitle}>{e.title}</Text>
                  {e.startsAt ? (
                    <Text style={styles.eventWhen}>
                      {new Date(e.startsAt).toLocaleString('tr-TR', { hour: '2-digit', minute: '2-digit', day: 'numeric', month: 'short' })}
                    </Text>
                  ) : null}
                </View>
              ))}
            </View>
          ) : null}

          {week.length ? (
            <View style={styles.box}>
              <Pressable
                style={styles.hoursHead}
                onPress={() => setHoursOpen((o) => !o)}
                accessibilityRole="button"
                accessibilityState={{ expanded: hoursOpen }}
                accessibilityLabel={`Çalışma saatleri, bugün ${today?.hours ?? 'bilinmiyor'}`}>
                <Ionicons name="time-outline" size={18} color={colors.coral} />
                <View style={{ flex: 1 }}>
                  <Text style={styles.boxTitle}>Çalışma saatleri</Text>
                  <Text style={styles.hoursToday}>Bugün · {today?.hours ?? '—'}</Text>
                </View>
                <Ionicons name={hoursOpen ? 'chevron-up' : 'chevron-down'} size={18} color={colors.muted} />
              </Pressable>
              {hoursOpen
                ? week.map((row) => (
                    <View key={row.day} style={styles.hourRow}>
                      <Text style={[styles.hourDay, row === today && styles.hourNow]}>{row.day}</Text>
                      <Text style={[styles.hourVal, row === today && styles.hourNow]}>{row.hours}</Text>
                    </View>
                  ))
                : null}
            </View>
          ) : detail.openingHours ? (
            <View style={styles.box}>
              <Text style={styles.boxTitle}>Çalışma saatleri</Text>
              <Text style={styles.desc}>{detail.openingHours}</Text>
            </View>
          ) : null}

          {walk || drive ? (
            <>
              <Text style={styles.section}>Nasıl giderim?</Text>
              <View style={styles.routeRow} accessibilityRole="radiogroup">
                {(
                  [
                    ['foot', 'walk', 'Yürüyerek', walk],
                    ['driving', 'car', 'Araçla', drive],
                  ] as const
                ).map(([key, icon, label, r]) => (
                  <Pressable
                    key={key}
                    style={[styles.routeChip, mode === key && styles.routeChipOn]}
                    onPress={() => setMode(key)}
                    accessibilityRole="radio"
                    accessibilityState={{ selected: mode === key }}
                    accessibilityLabel={`${label}${r ? `, ${formatRouteLine(r.distanceMeters, r.durationSeconds)}` : ''}`}>
                    <Ionicons name={icon} size={18} color={mode === key ? colors.white : colors.ink} />
                    <View style={{ flex: 1 }}>
                      <Text style={[styles.routeLabel, mode === key && styles.routeOnText]}>{label}</Text>
                      <Text style={[styles.routeMeta, mode === key && styles.routeOnText]}>
                        {r ? formatRouteLine(r.distanceMeters, r.durationSeconds) : '—'}
                      </Text>
                    </View>
                  </Pressable>
                ))}
              </View>
              {route?.points.length ? (
                <View style={styles.miniMapWrap} pointerEvents="none">
                  <MapView
                    style={styles.miniMap}
                    initialRegion={{
                      latitude: (detail.latitude + lat) / 2,
                      longitude: (detail.longitude + lng) / 2,
                      latitudeDelta: Math.max(0.01, Math.abs(detail.latitude - lat) * 1.8),
                      longitudeDelta: Math.max(0.01, Math.abs(detail.longitude - lng) * 1.8),
                    }}>
                    <Polyline coordinates={route.points} strokeColor={colors.coral} strokeWidth={4} />
                    <Marker coordinate={{ latitude: lat, longitude: lng }} pinColor={colors.sage} />
                    <Marker coordinate={{ latitude: detail.latitude, longitude: detail.longitude }} pinColor={colors.espresso} />
                  </MapView>
                </View>
              ) : null}
            </>
          ) : null}

          <View style={styles.sectionRow}>
            <Text style={styles.section}>Yorumlar</Text>
            {!mine ? (
              <Pressable onPress={writeReview} hitSlop={10} accessibilityRole="button" accessibilityLabel="Yorum yaz">
                <Text style={styles.link}>Yorum yaz</Text>
              </Pressable>
            ) : null}
          </View>
          <RatingSummary summary={summary} />

          {mine ? (
            <View style={[styles.review, styles.mine]}>
              <View style={styles.reviewHead}>
                <Avatar name={session?.displayName} url={session?.avatarUrl} size={36} />
                <View style={{ flex: 1 }}>
                  <Text style={styles.author}>Senin yorumun</Text>
                  <View style={styles.reviewMeta}>
                    <Stars value={mine.stars} size={12} />
                    <Text style={styles.when}>{relativeDate(mine.createdAt)}</Text>
                  </View>
                </View>
                <Pressable onPress={writeReview} hitSlop={8} style={styles.iconBtn} accessibilityRole="button" accessibilityLabel="Yorumu düzenle">
                  <Ionicons name="create-outline" size={20} color={colors.coral} />
                </Pressable>
                <Pressable onPress={removeMine} hitSlop={8} style={styles.iconBtn} accessibilityRole="button" accessibilityLabel="Yorumu sil">
                  <Ionicons name="trash-outline" size={20} color={colors.danger} />
                </Pressable>
              </View>
              <ReviewBody review={mine} onPhoto={setViewer} />
            </View>
          ) : null}

          {reviews.length === 0 ? (
            <View style={styles.firstReview}>
              <Text style={styles.firstTitle}>İlk yorumu sen yaz</Text>
              <Text style={styles.firstBody}>Deneyimin, buraya gelmeyi düşünenlere yol gösterir.</Text>
              <Button label="Yorum yaz" icon="create-outline" variant="secondary" compact onPress={writeReview} />
            </View>
          ) : null}

          {others.map((r) => (
            <View key={r.id} style={styles.review}>
              <View style={styles.reviewHead}>
                <Avatar name={r.authorName} url={r.authorAvatarUrl} size={36} />
                <View style={{ flex: 1 }}>
                  <Text style={styles.author}>{r.authorName || 'Gezgin'}</Text>
                  <View style={styles.reviewMeta}>
                    <Stars value={r.stars} size={12} />
                    <Text style={styles.when}>{relativeDate(r.createdAt)}</Text>
                  </View>
                </View>
                <Pressable
                  onPress={() => report(r.id, r.userId, r.authorName ?? undefined)}
                  hitSlop={8}
                  style={styles.iconBtn}
                  accessibilityRole="button"
                  accessibilityLabel="Yorumu bildir ya da yazarı engelle">
                  <Ionicons name="flag-outline" size={17} color={colors.muted} />
                </Pressable>
              </View>
              <ReviewBody review={r} onPhoto={setViewer} />
            </View>
          ))}

          {external.length ? (
            <>
              <View style={styles.sectionRow}>
                <Text style={styles.section}>Google’da ne diyorlar?</Text>
                {detail.mapsUri ? (
                  <Pressable onPress={() => void open(detail.mapsUri!)} hitSlop={10} accessibilityRole="link" accessibilityLabel="Tüm Google yorumlarını aç">
                    <Text style={styles.link}>Tümü</Text>
                  </Pressable>
                ) : null}
              </View>
              {external.map((r, i) => (
                <ExternalReviewCard key={`${r.author}-${i}`} review={r} />
              ))}
              <Text style={styles.source}>Yorumlar Google Haritalar’dan alınmıştır.</Text>
            </>
          ) : null}

          <View style={styles.footLinks}>
            {session ? (
              <Pressable
                style={styles.footLink}
                onPress={() => router.push({ pathname: '/owner/[id]', params: { id: id! } })}
                accessibilityRole="button">
                <Ionicons name="storefront-outline" size={18} color={colors.ink} />
                <Text style={styles.footText}>{owner ? 'İşletme paneli' : 'Bu mekân senin mi? Sahiplen'}</Text>
                <Ionicons name="chevron-forward" size={16} color={colors.muted} />
              </Pressable>
            ) : null}
            <Pressable style={styles.footLink} onPress={() => report()} accessibilityRole="button">
              <Ionicons name="flag-outline" size={18} color={colors.ink} />
              <Text style={styles.footText}>Hatalı bilgi bildir</Text>
              <Ionicons name="chevron-forward" size={16} color={colors.muted} />
            </Pressable>
          </View>
          <Text style={styles.attr}>{detail.attribution}</Text>
        </View>
      </ScrollView>

      <View style={[styles.sticky, { paddingBottom: insets.bottom + 12 }]}>
        <Button
          label="Yol tarifi al"
          icon="navigate"
          style={{ flex: 1 }}
          onPress={() => void openDirections(detail.latitude, detail.longitude, lat, lng).catch(() => toast.error('Harita açılamadı.'))}
        />
        <Button label={mine ? 'Düzenle' : 'Yorum yaz'} icon="create-outline" variant="outline" onPress={writeReview} />
      </View>

      <PhotoViewer
        uris={viewer?.uris ?? []}
        index={viewer?.index ?? null}
        onClose={() => setViewer(null)}
        onReport={viewer?.paths && viewer.reviewId !== mine?.id ? (i) => reportPhoto(viewer.paths![i]) : undefined}
      />
    </View>
  );
}

function ExternalReviewCard({ review }: { review: ExternalReview }) {
  const [expanded, setExpanded] = useState(false);
  const body = clip(review.text ?? '', 220);
  return (
    <View style={styles.review}>
      <View style={styles.reviewHead}>
        <Avatar name={review.author} url={review.authorPhotoUrl} size={36} />
        <View style={{ flex: 1 }}>
          {review.authorUri ? (
            <Text
              style={[styles.author, styles.authorLink]}
              numberOfLines={1}
              onPress={() => void open(review.authorUri!)}
              accessibilityRole="link"
              accessibilityHint="Yazarın Google profilini açar">
              {review.author}
            </Text>
          ) : (
            <Text style={styles.author} numberOfLines={1}>
              {review.author}
            </Text>
          )}
          <View style={styles.reviewMeta}>
            <Stars value={review.rating} size={12} />
            {review.when ? <Text style={styles.when}>{review.when}</Text> : null}
          </View>
        </View>
      </View>
      {review.text ? (
        <Pressable
          onPress={() => setExpanded((v) => !v)}
          disabled={!body.clipped}
          accessibilityRole={body.clipped ? 'button' : 'text'}
          accessibilityHint={body.clipped ? 'Yorumun tamamını gösterir' : undefined}>
          <Text style={styles.reviewText}>{expanded ? review.text : body.text}</Text>
          {body.clipped ? <Text style={[styles.link, styles.readMore]}>{expanded ? 'Kısalt' : 'Devamını oku'}</Text> : null}
        </Pressable>
      ) : null}
    </View>
  );
}

function ReviewBody({
  review,
  onPhoto,
}: {
  review: ReviewItem;
  onPhoto: (v: ViewerState) => void;
}) {
  // Aynı fotoğraf iki kez eklenmişse bir kez göster (anahtar da URI).
  const paths = [...new Set((review.photoUrls ?? []).filter((u) => mediaUrl(u)))];
  const uris = uniqueStrings(paths.map((u) => mediaUrl(u)));
  return (
    <>
      {review.text ? <Text style={styles.reviewText}>{review.text}</Text> : null}
      {review.tags?.length ? (
        <View style={styles.tagRow}>
          {review.tags.map((t) => (
            <Text key={t} style={styles.tag}>
              {vibeText(t)}
            </Text>
          ))}
        </View>
      ) : null}
      {uris.length ? (
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.reviewPhotos}>
          {uris.map((uri, i) => (
            <Pressable
              key={uri}
              onPress={() => onPhoto({ uris, index: i, reviewId: review.id, paths })}
              accessibilityRole="imagebutton"
              accessibilityLabel="Yorum fotoğrafı, büyüt">
              <Image source={{ uri }} style={styles.reviewPhoto} contentFit="cover" transition={150} />
            </Pressable>
          ))}
        </ScrollView>
      ) : null}
      {review.ownerReply ? (
        <View style={styles.reply}>
          <Text style={styles.replyWho}>İşletmenin yanıtı</Text>
          <Text style={styles.replyText}>{review.ownerReply}</Text>
        </View>
      ) : null}
    </>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  center: { flex: 1 },
  skel: { padding: 22, gap: 12 },
  headRight: { flexDirection: 'row', gap: 8 },
  glass: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.glass,
    alignItems: 'center',
    justifyContent: 'center',
  },
  hero: { height: HERO, backgroundColor: colors.line },
  dots: {
    position: 'absolute',
    left: 16,
    right: 16,
    bottom: 40,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  dot: { width: 7, height: 7, borderRadius: 4, backgroundColor: 'rgba(255,255,255,0.4)' },
  dotOn: { backgroundColor: colors.white, width: 16 },
  count: { marginLeft: 'auto', color: colors.white, fontFamily: fonts.sansBold, fontSize: 12 },
  body: {
    backgroundColor: colors.surface,
    marginTop: -28,
    borderTopLeftRadius: radii.lg,
    borderTopRightRadius: radii.lg,
    padding: 22,
    minHeight: 400,
  },
  h: { fontFamily: fonts.display, fontSize: 30, lineHeight: 36, color: colors.ink },
  kind: { color: colors.sage, fontFamily: fonts.sansBold, fontSize: 14, marginTop: 6 },
  gallery: { gap: 8, marginTop: 16 },
  galleryPhoto: { width: 132, height: 96, borderRadius: 14, backgroundColor: colors.line },
  features: { flexDirection: 'row', flexWrap: 'wrap', marginTop: 10, rowGap: 12 },
  feature: { width: '50%', flexDirection: 'row', alignItems: 'center', gap: 8, paddingRight: 8 },
  featureText: { flex: 1, color: colors.ink, fontFamily: fonts.sansMed, fontSize: 13 },
  more: { minHeight: 44, justifyContent: 'center', marginTop: 4 },
  source: { color: colors.muted, fontFamily: fonts.sans, fontSize: 12, marginTop: 10 },
  readMore: { marginTop: 6 },
  addr: { color: colors.muted, marginTop: 6, fontFamily: fonts.sans, fontSize: 14, lineHeight: 20 },
  statusRow: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 10, marginTop: 12 },
  status: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: radii.pill,
  },
  open: { backgroundColor: colors.coralSoft },
  closed: { backgroundColor: colors.dangerSoft },
  statusDot: { width: 7, height: 7, borderRadius: 4 },
  statusText: { fontFamily: fonts.sansBold, fontSize: 13 },
  ratingInline: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  ratingText: { fontFamily: fonts.sansBold, color: colors.ink, fontSize: 14 },
  ratingCount: { color: colors.muted, fontSize: 13, fontFamily: fonts.sans },
  fresh: { color: colors.muted, fontFamily: fonts.sans, fontSize: 13 },
  verified: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  verifiedText: { color: colors.sage, fontFamily: fonts.sansBold, fontSize: 13 },
  actions: { flexDirection: 'row', marginTop: 18, justifyContent: 'space-between' },
  action: { flex: 1, alignItems: 'center', gap: 6, minHeight: 68 },
  actionIcon: {
    width: 48,
    height: 48,
    borderRadius: 24,
    backgroundColor: colors.coralSoft,
    alignItems: 'center',
    justifyContent: 'center',
  },
  actionText: { color: colors.ink, fontFamily: fonts.sansMed, fontSize: 12 },
  note: { color: colors.ink, marginTop: 18, lineHeight: 24, fontSize: 16, fontFamily: fonts.sans },
  desc: { color: colors.ink, marginTop: 8, lineHeight: 22, fontFamily: fonts.sans, opacity: 0.88 },
  pills: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 14 },
  vibe: {
    backgroundColor: colors.bg,
    color: colors.espresso,
    fontFamily: fonts.sansMed,
    fontSize: 13,
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: radii.pill,
    overflow: 'hidden',
    borderWidth: 1,
    borderColor: colors.line,
  },
  box: {
    marginTop: 16,
    backgroundColor: colors.bg,
    borderRadius: 16,
    padding: 14,
    borderWidth: 1,
    borderColor: colors.line,
  },
  boxTitle: { fontFamily: fonts.sansBold, color: colors.ink, fontSize: 15 },
  eventRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 10 },
  eventTitle: { flex: 1, color: colors.ink, fontFamily: fonts.sansMed, fontSize: 14 },
  eventWhen: { color: colors.muted, fontFamily: fonts.sans, fontSize: 13 },
  hoursHead: { flexDirection: 'row', alignItems: 'center', gap: 10, minHeight: 44 },
  hoursToday: { color: colors.muted, fontFamily: fonts.sans, fontSize: 13, marginTop: 2 },
  hourRow: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 6 },
  hourDay: { color: colors.muted, fontFamily: fonts.sansMed, width: 44 },
  hourVal: { color: colors.ink, fontFamily: fonts.sansMed },
  hourNow: { color: colors.coral, fontFamily: fonts.sansBold },
  sectionRow: { flexDirection: 'row', alignItems: 'baseline', justifyContent: 'space-between' },
  section: { fontFamily: fonts.display, marginTop: 26, color: colors.ink, fontSize: 22 },
  link: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 14 },
  routeRow: { flexDirection: 'row', gap: 8, marginTop: 12 },
  routeChip: {
    flex: 1,
    minHeight: 56,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: colors.line,
    backgroundColor: colors.bg,
    paddingHorizontal: 12,
    paddingVertical: 10,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  routeChipOn: { backgroundColor: colors.coral, borderColor: colors.coral },
  routeLabel: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 13 },
  routeMeta: { color: colors.muted, fontFamily: fonts.sans, fontSize: 12, marginTop: 1 },
  routeOnText: { color: colors.white },
  miniMapWrap: { height: 180, borderRadius: 16, marginTop: 12, overflow: 'hidden' },
  miniMap: { flex: 1 },
  review: { marginTop: 12, backgroundColor: colors.bg, padding: 14, borderRadius: 16 },
  mine: { borderWidth: 1.5, borderColor: colors.coralSoft, backgroundColor: colors.surface },
  reviewHead: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  author: { fontFamily: fonts.sansBold, color: colors.ink, fontSize: 14 },
  authorLink: { textDecorationLine: 'underline' },
  reviewMeta: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 3 },
  when: { color: colors.muted, fontSize: 12, fontFamily: fonts.sans },
  iconBtn: { width: 36, height: 36, alignItems: 'center', justifyContent: 'center' },
  reviewText: { color: colors.ink, marginTop: 10, lineHeight: 21, fontFamily: fonts.sans, fontSize: 14 },
  tagRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 10 },
  tag: {
    backgroundColor: colors.coralSoft,
    color: colors.espresso,
    fontFamily: fonts.sansMed,
    fontSize: 12,
    paddingHorizontal: 9,
    paddingVertical: 4,
    borderRadius: radii.pill,
    overflow: 'hidden',
  },
  reviewPhotos: { gap: 8, marginTop: 10 },
  reviewPhoto: { width: 84, height: 84, borderRadius: 12, backgroundColor: colors.line },
  reply: { marginTop: 10, borderLeftWidth: 3, borderLeftColor: colors.gold, paddingLeft: 10 },
  replyWho: { fontFamily: fonts.sansBold, color: colors.ink, fontSize: 12 },
  replyText: { color: colors.ink, opacity: 0.86, marginTop: 2, lineHeight: 20, fontFamily: fonts.sans, fontSize: 14 },
  firstReview: {
    marginTop: 12,
    padding: 18,
    borderRadius: 16,
    backgroundColor: colors.bg,
    alignItems: 'center',
    gap: 8,
  },
  firstTitle: { fontFamily: fonts.sansBold, color: colors.ink, fontSize: 16 },
  firstBody: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, textAlign: 'center', lineHeight: 20, marginBottom: 4 },
  footLinks: { marginTop: 26, borderTopWidth: 1, borderTopColor: colors.line },
  footLink: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    minHeight: 52,
    borderBottomWidth: 1,
    borderBottomColor: colors.line,
  },
  footText: { flex: 1, color: colors.ink, fontFamily: fonts.sansMed, fontSize: 14 },
  attr: { marginTop: 16, fontSize: 12, color: colors.muted, fontFamily: fonts.sans },
  sticky: {
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
    flexDirection: 'row',
    gap: 10,
    paddingHorizontal: 16,
    paddingTop: 12,
    backgroundColor: colors.surface,
    borderTopWidth: 1,
    borderTopColor: colors.line,
  },
});
