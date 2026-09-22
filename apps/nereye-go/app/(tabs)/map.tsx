import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { FilterSheet } from '@/components/FilterSheet';
import { SearchingOverlay } from '@/components/SearchingOverlay';
import { VenueProductCard } from '@/components/VenueProductCard';
import { emptyMap, friendlyError } from '@/lib/empty';
import { detectLocation } from '@/lib/geo';
import { useFilters } from '@/lib/filters';
import { api } from '@/lib/api';
import { colors, fonts } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import MapView, { Marker, Region, UrlTile } from 'react-native-maps';
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
  const mapRef = useRef<MapView>(null);
  const [items, setItems] = useState<VenueCard[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sheet, setSheet] = useState(false);
  const [picked, setPicked] = useState<VenueCard | null>(null);
  const [region, setRegion] = useState<Region>({
    latitude: filters.lat,
    longitude: filters.lng,
    latitudeDelta: 0.035,
    longitudeDelta: 0.035,
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.recommend(
        filters.lat,
        filters.lng,
        filters.intent,
        filters.openNow,
        filters.vibes,
        filters.radiusMeters,
        0,
        40,
      );
      const next = res.items.filter((c) => !filters.minRating || (c.nereyeRating ?? 0) >= filters.minRating);
      setItems(next);
    } catch (e) {
      setError(friendlyError(e));
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [filters.lat, filters.lng, filters.intent, filters.openNow, filters.vibes, filters.radiusMeters, filters.minRating]);

  useEffect(() => {
    void load();
  }, [load]);

  const visible = useMemo(() => items.filter((c) => inRegion(c, region)), [items, region]);
  const sheetCards = picked ? [picked] : visible;

  async function here() {
    setBusy(true);
    try {
      const fix = await detectLocation();
      if (fix) {
        filters.setLoc(fix.lat, fix.lng, fix.label, false);
        mapRef.current?.animateToRegion({
          latitude: fix.lat,
          longitude: fix.lng,
          latitudeDelta: 0.035,
          longitudeDelta: 0.035,
        });
      }
    } finally {
      setBusy(false);
    }
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
          onPress={() => setSheet(true)}
          accessibilityRole="button"
          accessibilityLabel={`Konum ${filters.label}`}>
          <Ionicons name="location" size={14} color={colors.coral} />
          <Text style={styles.chipText} numberOfLines={1}>
            {filters.label}
          </Text>
        </Pressable>
        <Pressable style={styles.here} onPress={() => void here()} disabled={busy} accessibilityRole="button" accessibilityLabel="Konumumu kullan">
          <Ionicons name="navigate" size={16} color={colors.white} />
          <Text style={styles.hereText}>{busy ? 'Alınıyor…' : 'GPS'}</Text>
        </Pressable>
        <Pressable style={styles.filter} onPress={() => setSheet(true)} accessibilityRole="button" accessibilityLabel="Filtre">
          <Ionicons name="options" size={16} color={colors.ink} />
        </Pressable>
      </View>
      <View style={[styles.sheet, { paddingBottom: insets.bottom + 12 }]}>
        {error ? <ErrorBanner message={error} onRetry={() => void load()} /> : null}
        <Text style={styles.sub}>
          {filters.label} · {visible.length} yer
        </Text>
        {!loading && visible.length === 0 && !error ? <EmptyState title={emptyMap(filters.intent)} /> : null}
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.row}>
          {sheetCards.map((card) => (
            <View key={card.placeId} style={styles.card}>
              <VenueProductCard card={card} width={168} intent={filters.intent} />
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
    paddingTop: 14,
    paddingHorizontal: 16,
    maxHeight: 320,
    borderWidth: 1,
    borderColor: colors.line,
  },
  sub: { color: colors.muted, fontWeight: '700', marginBottom: 10 },
  row: { gap: 12, paddingBottom: 4 },
  card: { width: 168 },
  attr: { marginTop: 8, fontSize: 11, color: colors.muted },
});
