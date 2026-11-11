import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { VenueImage } from '@/components/VenueImage';
import { api, mediaUrl } from '@/lib/api';
import { vibeText } from '@/lib/constants';
import { friendlyError } from '@/lib/empty';
import { useFilters } from '@/lib/filters';
import { detectLocation } from '@/lib/geo';
import { parseOpeningHours } from '@/lib/hours';
import { openDirections } from '@/lib/maps';
import { openedVenue } from '@/lib/opened-venue';
import { formatRouteLine } from '@/lib/route';
import { useSession } from '@/lib/session';
import { colors, fonts, radii } from '@/lib/theme';
import type { ReviewItem, RouteMode, RouteResult, VenueDetail } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { Link, useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import {
  Alert,
  Linking,
  NativeScrollEvent,
  NativeSyntheticEvent,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  useWindowDimensions,
  View,
} from 'react-native';
import MapView, { Marker, Polyline, UrlTile } from 'react-native-maps';

export default function Venue() {
  const params = useLocalSearchParams<{ id: string | string[] }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const filters = useFilters();
  const { session } = useSession();
  const router = useRouter();
  const { width } = useWindowDimensions();
  const [detail, setDetail] = useState<VenueDetail | null>(null);
  const [reviews, setReviews] = useState<ReviewItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saved, setSaved] = useState(false);
  const [photoIx, setPhotoIx] = useState(0);
  const [walk, setWalk] = useState<RouteResult | null>(null);
  const [drive, setDrive] = useState<RouteResult | null>(null);
  const [mode, setMode] = useState<RouteMode>('foot');
  const [me, setMe] = useState<{ lat: number; lng: number } | null>(null);
  const [routeFrom, setRouteFrom] = useState<{ lat: number; lng: number } | null>(null);
  const owner = Boolean(session?.roles.includes('VenueOwner'));

  useEffect(() => {
    if (!id) {
      return;
    }
    const known = openedVenue(id);
    if (known) {
      setDetail({
        placeId: known.placeId,
        name: known.name,
        address: known.address,
        openNow: known.openNow,
        nereyeRating: known.nereyeRating,
        nereyeRatingCount: known.nereyeRatingCount,
        claimed: false,
        attribution: 'Harita verisi: OpenStreetMap',
        vibes: known.vibes ?? [],
        events: [],
        latitude: known.latitude,
        longitude: known.longitude,
        photoUrl: known.photoUrl,
        photoUrls: known.photoUrl ? [known.photoUrl] : [],
        note: known.note,
        cuisine: known.cuisine,
        amenityChips: [],
      });
      setLoading(false);
    }
    let alive = true;
    (async () => {
      if (!known) {
        setLoading(true);
      }
      setError(null);
      try {
        const v = await api.venue(id);
        if (!alive) {
          return;
        }
        setDetail(v);
        setSaved(Boolean(v.isFavorite));
        try {
          setReviews(await api.reviews(id));
        } catch {
          if (alive) {
            setReviews([]);
          }
        }
        const fix = await detectLocation();
        const originLat = fix?.lat ?? filters.lat;
        const originLng = fix?.lng ?? filters.lng;
        if (alive) {
          setRouteFrom({ lat: originLat, lng: originLng });
          if (fix) {
            setMe({ lat: fix.lat, lng: fix.lng });
          }
        }
        const [foot, driving] = await Promise.all([
          api.route(originLat, originLng, v.latitude, v.longitude, 'foot'),
          api.route(originLat, originLng, v.latitude, v.longitude, 'driving'),
        ]);
        if (!alive) {
          return;
        }
        setWalk(foot ?? null);
        setDrive(driving ?? null);
        if (session && !v.isFavorite) {
          setSaved(await api.isFavorite(id));
        }
      } catch (e) {
        if (alive && !known) {
          setError(friendlyError(e));
        }
      } finally {
        if (alive) {
          setLoading(false);
        }
      }
    })();
    return () => {
      alive = false;
    };
  }, [id, session, filters.lat, filters.lng]);

  async function toggleSave() {
    if (!session) {
      router.push('/auth');
      return;
    }
    const next = !saved;
    setSaved(next);
    try {
      if (next) {
        await api.favorite(id!);
      } else {
        await api.unfavorite(id!);
      }
    } catch {
      setSaved(!next);
    }
  }

  function report() {
    if (!session) {
      router.push('/auth');
      return;
    }
    Alert.alert('Şikayet', 'Bu mekânı neden bildiriyorsun?', [
      { text: 'Vazgeç', style: 'cancel' },
      { text: 'Yanlış bilgi', onPress: () => void api.report(id!, 'yanlis-bilgi') },
      { text: 'Uygunsuz', onPress: () => void api.report(id!, 'uygunsuz') },
    ]);
  }

  if (loading && !detail) {
    return (
      <View style={styles.bg}>
        <Text style={styles.loading}>Yükleniyor…</Text>
      </View>
    );
  }

  if (!detail) {
    return (
      <View style={styles.bg}>
        {error ? <ErrorBanner message={error} onRetry={() => setDetail(null)} /> : <EmptyState title="Mekan bulunamadı." />}
      </View>
    );
  }

  const photos = detail.photoUrls.length ? detail.photoUrls : detail.photoUrl ? [detail.photoUrl] : [];
  const week = parseOpeningHours(detail.openingHours);
  const osm = `https://www.openstreetmap.org/?mlat=${detail.latitude}&mlon=${detail.longitude}#map=18/${detail.latitude}/${detail.longitude}`;

  function onHeroScroll(e: NativeSyntheticEvent<NativeScrollEvent>) {
    const x = e.nativeEvent.contentOffset.x;
    setPhotoIx(Math.round(x / Math.max(width, 1)));
  }

  return (
    <ScrollView style={styles.bg} contentContainerStyle={styles.wrap}>
      <View style={[styles.hero, { width }]}>
        <ScrollView
          horizontal
          pagingEnabled
          showsHorizontalScrollIndicator={false}
          onMomentumScrollEnd={onHeroScroll}
          style={{ width, height: 320 }}>
          {(photos.length ? photos : [null]).map((url, i) => (
            <View key={url ?? `ph-${i}`} style={{ width, height: 320 }}>
              <VenueImage
                placeId={detail.placeId}
                photoUrl={url}
                name={detail.name}
                cuisine={detail.cuisine}
                intent={filters.intent}
                style={StyleSheet.absoluteFill}
                radius={0}
              />
              <LinearGradient colors={['transparent', 'rgba(15,20,25,0.7)']} style={styles.fade} />
            </View>
          ))}
        </ScrollView>
        <View style={styles.dots} pointerEvents="none">
          {(photos.length ? photos : [null]).map((_, i) => (
            <View key={`dot-${i}`} style={[styles.dot, i === photoIx ? styles.dotOn : null]} />
          ))}
          <Text style={styles.count}>
            {photoIx + 1} / {Math.max(photos.length, 1)}
          </Text>
        </View>
      </View>
      <View style={styles.body}>
        <View style={styles.titleRow}>
          <Text style={styles.h}>{detail.name}</Text>
          <Pressable onPress={() => void toggleSave()} accessibilityRole="button" accessibilityLabel={saved ? 'Kayıttan çıkar' : 'Kaydet'} hitSlop={8}>
            <Ionicons name={saved ? 'heart' : 'heart-outline'} size={26} color={saved ? colors.coral : colors.ink} />
          </Pressable>
        </View>
        <Text style={styles.addr}>{detail.address}</Text>
        <View style={styles.pills}>
          {detail.openNow === true ? <Text style={[styles.pill, styles.open]}>Açık</Text> : null}
          {detail.openNow === false ? <Text style={[styles.pill, styles.closed]}>Kapalı</Text> : null}
          {detail.claimed ? <Text style={styles.pill}>Sahiplenildi</Text> : null}
          <Text style={styles.pill}>
            Nereye {detail.nereyeRating != null ? detail.nereyeRating.toFixed(1) : 'yeni'}
            {detail.nereyeRatingCount ? ` · ${detail.nereyeRatingCount}` : ''}
          </Text>
          {detail.googleRating != null ? (
            <Text style={styles.pill}>Google {detail.googleRating.toFixed(1)}</Text>
          ) : null}
        </View>
        {detail.vibes.length || (detail.amenityChips?.length ?? 0) ? (
          <View style={styles.pills}>
            {detail.amenityChips?.map((c) => (
              <Text key={c} style={styles.vibe}>
                {c}
              </Text>
            ))}
            {detail.vibes.map((v) => (
              <Text key={v} style={styles.vibe}>
                {vibeText(v)}
              </Text>
            ))}
          </View>
        ) : null}
        {week.length ? (
          <View style={styles.hoursBox}>
            <Text style={styles.hoursTitle}>Çalışma saatleri</Text>
            {week.map((row) => (
              <View key={row.day} style={styles.hourRow}>
                <Text style={styles.hourDay}>{row.day}</Text>
                <Text style={styles.hourVal}>{row.hours}</Text>
              </View>
            ))}
          </View>
        ) : detail.openingHours ? (
          <Text style={styles.hours}>{detail.openingHours}</Text>
        ) : null}
        {detail.note ? <Text style={styles.note}>{detail.note}</Text> : null}
        {detail.ownerDescription ? <Text style={styles.desc}>{detail.ownerDescription}</Text> : null}
        {detail.events.map((e) => (
          <Text key={e.id} style={styles.event}>
            {e.title}
            {e.startsAt ? ` · ${new Date(e.startsAt).toLocaleString('tr-TR', { hour: '2-digit', minute: '2-digit', day: 'numeric', month: 'short' })}` : ''}
          </Text>
        ))}
        <View style={styles.routeRow}>
          <Pressable
            style={[styles.routeChip, mode === 'foot' && styles.routeChipOn]}
            onPress={() => setMode('foot')}
            accessibilityRole="button"
            accessibilityState={{ selected: mode === 'foot' }}>
            <Ionicons name="walk" size={16} color={mode === 'foot' ? colors.white : colors.ink} />
            <Text style={[styles.routeChipText, mode === 'foot' && styles.routeChipTextOn]}>
              Yürü{walk ? ` · ${formatRouteLine(walk.distanceMeters, walk.durationSeconds)}` : ''}
            </Text>
          </Pressable>
          <Pressable
            style={[styles.routeChip, mode === 'driving' && styles.routeChipOn]}
            onPress={() => setMode('driving')}
            accessibilityRole="button"
            accessibilityState={{ selected: mode === 'driving' }}>
            <Ionicons name="car" size={16} color={mode === 'driving' ? colors.white : colors.ink} />
            <Text style={[styles.routeChipText, mode === 'driving' && styles.routeChipTextOn]}>
              Araç{drive ? ` · ${formatRouteLine(drive.distanceMeters, drive.durationSeconds)}` : ''}
            </Text>
          </Pressable>
        </View>
        {(mode === 'foot' ? walk : drive)?.points.length ? (
          <MapView
            style={styles.miniMap}
            pointerEvents="none"
            initialRegion={{
              latitude: detail.latitude,
              longitude: detail.longitude,
              latitudeDelta: 0.02,
              longitudeDelta: 0.02,
            }}>
            <UrlTile urlTemplate="https://tile.openstreetmap.org/{z}/{x}/{y}.png" maximumZ={19} />
            <Polyline
              coordinates={(mode === 'foot' ? walk : drive)!.points}
              strokeColor={colors.coral}
              strokeWidth={4}
            />
            {me ? <Marker coordinate={{ latitude: me.lat, longitude: me.lng }} title="Sen" pinColor={colors.coral} /> : null}
            <Marker coordinate={{ latitude: detail.latitude, longitude: detail.longitude }} title={detail.name} pinColor={colors.sage} />
          </MapView>
        ) : null}
        <Pressable
          style={styles.btn}
          onPress={() => {
            const origin = routeFrom ?? me;
            void openDirections(detail.latitude, detail.longitude, origin?.lat, origin?.lng, mode);
          }}
          accessibilityRole="button"
          accessibilityLabel="Harici yol tarifi">
          <Ionicons name="navigate" size={18} color={colors.white} />
          <Text style={styles.btnText}>Harici yol tarifi</Text>
        </Pressable>
        {detail.phone ? (
          <Pressable style={styles.outline} onPress={() => void Linking.openURL(`tel:${detail.phone}`)} accessibilityRole="button">
            <Text style={styles.outlineText}>Ara</Text>
          </Pressable>
        ) : null}
        {detail.website ? (
          <Pressable style={styles.outline} onPress={() => void Linking.openURL(detail.website!)} accessibilityRole="button">
            <Text style={styles.outlineText}>Web sitesi</Text>
          </Pressable>
        ) : null}
        <Pressable style={styles.outline} onPress={() => void Linking.openURL(detail.mapsUri || osm)} accessibilityRole="button">
          <Text style={styles.outlineText}>Haritada aç</Text>
        </Pressable>
        {session ? (
          <Link href={{ pathname: '/review/[id]', params: { id: id! } }} asChild>
            <Pressable style={styles.outline} accessibilityRole="button">
              <Text style={styles.outlineText}>Yorum yaz</Text>
            </Pressable>
          </Link>
        ) : (
          <Pressable style={styles.outline} onPress={() => router.push('/auth')} accessibilityRole="button">
            <Text style={styles.outlineText}>Yorum için giriş</Text>
          </Pressable>
        )}
        {session ? (
          <Link href={{ pathname: '/owner/[id]', params: { id: id! } }} asChild>
            <Pressable style={styles.outline} accessibilityRole="button">
              <Text style={styles.outlineText}>{owner ? 'İşletme / öne çıkar' : 'Bu mekân senin mi?'}</Text>
            </Pressable>
          </Link>
        ) : null}
        <Pressable style={styles.ghost} onPress={report} accessibilityRole="button">
          <Text style={styles.ghostText}>Şikayet et</Text>
        </Pressable>
        <Text style={styles.section}>Yorumlar</Text>
        {reviews.length === 0 ? <Text style={styles.meta}>Henüz yorum yok.</Text> : null}
        {reviews.map((r) => (
          <View key={r.id} style={styles.review}>
            <Text style={styles.stars}>{'★'.repeat(r.stars)}</Text>
            <Text style={styles.meta}>{r.authorName ?? 'Gezgin'}{r.createdAt ? ` · ${new Date(r.createdAt).toLocaleDateString('tr-TR')}` : ''}</Text>
            {r.tags?.length ? <Text style={styles.meta}>{r.tags.map(vibeText).join(' · ')}</Text> : null}
            <Text style={styles.desc}>{r.text ?? ''}</Text>
            {r.photoUrls?.length ? (
              <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.reviewPhotos}>
                {r.photoUrls.map((u) => {
                  const src = mediaUrl(u);
                  return src ? <Image key={u} source={{ uri: src }} style={styles.reviewPhoto} /> : null;
                })}
              </ScrollView>
            ) : null}
            {r.ownerReply ? <Text style={styles.reply}>İşletme: {r.ownerReply}</Text> : null}
          </View>
        ))}
        <Text style={styles.attr}>{detail.attribution}</Text>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { paddingBottom: 40 },
  loading: { padding: 24, color: colors.ink },
  hero: { height: 320, backgroundColor: colors.line },
  fade: { position: 'absolute', left: 0, right: 0, bottom: 0, height: 120 },
  dots: {
    position: 'absolute',
    left: 16,
    right: 16,
    bottom: 36,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  dot: { width: 7, height: 7, borderRadius: 4, backgroundColor: 'rgba(255,255,255,0.35)' },
  dotOn: { backgroundColor: colors.white, width: 16 },
  count: { marginLeft: 'auto', color: colors.white, fontWeight: '700', fontSize: 12 },
  body: {
    backgroundColor: colors.surface,
    marginTop: -28,
    borderTopLeftRadius: radii.lg,
    borderTopRightRadius: radii.lg,
    padding: 22,
    minHeight: 400,
  },
  titleRow: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  h: { fontFamily: fonts.display, fontSize: 30, color: colors.ink, flex: 1 },
  addr: { color: colors.muted, marginTop: 6 },
  pills: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 12 },
  pill: {
    backgroundColor: colors.bg,
    color: colors.ink,
    fontWeight: '800',
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: radii.pill,
    overflow: 'hidden',
  },
  open: { backgroundColor: colors.coral, color: colors.white },
  closed: { backgroundColor: colors.espresso, color: colors.ink },
  vibe: {
    backgroundColor: colors.coralSoft,
    color: colors.ink,
    fontWeight: '700',
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: radii.pill,
    overflow: 'hidden',
  },
  hours: { color: colors.ink, marginTop: 12 },
  hoursBox: {
    marginTop: 16,
    backgroundColor: colors.bg,
    borderRadius: 16,
    padding: 14,
    borderWidth: 1,
    borderColor: colors.line,
  },
  hoursTitle: { fontFamily: fonts.sansBold, color: colors.ink, marginBottom: 8 },
  hourRow: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 5 },
  hourDay: { color: colors.muted, fontWeight: '700', width: 40 },
  hourVal: { color: colors.ink, fontWeight: '600' },
  reviewPhotos: { gap: 8, marginTop: 10 },
  reviewPhoto: { width: 72, height: 72, borderRadius: 10, backgroundColor: colors.line },
  meta: { color: colors.muted, marginTop: 10 },
  note: { color: colors.ink, marginTop: 14, lineHeight: 24, fontSize: 16 },
  desc: { color: colors.ink, marginTop: 8, lineHeight: 22 },
  reply: { color: colors.muted, marginTop: 8, fontStyle: 'italic' },
  event: { marginTop: 8, color: colors.coral, fontWeight: '700' },
  routeRow: { flexDirection: 'row', gap: 8, marginTop: 16 },
  routeChip: {
    flex: 1,
    minHeight: 44,
    borderRadius: 14,
    borderWidth: 1,
    borderColor: colors.line,
    backgroundColor: colors.bg,
    paddingHorizontal: 10,
    paddingVertical: 10,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  routeChipOn: { backgroundColor: colors.coral, borderColor: colors.coral },
  routeChipText: { color: colors.ink, fontWeight: '700', fontSize: 12, flex: 1 },
  routeChipTextOn: { color: colors.white },
  miniMap: { height: 180, borderRadius: 16, marginTop: 12, overflow: 'hidden' },
  btn: {
    backgroundColor: colors.coral,
    padding: 16,
    minHeight: 44,
    borderRadius: 16,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
    gap: 8,
    marginTop: 18,
  },
  btnText: { color: colors.white, fontWeight: '800' },
  outline: {
    padding: 13,
    minHeight: 44,
    borderRadius: 14,
    borderWidth: 1,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
    gap: 8,
    marginTop: 8,
  },
  outlineText: { color: colors.ink, fontWeight: '700' },
  ghost: { marginTop: 12, alignItems: 'center', minHeight: 44, justifyContent: 'center' },
  ghostText: { color: colors.muted, fontWeight: '700' },
  section: { fontFamily: fonts.display, marginTop: 22, color: colors.ink, fontSize: 22 },
  review: { marginTop: 10, backgroundColor: colors.bg, padding: 12, borderRadius: 14 },
  stars: { color: colors.gold, fontWeight: '700' },
  attr: { marginTop: 24, fontSize: 12, color: colors.muted },
});
