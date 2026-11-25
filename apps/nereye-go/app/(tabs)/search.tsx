import { t } from '@/lib/i18n';
import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { ScreenHeader } from '@/components/ScreenHeader';
import { GeoSuggestGroup } from '@/components/GeoSuggestRow';
import { VenueCardSkeleton } from '@/components/Skeleton';
import { VenueProductCard } from '@/components/VenueProductCard';
import { api } from '@/lib/api';
import { track } from '@/lib/analytics';
import { INTENTS, intentLabel } from '@/lib/constants';
import { emptySearch, friendlyError } from '@/lib/empty';
import { useFilters } from '@/lib/filters';
import { geoChip, type GeoHit } from '@/lib/geo-hit';
import { haptic } from '@/lib/haptics';
import { clearRecentSearches, loadRecentSearches, pushRecentSearch, removeRecentSearch } from '@/lib/recent-searches';
import { debounceMs, shouldSearch } from '@/lib/search-query';
import { colors, fonts, INTENT_ICONS, INTENT_PASTELS, shadows } from '@/lib/theme';
import { toast } from '@/lib/toast';
import type { VenueCard } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View , type TextInputInstance } from 'react-native';

// Ham Türkçe: çeviri çizimde (dil, modül yüklendikten sonra seçilir).
const IDEAS = ['Kahvaltı', 'Sahil', 'Tatlı', 'Canlı müzik', 'Kitap kafe', 'Manzara'];

