import { t } from '@/lib/i18n';
import { ErrorBanner } from '@/components/ErrorBanner';
import { FilterSheet } from '@/components/FilterSheet';
import { SearchingOverlay } from '@/components/SearchingOverlay';
import { VenueProductCard } from '@/components/VenueProductCard';
import { emptyMap, friendlyError } from '@/lib/empty';
import { detectLocation, reverseLabel } from '@/lib/geo';
import { geoDistanceMeters } from '@/lib/geo-hit';
import { clampRadius } from '@/lib/radius';
import { activeFilterCount } from '@/lib/sort';
import { toast } from '@/lib/toast';
import { useFilters } from '@/lib/filters';
import { api } from '@/lib/api';
import { useLatestRequest } from '@/lib/use-latest-request';
import { peekRecommend, recommendCacheKey } from '@/lib/recommend-cache';
import { colors, fonts, INTENT_ICONS, shadows } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import MapView, { currentMapMode, type MapRef, type Region } from '@/components/Map';
import { MePin, VenuePin } from '@/components/MapPin';
import { mapAttribution } from '@/lib/map-config';
import { INTENTS, intentLabel } from '@/lib/constants';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

function inRegion(card: VenueCard, region: Region) {
  const latPad = region.latitudeDelta / 2;
  const lngPad = region.longitudeDelta / 2;
  return (
    Math.abs(card.latitude - region.latitude) <= latPad &&
    Math.abs(card.longitude - region.longitude) <= lngPad
  );
}

/** Arama noktası ve en yakın mekanları kapsayan bölge; çok sıkışmasın, çok da açılmasın. */
function fitRegion(cards: VenueCard[], lat: number, lng: number): Region {
  const lats = [lat, ...cards.map((c) => c.latitude)];
  const lngs = [lng, ...cards.map((c) => c.longitude)];
  const minLat = Math.min(...lats);
  const maxLat = Math.max(...lats);
  const minLng = Math.min(...lngs);
  const maxLng = Math.max(...lngs);
  return {
    latitude: (minLat + maxLat) / 2,
    longitude: (minLng + maxLng) / 2,
    latitudeDelta: Math.min(0.2, Math.max(0.008, (maxLat - minLat) * 1.6)),
    longitudeDelta: Math.min(0.2, Math.max(0.008, (maxLng - minLng) * 1.6)),
  };
}

