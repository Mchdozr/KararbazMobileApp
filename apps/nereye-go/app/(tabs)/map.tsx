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
import { peekRecommend, recommendCacheKey } from '@/lib/recommend-cache';
import { colors, fonts } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import MapView, { Marker, UrlTile, type Region } from '@/components/Map';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

function inRegion(card: VenueCard, region: Region) {
  const latPad = region.latitudeDelta / 2;
  const lngPad = region.longitudeDelta / 2;
  return (
    Math.abs(card.latitude - region.latitude) <= latPad &&
    Math.abs(card.longitude - region.longitude) <= lngPad
  );
}

export default function MapScreen() {
  const insets = useSafeAreaInsets();
  const filters = useFilters();
  const router = useRouter();
  const mapRef = useRef<MapView>(null);
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

  const load = useCallback(async () => {
    const n = ++seq.current;
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
  }, [filters.lat, filters.lng, filters.intent, filters.openNow, filters.vibes, filters.radiusMeters, filters.minRating]);

  useEffect(() => {
    void load();
  }, [load]);

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
        toast.info('Konum izni kapalı. Semtini listeden seçebilirsin.');
      }
    } catch {
      toast.error('Konum alınamadı. Tekrar dene.');
    } finally {
      setBusy(false);
    }
  }

  async function searchHere() {
    setPicked(null);
    const label = await reverseLabel(region.latitude, region.longitude);
    filters.setLoc(region.latitude, region.longitude, label === 'Konumun' ? 'Harita merkezi' : label, true);
  }

  return (
    <View style={styles.bg}>
      <MapView
        ref={mapRef}
        style={StyleSheet.absoluteFill}
        initialRegion={region}
        onRegionChangeComplete={setRegion}
        onPress={() => setPicked(null)}>
        <UrlTile urlTemplate="https://tile.openstreetmap.org/{z}/{x}/{y}.png" maximumZ={19} />
        <Marker coordinate={{ latitude: filters.lat, longitude: filters.lng }} title="Sen" pinColor={colors.coral} />
        {visible.map((card) => (
          <Marker
            key={card.placeId}
            coordinate={{ latitude: card.latitude, longitude: card.longitude }}
            pinColor={picked?.placeId === card.placeId ? colors.coral : colors.espresso}
            onPress={() => setPicked(card)}
          />
        ))}
      </MapView>
      <View style={[styles.top, { paddingTop: insets.top + 8 }]}>
        <Pressable
          style={styles.chip}
          onPress={() => router.push('/places')}
          accessibilityRole="button"
          accessibilityLabel={`Konum ${filters.label}, değiştir`}>
          <Ionicons name="location" size={14} color={colors.coral} />
          <Text style={styles.chipText} numberOfLines={1}>
            {filters.label}
          </Text>
          <Ionicons name="chevron-down" size={14} color={colors.ink} style={{ marginLeft: 'auto' }} />
        </Pressable>
        <Pressable style={styles.here} onPress={() => void here()} disabled={busy} accessibilityRole="button" accessibilityLabel="Konumumu kullan">
          <Ionicons name="navigate" size={16} color={colors.white} />
          <Text style={styles.hereText}>{busy ? 'Alınıyor…' : 'GPS'}</Text>
        </Pressable>
        <Pressable
          style={[styles.filter, badge > 0 && styles.filterOn]}
          onPress={() => setSheet(true)}
          accessibilityRole="button"
          accessibilityLabel={badge > 0 ? `Filtre, ${badge} etkin` : 'Filtre'}>
          <Ionicons name="options" size={16} color={badge > 0 ? colors.white : colors.ink} />
        </Pressable>
      </View>
      {canSearchHere ? (
        <Pressable
          style={[styles.searchHere, { top: insets.top + 64 }]}
          onPress={() => void searchHere()}
          accessibilityRole="button"
          accessibilityLabel="Bu bölgede ara">
          <Ionicons name="refresh" size={14} color={colors.white} />
          <Text style={styles.searchHereText}>Bu bölgede ara</Text>
        </Pressable>
      ) : null}
      <View style={[styles.sheet, { paddingBottom: insets.bottom + 12 }]}>
        {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}
        <View style={styles.grab} />
        <Text style={styles.sub}>
          {picked ? picked.name : `${filters.label} · ${visible.length} yer`}
        </Text>
        {!loading && visible.length === 0 && !error ? (
          <Text style={styles.none}>{emptyMap(filters.intent)}</Text>
        ) : null}
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.row}>
          {sheetCards.map((card) => (
            <View key={card.placeId} style={styles.card}>
              <VenueProductCard card={card} width={168} intent={filters.intent} loadPhoto />
            </View>
          ))}
        </ScrollView>
        <Text style={styles.attr}>© OpenStreetMap</Text>
      </View>
      <FilterSheet visible={sheet} onClose={() => setSheet(false)} />
      <SearchingOverlay visible={loading} intent={filters.intent} />
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  top: {
    position: 'absolute',
    left: 16,
    right: 16,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  chip: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: colors.surface,
    paddingHorizontal: 12,
    minHeight: 44,
    borderRadius: 999,
    borderWidth: 1,
    borderColor: colors.line,
  },
  chipText: { color: colors.ink, fontWeight: '700', fontSize: 13, flexShrink: 1 },
  here: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: colors.coral,
    paddingHorizontal: 12,
    minHeight: 44,
    borderRadius: 999,
  },
  hereText: { color: colors.white, fontWeight: '800', fontSize: 13 },
  filter: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  sheet: {
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: colors.surface,
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    paddingTop: 10,
    paddingHorizontal: 16,
    maxHeight: 340,
    borderWidth: 1,
    borderColor: colors.line,
  },
  filterOn: { backgroundColor: colors.coral, borderColor: colors.coral },
  searchHere: {
    position: 'absolute',
    alignSelf: 'center',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: colors.espresso,
    paddingHorizontal: 14,
    minHeight: 40,
    borderRadius: 20,
    shadowColor: '#04382b',
    shadowOpacity: 0.25,
    shadowRadius: 10,
    shadowOffset: { width: 0, height: 4 },
    elevation: 6,
  },
  searchHereText: { color: colors.white, fontFamily: fonts.sansBold, fontSize: 13 },
  grab: { alignSelf: 'center', width: 36, height: 4, borderRadius: 2, backgroundColor: colors.line, marginBottom: 10 },
  none: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, lineHeight: 20, paddingBottom: 8 },
  sub: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 14, marginBottom: 10 },
  row: { gap: 12, paddingBottom: 4 },
  card: { width: 168 },
  attr: { marginTop: 8, fontSize: 11, color: colors.muted },
});
