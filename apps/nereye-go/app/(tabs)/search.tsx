import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { SearchingOverlay } from '@/components/SearchingOverlay';
import { VenueProductCard } from '@/components/VenueProductCard';
import { emptySearch, friendlyError } from '@/lib/empty';
import { useFilters } from '@/lib/filters';
import { GeoSuggestGroup } from '@/components/GeoSuggestRow';
import { api } from '@/lib/api';
import { geoChip, type GeoHit } from '@/lib/geo-hit';
import { loadRecentSearches, pushRecentSearch } from '@/lib/recent-searches';
import { debounceMs, shouldSearch } from '@/lib/search-query';
import { colors, fonts } from '@/lib/theme';
import type { VenueCard } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { useLocalSearchParams } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

export default function Search() {
  const insets = useSafeAreaInsets();
  const filters = useFilters();
  const params = useLocalSearchParams<{ q?: string }>();
  const [q, setQ] = useState(params.q ?? '');
  const [items, setItems] = useState<VenueCard[]>([]);
  const [suggest, setSuggest] = useState<GeoHit[]>([]);
  const [recent, setRecent] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [empty, setEmpty] = useState(false);
  const seq = useRef(0);

  useEffect(() => {
    void loadRecentSearches().then(setRecent);
  }, []);

  useEffect(() => {
    if (params.q && params.q !== q) {
      setQ(params.q);
    }
  }, [params.q]);

  useEffect(() => {
    if (!shouldSearch(q)) {
      setItems([]);
      setSuggest([]);
      setEmpty(false);
      setError(null);
      return;
    }
    const n = ++seq.current;
    const t = setTimeout(() => {
      void (async () => {
        setLoading(true);
        try {
          const [venues, geo] = await Promise.all([
            api.search(q.trim(), filters.lat, filters.lng),
            api.geoSuggest(q.trim()),
          ]);
          if (n !== seq.current) {
            return;
          }
          setItems(venues);
          setSuggest(geo.slice(0, 8));
          setEmpty(venues.length === 0 && geo.length === 0);
          setError(null);
          setRecent(await pushRecentSearch(q.trim()));
        } catch (e) {
          if (n !== seq.current) {
            return;
          }
          setError(friendlyError(e));
          setItems([]);
          setSuggest([]);
        } finally {
          if (n === seq.current) {
            setLoading(false);
          }
        }
      })();
    }, debounceMs());
    return () => clearTimeout(t);
  }, [q, filters.lat, filters.lng]);

  return (
    <View style={styles.bg}>
      <View style={[styles.header, { paddingTop: insets.top + 8 }]}>
        <Text style={styles.h}>Ara</Text>
        <View style={styles.search}>
          <Ionicons name="search" size={18} color={colors.muted} />
          <TextInput
            style={styles.input}
            placeholder="Petra, Çiya, Moda…"
            placeholderTextColor={colors.muted}
            value={q}
            onChangeText={setQ}
            returnKeyType="search"
            autoCorrect={false}
            accessibilityLabel="Arama"
          />
        </View>
      </View>
      <ScrollView contentContainerStyle={styles.wrap} keyboardShouldPersistTaps="handled">
        {error ? <ErrorBanner message={error} onRetry={() => setQ((s) => `${s} `)} /> : null}
        {!shouldSearch(q) && recent.length > 0 ? (
          <View style={styles.recent}>
            <Text style={styles.kicker}>Son aramalar</Text>
            {recent.map((r) => (
              <Pressable key={r} style={styles.recentRow} onPress={() => setQ(r)} accessibilityRole="button">
                <Ionicons name="time-outline" size={16} color={colors.muted} />
                <Text style={styles.recentText}>{r}</Text>
              </Pressable>
            ))}
          </View>
        ) : null}
        <GeoSuggestGroup
          title="Konumlar"
          hits={suggest}
          from={{ lat: filters.lat, lng: filters.lng }}
          onPress={(hit) => filters.setLoc(hit.latitude, hit.longitude, geoChip(hit), true)}
        />
        {empty ? <EmptyState title={emptySearch(q.trim())} /> : null}
        {items.length ? <Text style={styles.kicker}>Mekanlar</Text> : null}
        <View style={styles.list}>
          {items.map((card) => (
            <VenueProductCard key={card.placeId} card={card} intent={filters.intent} />
          ))}
        </View>
      </ScrollView>
      <SearchingOverlay visible={loading} intent={filters.intent} query={q} />
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  header: { paddingHorizontal: 20, paddingBottom: 12 },
  h: { fontFamily: fonts.display, fontSize: 32, color: colors.ink, marginBottom: 12 },
  search: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    backgroundColor: colors.surface,
    borderRadius: 16,
    paddingHorizontal: 14,
    borderWidth: 1,
    borderColor: colors.line,
  },
  input: { flex: 1, paddingVertical: 14, color: colors.ink, fontSize: 16, minHeight: 44 },
  wrap: { padding: 20, paddingBottom: 48 },
  recent: { marginBottom: 12, gap: 4 },
  kicker: {
    color: colors.muted,
    fontWeight: '700',
    marginBottom: 10,
    letterSpacing: 1,
    fontSize: 12,
    textTransform: 'uppercase',
  },
  recentRow: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingVertical: 10, minHeight: 44 },
  recentText: { color: colors.ink, fontWeight: '600' },
  list: { gap: 16 },
});
