import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { SearchingOverlay } from '@/components/SearchingOverlay';
import { VenueProductCard } from '@/components/VenueProductCard';
import { emptySearch, friendlyError } from '@/lib/empty';
import { useFilters } from '@/lib/filters';
import { api } from '@/lib/api';
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
  const [suggest, setSuggest] = useState<{ label: string; latitude: number; longitude: number }[]>([]);
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
          setSuggest(geo.slice(0, 5));
          setEmpty(venues.length === 0 && geo.length === 0);
          setError(null);
          setRecent(await pushRecentSearch(q.trim()));
        } catch (e) {
          if (n !== seq.current) {
            return;
          }
          setError(friendlyError(e));
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
        {suggest.map((s) => (
          <Pressable
            key={`${s.label}-${s.latitude}`}
            style={styles.recentRow}
            onPress={() => filters.setLoc(s.latitude, s.longitude, s.label, true)}
            accessibilityRole="button">
            <Ionicons name="location-outline" size={16} color={colors.coral} />
            <Text style={styles.recentText}>{s.label}</Text>
          </Pressable>
        ))}
        {empty ? <EmptyState title={emptySearch(q.trim())} /> : null}
        <View style={styles.grid}>
          {items.map((card) => (
            <View key={card.placeId} style={styles.cell}>
              <VenueProductCard card={card} intent={filters.intent} />
            </View>
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
  kicker: { color: colors.muted, fontWeight: '700', marginBottom: 8 },
  recentRow: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingVertical: 10, minHeight: 44 },
  recentText: { color: colors.ink, fontWeight: '600' },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  cell: { width: '47%' },
});
