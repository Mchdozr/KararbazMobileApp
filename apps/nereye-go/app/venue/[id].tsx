import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { VenueImage } from '@/components/VenueImage';
import { api } from '@/lib/api';
import { vibeText } from '@/lib/constants';
import { friendlyError } from '@/lib/empty';
import { useFilters } from '@/lib/filters';
import { openDirections } from '@/lib/maps';
import { useSession } from '@/lib/session';
import { colors, fonts, radii } from '@/lib/theme';
import type { ReviewItem, VenueDetail } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import { Link, useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Alert, Linking, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';

export default function Venue() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const filters = useFilters();
  const { session } = useSession();
  const router = useRouter();
  const [detail, setDetail] = useState<VenueDetail | null>(null);
  const [reviews, setReviews] = useState<ReviewItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saved, setSaved] = useState(false);
  const owner = Boolean(session?.roles.includes('VenueOwner'));

  useEffect(() => {
    if (!id) {
      return;
    }
    let alive = true;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const v = await api.venue(id);
        if (!alive) {
          return;
        }
        setDetail(v);
        setSaved(Boolean(v.isFavorite));
        setReviews(await api.reviews(id));
        if (session && !v.isFavorite) {
          setSaved(await api.isFavorite(id));
        }
      } catch (e) {
        if (alive) {
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
  }, [id, session]);

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
  const osm = `https://www.openstreetmap.org/?mlat=${detail.latitude}&mlon=${detail.longitude}#map=18/${detail.latitude}/${detail.longitude}`;

  return (
    <ScrollView style={styles.bg} contentContainerStyle={styles.wrap}>
      <ScrollView horizontal pagingEnabled showsHorizontalScrollIndicator={false} style={styles.hero}>
        {(photos.length ? photos : [null]).map((url, i) => (
          <View key={url ?? `ph-${i}`} style={styles.heroPage}>
            <VenueImage placeId={detail.placeId} photoUrl={url} intent={filters.intent} style={StyleSheet.absoluteFill} radius={0} />
            <LinearGradient colors={['transparent', 'rgba(28,20,16,0.55)']} style={styles.fade} />
          </View>
        ))}
      </ScrollView>
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
        {detail.vibes.length ? (
          <View style={styles.pills}>
            {detail.vibes.map((v) => (
              <Text key={v} style={styles.vibe}>
                {vibeText(v)}
              </Text>
            ))}
          </View>
        ) : null}
        {detail.openingHours ? <Text style={styles.hours}>{detail.openingHours}</Text> : null}
        {detail.note ? <Text style={styles.note}>{detail.note}</Text> : null}
        {detail.ownerDescription ? <Text style={styles.desc}>{detail.ownerDescription}</Text> : null}
        {detail.events.map((e) => (
          <Text key={e.id} style={styles.event}>
            {e.title}
            {e.startsAt ? ` · ${new Date(e.startsAt).toLocaleString('tr-TR', { hour: '2-digit', minute: '2-digit', day: 'numeric', month: 'short' })}` : ''}
          </Text>
        ))}
        <Pressable
          style={styles.btn}
          onPress={() => {
            void openDirections(detail.latitude, detail.longitude, filters.lat, filters.lng);
          }}
          accessibilityRole="button"
          accessibilityLabel="Yol tarifi">
          <Ionicons name="navigate" size={18} color={colors.white} />
          <Text style={styles.btnText}>Yol tarifi</Text>
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
  hero: { width: '100%', height: 320, backgroundColor: colors.line },
  heroPage: { width: 390, height: 320 },
  fade: { position: 'absolute', left: 0, right: 0, bottom: 0, height: 120 },
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
  open: { backgroundColor: colors.sage, color: colors.white },
  closed: { backgroundColor: colors.coral, color: colors.white },
  vibe: {
    backgroundColor: colors.coralSoft,
    color: colors.espresso,
    fontWeight: '700',
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: radii.pill,
    overflow: 'hidden',
  },
  hours: { color: colors.ink, marginTop: 12 },
  meta: { color: colors.muted, marginTop: 10 },
  note: { color: colors.ink, marginTop: 14, lineHeight: 24, fontSize: 16 },
  desc: { color: colors.ink, marginTop: 8, lineHeight: 22 },
  reply: { color: colors.muted, marginTop: 8, fontStyle: 'italic' },
  event: { marginTop: 8, color: colors.coral, fontWeight: '700' },
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