export default function MapScreen() {
  const insets = useSafeAreaInsets();
  const filters = useFilters();
  const router = useRouter();
  const mapRef = useRef<MapRef>(null);
  const [items, setItems] = useState<VenueCard[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sheet, setSheet] = useState(false);
  const [picked, setPicked] = useState<VenueCard | null>(null);
  // Niyet/konum değişince önceki isteğin geç gelen cevabı yeni listeyi ezmesin.
  const seq = useRef(0);
  const [region, setRegion] = useState<Region>({
    latitude: filters.lat,
    longitude: filters.lng,
    latitudeDelta: 0.035,
    longitudeDelta: 0.035,
  });

  const nextRequest = useLatestRequest();
  const load = useCallback(async () => {
    const n = ++seq.current;
    const signal = nextRequest();
    // Seçili kart önceki niyetin mekanı olabilir; yeni liste gelirken alt sayfada kalmasın.
    setPicked(null);
    setError(null);
    const radius = clampRadius(filters.radiusMeters);
    const cached = peekRecommend(
      recommendCacheKey({
        lat: filters.lat,
        lng: filters.lng,
        intent: filters.intent,
        radiusMeters: radius,
        openNow: filters.openNow,
        vibes: filters.vibes,
        skip: 0,
        take: 40,
      }),
    );
    if (cached) {
      const next = cached.items.filter((c) => !filters.minRating || (c.nereyeRating ?? 0) >= filters.minRating);
      setItems(next);
      setLoading(false);
    } else {
      setLoading(true);
    }
    try {
      const res = await api.recommend(
        filters.lat,
        filters.lng,
        filters.intent,
        filters.openNow,
        filters.vibes,
        radius,
        0,
        40,
        { signal },
      );
      if (n !== seq.current) {
        return;
      }
      const next = res.items.filter((c) => !filters.minRating || (c.nereyeRating ?? 0) >= filters.minRating);
      setItems(next);
      setLoading(false);
      if (res.hasMore) {
        try {
          const more = await api.recommend(
            filters.lat,
            filters.lng,
            filters.intent,
            filters.openNow,
            filters.vibes,
            radius,
            40,
            110,
            { signal },
          );
          // İkinci dalga eski niyetin mekanlarını yeni listeye eklemesin.
          if (n !== seq.current) {
            return;
          }
          const extra = more.items.filter((c) => !filters.minRating || (c.nereyeRating ?? 0) >= filters.minRating);
          setItems((prev) => {
            const seen = new Set(prev.map((c) => c.placeId));
            return [...prev, ...extra.filter((c) => !seen.has(c.placeId))];
          });
        } catch {
          /* ilk dalga durur */
        }
      }
    } catch (e) {
      if (n !== seq.current) {
        return;
      }
      if (!cached) {
        setError(friendlyError(e));
        setItems([]);
      }
    } finally {
      if (n === seq.current) {
        setLoading(false);
      }
    }
  }, [filters.lat, filters.lng, filters.intent, filters.openNow, filters.vibes, filters.radiusMeters, filters.minRating, nextRequest]);

  useEffect(() => {
    void load();
  }, [load]);

  // Liste gelince harita mekanları sığdıracak kadar yakınlaşır (pinler üst üste binmesin).
  const fitted = useRef<string | null>(null);
  useEffect(() => {
    const key = `${filters.intent}:${filters.lat}:${filters.lng}`;
    if (loading || items.length === 0 || fitted.current === key) {
      return;
    }
    fitted.current = key;
    const next = fitRegion(items.slice(0, 20), filters.lat, filters.lng);
    setRegion(next);
    mapRef.current?.animateToRegion(next, 450);
  }, [items, loading, filters.intent, filters.lat, filters.lng]);

  const visible = useMemo(() => items.filter((c) => inRegion(c, region)), [items, region]);
  const sheetCards = picked ? [picked] : visible;

  // Harita merkezi arama noktasından belirgin uzaklaştıysa "bu bölgede ara" öner.
  const moved = geoDistanceMeters(filters.lat, filters.lng, region.latitude, region.longitude);
  const canSearchHere = !loading && moved > Math.max(400, filters.radiusMeters * 0.5);
  const badge = activeFilterCount(filters);

  // Konum başka ekrandan değişince haritayı oraya taşı.
  useEffect(() => {
    mapRef.current?.animateToRegion({
      latitude: filters.lat,
      longitude: filters.lng,
      latitudeDelta: 0.035,
      longitudeDelta: 0.035,
    });
  }, [filters.lat, filters.lng]);

  async function here() {
    setBusy(true);
    try {
      const fix = await detectLocation();
      if (fix) {
        filters.setLoc(fix.lat, fix.lng, fix.label, false);
      } else {
        toast.info(t('Konum izni kapalı. Semtini listeden seçebilirsin.'));
      }
    } catch {
      toast.error(t('Konum alınamadı. Tekrar dene.'));
    } finally {
      setBusy(false);
    }
  }

  async function searchHere() {
    setPicked(null);
    const label = await reverseLabel(region.latitude, region.longitude);
    filters.setLoc(region.latitude, region.longitude, label === 'Konumun' ? t('Harita merkezi') : label, true);
  }

  function pick(card: VenueCard | null) {
    setPicked(card);
    if (card) {
      // Seçilen mekan alt sayfanın üstünde, görünür alanın ortasında kalsın.
      mapRef.current?.animateToRegion(
        { latitude: card.latitude, longitude: card.longitude, latitudeDelta: region.latitudeDelta, longitudeDelta: region.longitudeDelta },
        350,
      );
    }
  }

  function recenter() {
    mapRef.current?.animateToRegion(
      { latitude: filters.lat, longitude: filters.lng, latitudeDelta: 0.035, longitudeDelta: 0.035 },
      350,
    );
  }

  const mode = currentMapMode();

  return (
    <View style={styles.bg}>
      <MapView
        ref={mapRef}
        style={StyleSheet.absoluteFill}
        initialRegion={region}
        // Üstteki çubuk ve alttaki sayfa harita logosunu örtmesin; atıf alt sayfada yazılı.
        mapPadding={{ top: insets.top + 112, right: 0, bottom: SHEET + 8, left: 0 }}
        showAttribution={false}
        showsCompass={false}
        showsPointsOfInterests={false}
        toolbarEnabled={false}
        onRegionChangeComplete={setRegion}
        onPress={() => setPicked(null)}>
        <MePin latitude={filters.lat} longitude={filters.lng} />
        {visible.map((card) => (
          <VenuePin
            key={card.placeId}
            latitude={card.latitude}
            longitude={card.longitude}
            intent={filters.intent}
            name={card.name}
            selected={picked?.placeId === card.placeId}
            onPress={() => pick(card)}
          />
        ))}
      </MapView>

      <View style={[styles.top, { paddingTop: insets.top + 8 }]} pointerEvents="box-none">
        <View style={styles.bar}>
          <Pressable
            style={styles.chip}
            onPress={() => router.push('/places')}
            accessibilityRole="button"
            accessibilityLabel={t('Konum {p0}, değiştir', { p0: filters.label })}>
            <Ionicons name="location" size={16} color={colors.coral} />
            <Text style={styles.chipText} numberOfLines={1}>
              {filters.label}
            </Text>
            <Ionicons name="chevron-down" size={14} color={colors.muted} style={{ marginLeft: 'auto' }} />
          </Pressable>
          <Pressable
            style={styles.round}
            onPress={() => void here()}
            disabled={busy}
            accessibilityRole="button"
            accessibilityLabel={t('Konumumu kullan')}>
            {busy ? <ActivityIndicator size="small" color={colors.coral} /> : <Ionicons name="navigate" size={18} color={colors.coral} />}
          </Pressable>
          <Pressable
            style={[styles.round, badge > 0 && styles.roundOn]}
            onPress={() => setSheet(true)}
            accessibilityRole="button"
            accessibilityLabel={badge > 0 ? `Filtre, ${badge} etkin` : 'Filtre'}>
            <Ionicons name="options" size={18} color={badge > 0 ? colors.onCoral : colors.ink} />
          </Pressable>
        </View>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.intents}>
          {INTENTS.map((i) => {
            const on = filters.intent === i;
            return (
              <Pressable
                key={i}
                style={[styles.intent, on && styles.intentOn]}
                onPress={() => filters.setIntent(i)}
                accessibilityRole="button"
                accessibilityLabel={intentLabel(i)}
                accessibilityState={{ selected: on }}
                aria-selected={on}>
                <Ionicons name={INTENT_ICONS[i]} size={14} color={on ? colors.onCoral : colors.coral} />
                <Text style={[styles.intentText, on && styles.intentTextOn]}>{intentLabel(i)}</Text>
              </Pressable>
            );
          })}
        </ScrollView>
        {canSearchHere ? (
          <Pressable style={styles.searchHere} onPress={() => void searchHere()} accessibilityRole="button" accessibilityLabel={t('Bu bölgede ara')}>
            <Ionicons name="refresh" size={14} color={colors.onAccent} />
            <Text style={styles.searchHereText}>{t('Bu bölgede ara')}</Text>
          </Pressable>
        ) : null}
      </View>

      {mode !== 'no-key' ? (
        <Pressable
          style={[styles.round, styles.recenter, { bottom: SHEET + 16 }]}
          onPress={recenter}
          accessibilityRole="button"
          accessibilityLabel={t('Arama noktasına dön')}>
          <Ionicons name="locate" size={20} color={colors.coral} />
        </Pressable>
      ) : null}

      <View style={[styles.sheet, { paddingBottom: insets.bottom + 12 }]}>
        <View style={styles.grab} />
        <View style={styles.sheetHead}>
          <View style={{ flex: 1 }}>
            <Text style={styles.sheetTitle} numberOfLines={1}>
              {picked ? picked.name : `${intentLabel(filters.intent)} · ${filters.label}`}
            </Text>
            <Text style={styles.sheetSub}>
              {picked ? t('Haritada seçili') : loading ? t('Aranıyor…') : t('{p0} yer bu görünümde', { p0: visible.length })}
            </Text>
          </View>
          {picked ? (
            <Pressable style={styles.all} onPress={() => setPicked(null)} accessibilityRole="button" accessibilityLabel={t('Tüm mekanları göster')}>
              <Text style={styles.allText}>{t('Tümü')}</Text>
            </Pressable>
          ) : null}
        </View>
        {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}
        {!loading && visible.length === 0 && !error ? <Text style={styles.none}>{emptyMap(filters.intent)}</Text> : null}
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.row}>
          {sheetCards.map((card) => (
            <View key={card.placeId} style={styles.card}>
              <VenueProductCard card={card} width={CARD} intent={filters.intent} loadPhoto />
            </View>
          ))}
        </ScrollView>
        <Text style={styles.attr}>{mapAttribution(mode)}</Text>
      </View>
      <FilterSheet visible={sheet} onClose={() => setSheet(false)} />
      <SearchingOverlay visible={loading && items.length === 0} intent={filters.intent} />
    </View>
  );
}

