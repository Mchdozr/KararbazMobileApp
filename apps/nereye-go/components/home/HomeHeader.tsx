import { t } from '@/lib/i18n';
import { BrandLogo } from '@/components/BrandLogo';
import { LocationPicker } from '@/components/LocationPicker';
import { api } from '@/lib/api';
import { isEvening, timeGreeting } from '@/lib/copy';
import { detectLocation } from '@/lib/geo';
import { isLocPinned, useFilters } from '@/lib/filters';
import { hydrateSavedPlaces, useSavedPlaces } from '@/lib/saved-places';
import { useSession } from '@/lib/session';
import { colors, fonts, gradients, shadows } from '@/lib/theme';
import { toast } from '@/lib/toast';
import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import { useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/**
 * @param topInset Üst güvenli alan. Liste başlığı olarak kullanıldığında ekran durum çubuğu şeridini kendi çizer
 * ve 0 verir.
 */
export function HomeHeader({ topInset }: { topInset?: number } = {}) {
  const insets = useSafeAreaInsets();
  const top = topInset ?? insets.top;
  const router = useRouter();
  const filters = useFilters();
  const { session } = useSession();
  const saved = useSavedPlaces();
  const [sheet, setSheet] = useState(false);
  const [q, setQ] = useState('');
  const [locating, setLocating] = useState(false);
  const evening = isEvening();

  useEffect(() => {
    void hydrateSavedPlaces(Boolean(session));
  }, [session]);

  useEffect(() => {
    if (filters.pinned) {
      return;
    }
    let alive = true;
    void detectLocation()
      .then((fix) => {
        if (alive && fix && !isLocPinned()) {
          filters.setLoc(fix.lat, fix.lng, fix.label, false);
          if (session) {
            void api.consent('location', true);
          }
        }
      })
      .catch(() => {});
    return () => {
      alive = false;
    };
  }, [filters.pinned, session]);

  function goSearch(text?: string) {
    const query = (text ?? q).trim();
    router.push({ pathname: '/(tabs)/search', params: query ? { q: query } : {} });
  }

  return (
    <View style={styles.outer}>
      <StatusBar style="light" />
      <LinearGradient
        colors={evening ? gradients.evening : gradients.hero}
        style={[styles.hero, { paddingTop: top + 14 }]}>
        <View style={styles.top}>
          <View style={styles.brand}>
            <View style={styles.logoTile}>
              <BrandLogo size={34} />
            </View>
            <Text style={styles.brandName}>{t('Nereye')}</Text>
          </View>
          <View style={styles.locRow}>
            <Pressable
              style={styles.loc}
              onPress={() => setSheet(true)}
              accessibilityRole="button"
              accessibilityLabel={t('Konum {p0}, değiştir', { p0: filters.label })}>
              <Ionicons name="location" size={14} color={colors.white} />
              <Text style={styles.locText} numberOfLines={1}>
                {filters.label}
              </Text>
              <Ionicons name="chevron-down" size={14} color={colors.onDarkMuted} />
            </Pressable>
            <Pressable
              style={styles.gps}
              onPress={() => {
                if (locating) {
                  return;
                }
                setLocating(true);
                void detectLocation()
                  .then((fix) => {
                    if (fix) {
                      filters.setLoc(fix.lat, fix.lng, fix.label, false);
                      toast.success(`Konum: ${fix.label}`);
                      if (session) {
                        void api.consent('location', true);
                      }
                    } else {
                      toast.info(t('Konum izni kapalı. Semtini listeden seçebilirsin.'));
                      setSheet(true);
                    }
                  })
                  .catch(() => toast.error(t('Konum alınamadı. Tekrar dene.')))
                  .finally(() => setLocating(false));
              }}
              accessibilityRole="button"
              accessibilityLabel={t('Konumumu kullan')}
              hitSlop={8}>
              {locating ? <ActivityIndicator size="small" color={colors.white} /> : <Ionicons name="navigate" size={16} color={colors.white} />}
            </Pressable>
          </View>
        </View>
        <Text style={styles.greeting}>{timeGreeting()}</Text>
        {saved.items.length > 0 ? (
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.savedRow}>
            {saved.items.slice(0, 8).map((p) => {
              const on = p.label === filters.label;
              return (
                <Pressable
                  key={p.id}
                  style={[styles.saved, on && styles.savedOn]}
                  onPress={() => filters.setLoc(p.latitude, p.longitude, p.label, true)}
                  accessibilityRole="button"
                  accessibilityLabel={p.label}
                  accessibilityState={{ selected: on }} aria-selected={on}>
                  <Ionicons name={p.kind === 'home' ? 'home' : p.kind === 'work' ? 'briefcase' : 'bookmark'} size={12} color={on ? colors.coral : colors.white} />
                  <Text style={[styles.savedText, on && styles.savedTextOn]}>{p.label}</Text>
                </Pressable>
              );
            })}
          </ScrollView>
        ) : null}
      </LinearGradient>
      {/* Arama kutusu üst menünün kavisli alt kenarına taşar. */}
      <View style={styles.searchWrap}>
        <View style={styles.search}>
          <Ionicons name="search" size={18} color={colors.coral} />
          <TextInput
            style={styles.input}
            placeholder={t('Mekan, semt ya da tür ara')}
            placeholderTextColor={colors.muted}
            value={q}
            onChangeText={setQ}
            onSubmitEditing={() => goSearch()}
            returnKeyType="search"
            accessibilityLabel={t('Mekan ara')}
          />
          {q ? (
            <Pressable onPress={() => setQ('')} hitSlop={10} accessibilityRole="button" accessibilityLabel={t('Aramayı temizle')}>
              <Ionicons name="close-circle" size={18} color={colors.muted} />
            </Pressable>
          ) : (
            <Pressable
              style={styles.go}
              onPress={() => goSearch()}
              accessibilityRole="button"
              accessibilityLabel={t('Aramaya git')}>
              <Ionicons name="arrow-forward" size={16} color={colors.onCoral} />
            </Pressable>
          )}
        </View>
      </View>

      <Modal visible={sheet} animationType="slide" presentationStyle="pageSheet" onRequestClose={() => setSheet(false)}>
        <View style={[styles.sheet, { paddingTop: 16 }]}>
          <View style={styles.sheetHead}>
            <Text style={styles.sheetTitle}>{t('Neredesin?')}</Text>
            <Pressable style={styles.close} onPress={() => setSheet(false)} accessibilityRole="button" accessibilityLabel={t('Kapat')}>
              <Ionicons name="close" size={20} color={colors.ink} />
            </Pressable>
          </View>
          <LocationPicker onPicked={() => setSheet(false)} />
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  outer: { backgroundColor: colors.bg, zIndex: 2 },
  hero: {
    paddingHorizontal: 20,
    paddingBottom: 40,
    borderBottomLeftRadius: 28,
    borderBottomRightRadius: 28,
  },
  top: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 },
  brand: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  logoTile: {
    width: 44,
    height: 44,
    borderRadius: 14,
    backgroundColor: colors.surface,
    alignItems: 'center',
    justifyContent: 'center',
  },
  brandName: { fontFamily: fonts.display, fontSize: 22, color: colors.white, letterSpacing: 0.2 },
  locRow: { flexDirection: 'row', alignItems: 'center', gap: 8, flexShrink: 1 },
  loc: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
    backgroundColor: colors.glassLight,
    borderWidth: 1,
    borderColor: colors.glassLine,
    paddingHorizontal: 12,
    minHeight: 44,
    borderRadius: 999,
    flexShrink: 1,
  },
  locText: { color: colors.white, fontFamily: fonts.sansBold, fontSize: 13, maxWidth: 120 },
  gps: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.glassLight,
    borderWidth: 1,
    borderColor: colors.glassLine,
    alignItems: 'center',
    justifyContent: 'center',
  },
  greeting: {
    fontFamily: fonts.display,
    fontSize: 26,
    lineHeight: 32,
    color: colors.white,
    marginTop: 18,
    marginBottom: 4,
  },
  savedRow: { gap: 8, paddingTop: 12 },
  saved: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
    backgroundColor: colors.glassLight,
    borderWidth: 1,
    borderColor: colors.glassLine,
    paddingHorizontal: 12,
    minHeight: 44,
    borderRadius: 999,
  },
  savedOn: { backgroundColor: colors.surface, borderColor: colors.surface },
  savedText: { color: colors.white, fontFamily: fonts.sansBold, fontSize: 12 },
  savedTextOn: { color: colors.coral },
  searchWrap: { paddingHorizontal: 20, marginTop: -26 },
  search: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    backgroundColor: colors.surface,
    borderRadius: 18,
    paddingLeft: 16,
    paddingRight: 6,
    minHeight: 54,
    ...shadows.float,
  },
  input: { flex: 1, paddingVertical: 14, color: colors.ink, fontSize: 16, minHeight: 44, fontFamily: fonts.sans },
  go: {
    width: 44,
    height: 44,
    borderRadius: 14,
    backgroundColor: colors.coral,
    alignItems: 'center',
    justifyContent: 'center',
  },
  sheet: { flex: 1, backgroundColor: colors.bg, paddingHorizontal: 20 },
  sheetHead: { flexDirection: 'row', alignItems: 'center', marginBottom: 8 },
  sheetTitle: { fontFamily: fonts.display, fontSize: 28, color: colors.ink, flex: 1 },
  close: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
