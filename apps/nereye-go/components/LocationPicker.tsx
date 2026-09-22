import { GeoSuggestGroup } from '@/components/GeoSuggestRow';
import { api } from '@/lib/api';
import { QUICK_PLACES } from '@/lib/copy';
import { geoChip, type GeoHit } from '@/lib/geo-hit';
import { detectLocation } from '@/lib/geo';
import { useFilters } from '@/lib/filters';
import { hydrateSavedPlaces, useSavedPlaces } from '@/lib/saved-places';
import { useSession } from '@/lib/session';
import { colors } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';

export function LocationPicker({ onPicked }: { onPicked: () => void }) {
  const filters = useFilters();
  const { session } = useSession();
  const saved = useSavedPlaces();
  const [q, setQ] = useState('');
  const [hits, setHits] = useState<GeoHit[]>([]);
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<string | null>(null);

  useEffect(() => {
    void hydrateSavedPlaces(Boolean(session));
  }, [session]);

  useEffect(() => {
    const t = q.trim();
    if (t.length < 2) {
      setHits([]);
      return;
    }
    const id = setTimeout(() => {
      void api.geoSuggest(t).then(setHits);
    }, 280);
    return () => clearTimeout(id);
  }, [q]);

  function pick(lat: number, lng: number, label: string, pinned = true) {
    filters.setLoc(lat, lng, label, pinned);
    onPicked();
  }

  async function here() {
    setBusy(true);
    try {
      const fix = await detectLocation();
      if (fix) {
        pick(fix.lat, fix.lng, fix.label, false);
      } else {
        setNote('Konum izni yok. Mahalle yaz veya listeden seç.');
      }
    } finally {
      setBusy(false);
    }
  }

  function saveHere(kind: 'home' | 'work' | 'custom') {
    const label = kind === 'home' ? 'Ev' : kind === 'work' ? 'İş' : filters.label;
    void saved.add({ label, kind, latitude: filters.lat, longitude: filters.lng }, Boolean(session));
    setNote(`${label} kaydedildi.`);
  }

  return (
    <ScrollView style={{ flex: 1 }} contentContainerStyle={styles.wrap} keyboardShouldPersistTaps="handled">
      <Text style={styles.sub}>Mahalle, ilçe veya durak yaz. Sonuçlarda semt ayrıntısı görünür.</Text>
      <View style={styles.search}>
        <Ionicons name="search" size={18} color={colors.muted} />
        <TextInput
          style={styles.input}
          placeholder="Esenler, Tuna, Moda…"
          placeholderTextColor={colors.muted}
          value={q}
          onChangeText={setQ}
          autoCorrect={false}
          returnKeyType="search"
        />
      </View>
      <Pressable style={styles.gps} onPress={() => void here()} disabled={busy}>
        {busy ? <ActivityIndicator color={colors.white} /> : <Ionicons name="navigate" size={16} color={colors.white} />}
        <Text style={styles.gpsText}>Konumum</Text>
      </Pressable>

      {saved.items.length > 0 ? (
        <>
          <Text style={styles.kicker}>Kayıtlı yerlerin</Text>
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chips}>
            {saved.items.map((p) => (
              <Pressable
                key={p.id}
                style={styles.chip}
                onPress={() => pick(p.latitude, p.longitude, p.label)}
                onLongPress={() =>
                  Alert.alert(p.label, 'Kayıttan silinsin mi?', [
                    { text: 'Vazgeç', style: 'cancel' },
                    { text: 'Sil', style: 'destructive', onPress: () => void saved.remove(p.id, Boolean(session)) },
                  ])
                }>
                <Ionicons
                  name={p.kind === 'home' ? 'home' : p.kind === 'work' ? 'briefcase' : 'bookmark'}
                  size={14}
                  color={colors.coral}
                />
                <Text style={styles.chipText}>{p.label}</Text>
              </Pressable>
            ))}
          </ScrollView>
        </>
      ) : null}

      <Text style={styles.kicker}>İstanbul’dan seç</Text>
      <View style={styles.grid}>
        {QUICK_PLACES.map((p) => (
          <Pressable key={p.label} style={styles.quick} onPress={() => pick(p.lat, p.lng, p.label)}>
            <Text style={styles.quickText}>{p.label}</Text>
          </Pressable>
        ))}
      </View>

      {hits.length > 0 ? (
        <GeoSuggestGroup
          title="Sonuçlar"
          hits={hits}
          from={{ lat: filters.lat, lng: filters.lng }}
          onPress={(hit) => pick(hit.latitude, hit.longitude, geoChip(hit))}
        />
      ) : q.trim().length >= 2 ? (
        <Text style={styles.empty}>Mahalle, ilçe veya durak yaz — aynı isimli yerler alt satırda ayrılır.</Text>
      ) : null}

      {note ? <Text style={styles.note}>{note}</Text> : null}

      <Text style={styles.kicker}>Şu anki konumu kaydet</Text>
      <View style={styles.saveRow}>
        <Pressable style={styles.saveBtn} onPress={() => saveHere('home')}>
          <Ionicons name="home-outline" size={16} color={colors.ink} />
          <Text style={styles.saveText}>Ev</Text>
        </Pressable>
        <Pressable style={styles.saveBtn} onPress={() => saveHere('work')}>
          <Ionicons name="briefcase-outline" size={16} color={colors.ink} />
          <Text style={styles.saveText}>İş</Text>
        </Pressable>
        <Pressable style={styles.saveBtn} onPress={() => saveHere('custom')}>
          <Ionicons name="bookmark-outline" size={16} color={colors.ink} />
          <Text style={styles.saveText}>{filters.label}</Text>
        </Pressable>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { paddingBottom: 36 },
  sub: { color: colors.muted, marginBottom: 16, fontSize: 15 },
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
  input: { flex: 1, paddingVertical: 14, color: colors.ink, fontSize: 16 },
  gps: {
    marginTop: 12,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    backgroundColor: colors.coral,
    paddingVertical: 14,
    borderRadius: 16,
  },
  gpsText: { color: colors.white, fontWeight: '800', fontSize: 16 },
  kicker: {
    marginTop: 22,
    marginBottom: 10,
    color: colors.coral,
    fontWeight: '700',
    letterSpacing: 1.1,
    fontSize: 11,
    textTransform: 'uppercase',
  },
  chips: { gap: 8, paddingBottom: 4 },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    paddingHorizontal: 12,
    paddingVertical: 10,
    borderRadius: 999,
  },
  chipText: { color: colors.ink, fontWeight: '700', fontSize: 13 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  quick: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    paddingHorizontal: 14,
    paddingVertical: 10,
    borderRadius: 14,
  },
  quickText: { color: colors.ink, fontWeight: '700', fontSize: 13 },
  empty: { color: colors.muted, marginTop: 8, lineHeight: 20 },
  note: { color: colors.sage, marginTop: 12, fontWeight: '600' },
  saveRow: { flexDirection: 'row', gap: 8 },
  saveBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    paddingVertical: 12,
    borderRadius: 14,
  },
  saveText: { color: colors.ink, fontWeight: '700', fontSize: 12 },
});