const SHEET = 396;
const CARD = 176;

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  top: { position: 'absolute', left: 0, right: 0, gap: 10 },
  bar: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 16 },
  chip: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: colors.surface,
    paddingHorizontal: 14,
    minHeight: 48,
    borderRadius: 24,
    ...shadows.float,
  },
  chipText: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 14, flexShrink: 1 },
  round: {
    width: 48,
    height: 48,
    borderRadius: 24,
    backgroundColor: colors.surface,
    alignItems: 'center',
    justifyContent: 'center',
    ...shadows.float,
  },
  roundOn: { backgroundColor: colors.coral },
  intents: { gap: 8, paddingHorizontal: 16, paddingBottom: 4 },
  intent: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    minHeight: 36,
    paddingHorizontal: 12,
    borderRadius: 18,
    backgroundColor: colors.surface,
    ...shadows.card,
  },
  intentOn: { backgroundColor: colors.coral },
  intentText: { color: colors.ink, fontFamily: fonts.sansMed, fontSize: 13 },
  intentTextOn: { color: colors.onCoral, fontFamily: fonts.sansBold },
  searchHere: {
    alignSelf: 'center',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: colors.accent,
    paddingHorizontal: 16,
    minHeight: 40,
    borderRadius: 20,
    ...shadows.float,
  },
  searchHereText: { color: colors.onAccent, fontFamily: fonts.sansBold, fontSize: 13 },
  recenter: { position: 'absolute', right: 16 },
  sheet: {
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
    height: SHEET,
    backgroundColor: colors.surface,
    borderTopLeftRadius: 28,
    borderTopRightRadius: 28,
    paddingTop: 10,
    paddingHorizontal: 16,
    ...shadows.sheet,
  },
  grab: { alignSelf: 'center', width: 40, height: 5, borderRadius: 3, backgroundColor: colors.line, marginBottom: 10 },
  sheetHead: { flexDirection: 'row', alignItems: 'center', gap: 10, marginBottom: 12 },
  sheetTitle: { color: colors.ink, fontFamily: fonts.display, fontSize: 19 },
  sheetSub: { color: colors.muted, fontFamily: fonts.sans, fontSize: 13, marginTop: 2 },
  all: { minHeight: 44, paddingHorizontal: 14, borderRadius: 22, backgroundColor: colors.accentSoft, justifyContent: 'center' },
  allText: { color: colors.accent, fontFamily: fonts.sansBold, fontSize: 13 },
  none: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, lineHeight: 20, paddingBottom: 8 },
  row: { gap: 12, paddingBottom: 12, paddingTop: 2 },
  card: { width: CARD },
  attr: { fontSize: 10, color: colors.muted, marginTop: 'auto' },
});
