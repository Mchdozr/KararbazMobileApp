import { LocationPicker } from '@/components/LocationPicker';
import { api } from '@/lib/api';
import { isEvening, timeGreeting } from '@/lib/copy';
import { detectLocation } from '@/lib/geo';
import { isLocPinned, useFilters } from '@/lib/filters';
import { hydrateSavedPlaces, useSavedPlaces } from '@/lib/saved-places';
import { useSession } from '@/lib/session';
import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

export function HomeHeader() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const filters = useFilters();
  const { session } = useSession();
  const saved = useSavedPlaces();
  const [sheet, setSheet] = useState(false);
  const [q, setQ] = useState('');
  const evening = isEvening();

  useEffect(() => {
    void hydrateSavedPlaces(Boolean(session));
  }, [session]);

  useEffect(() => {
    if (filters.pinned) {
      return;
    }
    let alive = true;
    void detectLocation().then((fix) => {
      if (alive && fix && !isLocPinned()) {
        filters.setLoc(fix.lat, fix.lng, fix.label, false);
        if (session) {
          void api.consent('location', true);
        }
      }
    });
    return () => {
      alive = false;
    };
  }, [filters.pinned, session]);

  function goSearch(text?: string) {
    const query = (text ?? q).trim();
    router.push({ pathname: '/(tabs)/search', params: query ? { q: query } : {} });
  }

  return (
    <View style={[styles.wrap, evening && styles.eveWrap, { paddingTop: insets.top + 10 }]}>
      <StatusBar style={evening ? 'light' : 'dark'} />
      <View style={styles.top}>
        <Text style={[styles.brand, evening && styles.eveText]}>Nereye</Text>
        <View style={styles.locRow}>
          <Pressable
            style={styles.loc}
            onPress={() => setSheet(true)}
            accessibilityRole="button"
            accessibilityLabel={`Konum ${filters.label}`}>
            <Ionicons name="location" size={14} color={colors.coral} />
            <Text style={styles.locText} numberOfLines={1}>
              {filters.label}
            </Text>
            <Ionicons name="chevron-down" size={14} color={colors.ink} />
          </Pressable>
          <Pressable
            style={styles.gps}
            onPress={() => {
              void detectLocation().then((fix) => {
                if (fix) {
                  filters.setLoc(fix.lat, fix.lng, fix.label, false);
                  if (session) {
                    void api.consent('location', true);
                  }
                } else {
                  setSheet(true);
                }
              });
            }}
            accessibilityRole="button"
            accessibilityLabel="Konumumu kullan"
            hitSlop={8}>
            <Ionicons name="navigate" size={16} color={colors.coral} />
          </Pressable>
        </View>
      </View>
      <Text style={[styles.line, evening && styles.eveMuted]}>{timeGreeting()}</Text>
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
                accessibilityState={{ selected: on }}>
                <Ionicons name={p.kind === 'home' ? 'home' : p.kind === 'work' ? 'briefcase' : 'bookmark'} size={12} color={on ? colors.white : colors.coral} />
                <Text style={[styles.savedText, on && styles.savedTextOn]}>{p.label}</Text>
              </Pressable>
            );
          })}
        </ScrollView>
      ) : null}
      <View style={styles.search}>
        <Ionicons name="search" size={18} color={colors.muted} />
        <TextInput
          style={styles.input}
          placeholder="Kahve, kafe, sahil…"
          placeholderTextColor={colors.muted}
          value={q}
          onChangeText={setQ}
          onSubmitEditing={() => goSearch()}
          returnKeyType="search"
          accessibilityLabel="Mekan ara"
        />
      </View>

      <Modal visible={sheet} animationType="slide" presentationStyle="pageSheet" onRequestClose={() => setSheet(false)}>
        <View style={[styles.sheet, { paddingTop: 16 }]}>
          <View style={styles.sheetHead}>
            <Text style={styles.sheetTitle}>Neredesin?</Text>
            <Pressable style={styles.close} onPress={() => setSheet(false)} accessibilityRole="button" accessibilityLabel="Kapat">
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
  wrap: {
    backgroundColor: colors.bg,
    paddingHorizontal: 20,
    paddingBottom: 8,
  },
  eveWrap: { backgroundColor: colors.espresso },
  top: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  brand: {
    fontFamily: fonts.display,
    fontSize: 34,
    color: colors.ink,
    fontWeight: '700',
    letterSpacing: -0.6,
  },
  eveText: { color: colors.white },
  locRow: { flexDirection: 'row', alignItems: 'center', gap: 8, maxWidth: '58%' },
  loc: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: colors.surface,
    paddingHorizontal: 10,
    paddingVertical: 8,
    minHeight: 44,
    borderRadius: 999,
    borderWidth: 1,
    borderColor: colors.line,
    flexShrink: 1,
  },
  locText: { color: colors.ink, fontWeight: '700', fontSize: 13, maxWidth: 120 },
  gps: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    alignItems: 'center',
    justifyContent: 'center',
  },
  line: { color: colors.muted, marginTop: 4, marginBottom: 10, fontSize: 15 },
  eveMuted: { color: colors.gold },
  savedRow: { gap: 8, paddingBottom: 12 },
  saved: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    paddingHorizontal: 10,
    paddingVertical: 7,
    minHeight: 44,
    borderRadius: 999,
  },
  savedOn: { backgroundColor: colors.espresso, borderColor: colors.espresso },
  savedText: { color: colors.ink, fontWeight: '700', fontSize: 12 },
  savedTextOn: { color: colors.white },
  search: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    backgroundColor: colors.surface,
    borderRadius: 16,
    paddingHorizontal: 14,
    borderWidth: 1,
    borderColor: colors.line,
  },
  input: { flex: 1, paddingVertical: 14, color: colors.ink, fontSize: 16, minHeight: 44 },
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