export default function Search() {
  const filters = useFilters();
  const router = useRouter();
  const params = useLocalSearchParams<{ q?: string }>();
  const [q, setQ] = useState(params.q ?? '');
  const [items, setItems] = useState<VenueCard[]>([]);
  const [suggest, setSuggest] = useState<GeoHit[]>([]);
  const [recent, setRecent] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);
  const [retry, setRetry] = useState(0);
  const seq = useRef(0);
  const inputRef = useRef<TextInputInstance>(null);

  useEffect(() => {
    void loadRecentSearches().then(setRecent);
  }, []);

  useEffect(() => {
    if (params.q) {
      setQ(params.q);
    }
  }, [params.q]);

  useEffect(() => {
    const term = q.trim();
    const n = ++seq.current;
    if (!shouldSearch(term)) {
      setItems([]);
      setSuggest([]);
      setDone(false);
      setError(null);
      setLoading(false);
      return;
    }
    setLoading(true);
    const t = setTimeout(() => {
      void (async () => {
        try {
          const [venues, geo] = await Promise.all([api.search(term, filters.lat, filters.lng), api.geoSuggest(term)]);
          if (n !== seq.current) {
            return;
          }
          track('search');
          setItems(venues);
          setSuggest(geo.slice(0, 5));
          setDone(true);
          setError(null);
          if (venues.length || geo.length) {
            setRecent(await pushRecentSearch(term));
          }
        } catch (e) {
          if (n === seq.current) {
            setError(friendlyError(e));
          }
        } finally {
          if (n === seq.current) {
            setLoading(false);
          }
        }
      })();
    }, debounceMs());
    return () => clearTimeout(t);
  }, [q, filters.lat, filters.lng, retry]);

  const typing = shouldSearch(q);
  const empty = typing && done && !loading && !error && items.length === 0 && suggest.length === 0;

  return (
    <View style={styles.bg}>
      <ScreenHeader title={t('Ara')} subtitle={t('{p0} çevresinde mekan, semt ya da durak', { p0: filters.label })} overlap={28} />
      {/* Arama kutusu yeşil başlığın kavisli kenarına taşar (ana sayfayla aynı). */}
      <View style={styles.searchWrap}>
        <View style={styles.search}>
          <Ionicons name="search" size={18} color={colors.coral} />
          <TextInput
            ref={inputRef}
            style={styles.input}
            placeholder={t('Mekan, semt ya da durak')}
            placeholderTextColor={colors.muted}
            value={q}
            onChangeText={setQ}
            returnKeyType="search"
            autoCorrect={false}
            autoCapitalize="none"
            accessibilityLabel={t('Arama')}
          />
          {loading ? <ActivityIndicator size="small" color={colors.coral} /> : null}
          {q && !loading ? (
            <Pressable
              onPress={() => {
                setQ('');
                inputRef.current?.focus();
              }}
              hitSlop={10}
              accessibilityRole="button"
              accessibilityLabel={t('Aramayı temizle')}>
              <Ionicons name="close-circle" size={20} color={colors.muted} />
            </Pressable>
          ) : null}
        </View>
      </View>
      <ScrollView
        contentContainerStyle={styles.wrap}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode="on-drag"
        showsVerticalScrollIndicator={false}>
        {error ? (
          <View style={styles.bleed}>
            <ErrorBanner message={error} onRetry={() => setRetry((n) => n + 1)} />
          </View>
        ) : null}

        {!typing ? (
          <>
            {recent.length > 0 ? (
              <View style={styles.block}>
                <View style={styles.kickerRow}>
                  <Text style={styles.kicker}>{t('Son aramalar')}</Text>
                  <Pressable
                    onPress={() => void clearRecentSearches().then(setRecent)}
                    hitSlop={10}
                    accessibilityRole="button"
                    accessibilityLabel={t('Son aramaları temizle')}>
                    <Text style={styles.clear}>{t('Temizle')}</Text>
                  </Pressable>
                </View>
                {recent.map((r) => (
                  <View key={r} style={styles.recentRow}>
                    <Pressable style={styles.recentMain} onPress={() => setQ(r)} accessibilityRole="button" accessibilityLabel={`${r} ara`}>
                      <Ionicons name="time-outline" size={18} color={colors.muted} />
                      <Text style={styles.recentText} numberOfLines={1}>
                        {r}
                      </Text>
                    </Pressable>
                    <Pressable
                      onPress={() => void removeRecentSearch(r).then(setRecent)}
                      hitSlop={10}
                      style={styles.recentX}
                      accessibilityRole="button"
                      accessibilityLabel={t('{p0} aramasını sil', { p0: r })}>
                      <Ionicons name="close" size={16} color={colors.muted} />
                    </Pressable>
                  </View>
                ))}
              </View>
            ) : null}
            <View style={styles.block}>
              <Text style={styles.kicker}>{t('Fikir ver')}</Text>
              <View style={styles.ideas}>
                {IDEAS.map((idea) => (
                  <Pressable
                    key={idea}
                    style={styles.idea}
                    onPress={() => {
                      haptic.tap();
                      setQ(t(idea));
                    }}
                    accessibilityRole="button"
                    accessibilityLabel={t('{p0} ara', { p0: t(idea) })}>
                    <Text style={styles.ideaText}>{t(idea)}</Text>
                  </Pressable>
                ))}
              </View>
            </View>
            <View style={styles.block}>
              <Text style={styles.kicker}>{t('Niyete göre')}</Text>
              <View style={styles.intents}>
                {INTENTS.map((i) => (
                  <Pressable
                    key={i}
                    style={styles.intent}
                    onPress={() => {
                      haptic.tap();
                      filters.setIntent(i);
                      router.push('/(tabs)/discover');
                    }}
                    accessibilityRole="button"
                    accessibilityLabel={t('{p0} mekanlarını listele', { p0: intentLabel(i) })}>
                    <View style={[styles.intentIcon, { backgroundColor: INTENT_PASTELS[i] ?? colors.coralSoft }]}>
                      <Ionicons name={INTENT_ICONS[i]} size={18} color={colors.coral} />
                    </View>
                    <Text style={styles.intentText}>{intentLabel(i)}</Text>
                  </Pressable>
                ))}
              </View>
            </View>
          </>
        ) : null}

        {typing && loading && items.length === 0 && suggest.length === 0 ? (
          <View style={styles.list} accessibilityLabel={t('Aranıyor')}>
            <VenueCardSkeleton />
            <VenueCardSkeleton />
          </View>
        ) : null}

        <GeoSuggestGroup
          title={t('Konumlar')}
          hits={suggest}
          from={{ lat: filters.lat, lng: filters.lng }}
          onPress={(hit) => {
            const label = geoChip(hit);
            filters.setLoc(hit.latitude, hit.longitude, label, true);
            toast.success(`Konum: ${label}`);
            router.push('/(tabs)');
          }}
        />
        {empty ? (
          <EmptyState icon="search-outline" title={emptySearch(q.trim())} body={t('Yazımı kontrol et ya da daha genel bir kelime dene.')} />
        ) : null}
        {items.length ? <Text style={styles.kicker}>Mekanlar · {items.length}</Text> : null}
        <View style={styles.list}>
          {items.map((card) => (
            <VenueProductCard key={card.placeId} card={card} intent={filters.intent} />
          ))}
        </View>
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  searchWrap: { paddingHorizontal: 20, marginTop: -30, zIndex: 3 },
  search: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    backgroundColor: colors.surface,
    borderRadius: 18,
    paddingHorizontal: 16,
    minHeight: 56,
    ...shadows.float,
  },
  input: { flex: 1, paddingVertical: 14, color: colors.ink, fontSize: 16, minHeight: 48, fontFamily: fonts.sans },
  wrap: { padding: 20, paddingTop: 20, paddingBottom: 48 },
  bleed: { marginHorizontal: -20, marginBottom: 12 },
  block: { marginBottom: 24 },
  kickerRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  kicker: {
    color: colors.muted,
    fontFamily: fonts.sansBold,
    marginBottom: 10,
    letterSpacing: 1,
    fontSize: 12,
    textTransform: 'uppercase',
  },
  clear: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 13, marginBottom: 10 },
  recentRow: { flexDirection: 'row', alignItems: 'center' },
  recentMain: { flex: 1, flexDirection: 'row', alignItems: 'center', gap: 10, minHeight: 46 },
  recentText: { color: colors.ink, fontFamily: fonts.sansMed, fontSize: 15, flexShrink: 1 },
  recentX: { width: 44, height: 44, alignItems: 'center', justifyContent: 'center' },
  ideas: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  idea: {
    minHeight: 40,
    paddingHorizontal: 14,
    borderRadius: 20,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    justifyContent: 'center',
  },
  ideaText: { color: colors.ink, fontFamily: fonts.sansMed, fontSize: 14 },
  intents: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between', rowGap: 10 },
  intent: {
    width: '48.2%',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    minHeight: 56,
    paddingHorizontal: 12,
    borderRadius: 16,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
  },
  intentIcon: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: colors.coralSoft,
    alignItems: 'center',
    justifyContent: 'center',
  },
  intentText: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 14 },
  list: { gap: 16 },
});
