import { VenueImage } from '@/components/VenueImage';
import { RadiusPicker } from '@/components/RadiusPicker';
import { INTENTS, VIBES, intentLabel, vibeLabel } from '@/lib/constants';
import { clampRadius, radiusLabel as formatRadius } from '@/lib/radius';
import { api, mediaUrl } from '@/lib/api';
import { useFilters } from '@/lib/filters';
import { hydrateSavedPlaces, useSavedPlaces } from '@/lib/saved-places';
import { INTENT_PHOTOS } from '@/lib/photos';
import { useSession } from '@/lib/session';
import { colors, fonts } from '@/lib/theme';
import type { Preferences, VenueCard } from '@/lib/types';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import * as ImagePicker from 'expo-image-picker';
import { LinearGradient } from 'expo-linear-gradient';
import { useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useMemo, useState } from 'react';
import { Alert, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

const EMPTY: Preferences = { defaultRadiusMeters: 1500, intents: [], vibes: [] };

function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  const a = parts[0]?.[0] ?? 'N';
  const b = parts.length > 1 ? parts[1][0] : '';
  return (a + b).toUpperCase();
}

export default function Profile() {
  const insets = useSafeAreaInsets();
  const { session, signOut, patchSession } = useSession();
  const filters = useFilters();
  const savedPlaces = useSavedPlaces();
  const router = useRouter();
  const [prefs, setPrefs] = useState<Preferences | null>(null);
  const [saving, setSaving] = useState(false);
  const [savedOk, setSavedOk] = useState(false);
  const [saved, setSaved] = useState<VenueCard[]>([]);

  useEffect(() => {
    if (!session) {
      setPrefs(EMPTY);
      setSaved([]);
      return;
    }
    void api.preferences().then((p) =>
      setPrefs({ ...p, defaultRadiusMeters: clampRadius(p.defaultRadiusMeters) }),
    );
    void api.favorites().then(setSaved);
    void hydrateSavedPlaces(true);
  }, [session]);

  const heroIntent = prefs?.intents[0] ?? 'AcikHava';
  const name = session?.displayName ?? 'Gezgin';
  const role = session?.roles.includes('Admin') ? 'Admin' : 'Gezgin';
  const selectedCount = (prefs?.intents.length ?? 0) + (prefs?.vibes.length ?? 0);

  const radiusLabel = useMemo(
    () => formatRadius(prefs?.defaultRadiusMeters ?? 1500),
    [prefs?.defaultRadiusMeters],
  );

  function toggleIntent(intent: string) {
    if (!prefs) return;
    const on = prefs.intents.includes(intent);
    setPrefs({
      ...prefs,
      intents: on ? prefs.intents.filter((x) => x !== intent) : [...prefs.intents, intent],
    });
    setSavedOk(false);
  }

  function toggleVibe(vibe: (typeof VIBES)[number]) {
    if (!prefs) return;
    const on = prefs.vibes.includes(vibe);
    setPrefs({
      ...prefs,
      vibes: on ? prefs.vibes.filter((x) => x !== vibe) : [...prefs.vibes, vibe],
    });
    setSavedOk(false);
  }

  async function save() {
    if (!prefs) return;
    setSaving(true);
    try {
      const saved = await api.savePreferences(prefs);
      if (!saved) {
        Alert.alert('Oturum', 'Tercih için tekrar giriş yap.');
        return;
      }
      setPrefs(saved);
      filters.setRadius(saved.defaultRadiusMeters);
      filters.setVibes(saved.vibes);
      if (saved.intents[0]) {
        filters.setIntent(saved.intents[0]);
      }
      setSavedOk(true);
    } finally {
      setSaving(false);
    }
  }

  async function pickAvatar() {
    if (!session) {
      router.push('/auth');
      return;
    }
    const perm = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (perm.status !== 'granted') {
      Alert.alert('Galeri', 'Profil fotoğrafı için izin gerekir.');
      return;
    }
    const picked = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.72,
      base64: true,
    });
    const asset = picked.assets?.[0];
    if (picked.canceled || !asset?.base64) {
      return;
    }
    const res = await api.uploadAvatar(asset.base64);
    if (res?.avatarUrl) {
      await patchSession({ avatarUrl: res.avatarUrl });
    } else {
      Alert.alert('Fotoğraf', 'Yüklenemedi, tekrar dene.');
    }
  }

  function confirmDelete() {
    Alert.alert('Hesabı sil', 'Yorumların ve kayıtların silinir. Geri alınmaz.', [
      { text: 'Vazgeç', style: 'cancel' },
      {
        text: 'Sil',
        style: 'destructive',
        onPress: () => {
          void (async () => {
            try {
              await api.deleteMe();
            } catch {
              /* oturum yoksa da çık */
            }
            await signOut();
            router.replace('/auth');
          })();
        },
      },
    ]);
  }

  return (
    <ScrollView style={styles.bg} contentContainerStyle={styles.wrap} showsVerticalScrollIndicator={false}>
      <StatusBar style="light" />
      <View style={styles.hero}>
        <Image source={INTENT_PHOTOS[heroIntent] ?? INTENT_PHOTOS.AcikHava} style={StyleSheet.absoluteFill} contentFit="cover" />
        <LinearGradient colors={['rgba(6,78,59,0.15)', 'rgba(6,78,59,0.78)']} style={StyleSheet.absoluteFill} />
        <View style={[styles.heroCopy, { paddingTop: insets.top + 12 }]}>
          <Text style={styles.kicker}>Profil</Text>
          <Text style={styles.heroLine}>Karar tarzın.</Text>
        </View>
      </View>

      <View style={styles.identity}>
        <Pressable style={styles.avatar} onPress={() => void pickAvatar()}>
          {session?.avatarUrl ? (
            <Image source={{ uri: mediaUrl(session.avatarUrl) ?? undefined }} style={styles.avatarImg} contentFit="cover" />
          ) : (
            <Text style={styles.avatarText}>{initials(name)}</Text>
          )}
          <View style={styles.cam}>
            <Ionicons name="camera" size={12} color={colors.white} />
          </View>
        </Pressable>
        <Text style={styles.name}>{name}</Text>
        <View style={styles.badgeRow}>
          <View style={styles.badge}>
            <Text style={styles.badgeText}>{role}</Text>
          </View>
          <Pressable style={styles.loc} onPress={() => router.push('/places')}>
            <Ionicons name="location" size={13} color={colors.coral} />
            <Text style={styles.locText}>{filters.label}</Text>
            <Ionicons name="chevron-down" size={13} color={colors.ink} />
          </Pressable>
        </View>
        <Text style={styles.meta}>
          {selectedCount > 0 ? `${prefs?.intents.length ?? 0} niyet · ${radiusLabel}` : 'Niyetlerini seç, öneriler kişiselleşir.'}
        </Text>
      </View>

      {savedPlaces.items.length > 0 ? (
        <View style={styles.card}>
          <Text style={styles.sectionKicker}>Yerlerim</Text>
          <Text style={styles.sectionTitle}>Kayıtlı konumlar</Text>
          <View style={styles.chips}>
            {savedPlaces.items.map((p) => (
              <Pressable
                key={p.id}
                style={styles.chip}
                onPress={() => {
                  filters.setLoc(p.latitude, p.longitude, p.label, true);
                  router.push('/(tabs)');
                }}>
                <Text style={styles.chipOffText}>{p.label}</Text>
              </Pressable>
            ))}
            <Pressable style={styles.chip} onPress={() => router.push('/places')}>
              <Text style={styles.chipOffText}>+ Ekle</Text>
            </Pressable>
          </View>
        </View>
      ) : (
        <Pressable style={[styles.card, { marginTop: 16 }]} onPress={() => router.push('/places')}>
          <Text style={styles.sectionKicker}>Yerlerim</Text>
          <Text style={styles.sectionTitle}>Ev, iş, mahalle</Text>
          <Text style={styles.meta}>Kaydet, tek dokunuşla geç.</Text>
        </Pressable>
      )}

      {saved.length > 0 ? (
        <View style={styles.card}>
          <Text style={styles.sectionKicker}>Kayıtlı</Text>
          <Text style={styles.sectionTitle}>Kaydettiklerin</Text>
          <View style={styles.saveGrid}>
            {saved.map((card) => (
              <Pressable
                key={card.placeId}
                style={styles.saveCell}
                onPress={() => router.push({ pathname: '/venue/[id]', params: { id: card.placeId } })}
                accessibilityRole="button"
                accessibilityLabel={card.name}>
                <VenueImage
                  placeId={card.placeId}
                  photoUrl={card.photoUrl}
                  photoReference={card.photoReference}
                  name={card.name}
                  style={StyleSheet.absoluteFill}
                  radius={0}
                />
              </Pressable>
            ))}
          </View>
        </View>
      ) : session ? (
        <Text style={styles.emptySaved}>Henüz kayıt yok. Mekanda kalp bırak.</Text>
      ) : null}

      {prefs ? (
        <>
          <View style={styles.card}>
            <Text style={styles.sectionKicker}>Niyet</Text>
            <Text style={styles.sectionTitle}>Ne arıyorsun?</Text>
            <View style={styles.grid}>
              {INTENTS.map((intent) => {
                const on = prefs.intents.includes(intent);
                return (
                  <Pressable key={intent} onPress={() => toggleIntent(intent)} style={[styles.tile, on && styles.tileOn]}>
                    <Image source={INTENT_PHOTOS[intent]} style={StyleSheet.absoluteFill} contentFit="cover" />
                    <LinearGradient colors={['transparent', 'rgba(6,78,59,0.72)']} style={StyleSheet.absoluteFill} />
                    {on ? (
                      <View style={styles.check}>
                        <Ionicons name="checkmark" size={14} color={colors.white} />
                      </View>
                    ) : null}
                    <Text style={styles.tileText}>{intentLabel(intent)}</Text>
                  </Pressable>
                );
              })}
            </View>
          </View>

          <View style={styles.card}>
            <Text style={styles.sectionKicker}>Atmosfer</Text>
            <Text style={styles.sectionTitle}>Nasıl bir yer?</Text>
            <View style={styles.chips}>
              {VIBES.map((vibe) => {
                const on = prefs.vibes.includes(vibe);
                return (
                  <Pressable key={vibe} onPress={() => toggleVibe(vibe)} style={[styles.chip, on && styles.chipOn]}>
                    <Text style={on ? styles.chipOnText : styles.chipOffText}>{vibeLabel(vibe)}</Text>
                  </Pressable>
                );
              })}
            </View>
          </View>

          <View style={styles.card}>
            <Text style={styles.sectionKicker}>Mesafe</Text>
            <Text style={styles.sectionTitle}>Ne kadar uzağa?</Text>
            <RadiusPicker
              meters={prefs.defaultRadiusMeters}
              onChange={(meters) => {
                setPrefs({ ...prefs, defaultRadiusMeters: meters });
                filters.setRadius(meters);
                setSavedOk(false);
              }}
            />
          </View>

          <Pressable style={[styles.save, saving && styles.saveBusy]} onPress={() => void save()} disabled={saving}>
            <Text style={styles.saveText}>{savedOk ? 'Kaydedildi' : saving ? 'Kaydediliyor…' : 'Tercihleri kaydet'}</Text>
          </Pressable>
        </>
      ) : null}

      <View style={styles.account}>
        <Pressable style={styles.row} onPress={() => router.push('/places')}>
          <Ionicons name="navigate-outline" size={20} color={colors.ink} />
          <Text style={styles.rowText}>Konumu değiştir</Text>
          <Ionicons name="chevron-forward" size={18} color={colors.muted} />
        </Pressable>
        <View style={styles.divider} />
        <Pressable
          style={styles.row}
          onPress={async () => {
            if (session) {
              await api.consent('kvkk', true);
            }
          }}>
          <Ionicons name="shield-checkmark-outline" size={20} color={colors.ink} />
          <Text style={styles.rowText}>KVKK onayı</Text>
        </Pressable>
        <View style={styles.divider} />
        <Pressable
          style={styles.row}
          onPress={async () => {
            if (session) {
              await signOut();
              router.replace('/(tabs)');
              return;
            }
            router.push('/auth');
          }}>
          <Ionicons name="log-out-outline" size={20} color={colors.coral} />
          <Text style={[styles.rowText, styles.danger]}>{session ? 'Çıkış' : 'Giriş'}</Text>
        </Pressable>
        <View style={styles.divider} />
        <Pressable style={styles.row} onPress={confirmDelete}>
          <Ionicons name="trash-outline" size={20} color={colors.muted} />
          <Text style={[styles.rowText, styles.muted]}>Hesabı sil</Text>
        </Pressable>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { paddingBottom: 56 },
  hero: { height: 196, backgroundColor: colors.espresso },
  heroCopy: { flex: 1, justifyContent: 'flex-end', paddingHorizontal: 20, paddingBottom: 36 },
  kicker: {
    color: colors.coralSoft,
    fontWeight: '700',
    letterSpacing: 1.4,
    fontSize: 11,
    textTransform: 'uppercase',
  },
  heroLine: { fontFamily: fonts.display, color: colors.white, fontSize: 32, marginTop: 6 },
  identity: {
    marginTop: -28,
    marginHorizontal: 20,
    backgroundColor: colors.surface,
    borderRadius: 24,
    padding: 20,
    paddingTop: 28,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.line,
  },
  avatar: {
    position: 'absolute',
    top: -36,
    width: 72,
    height: 72,
    borderRadius: 36,
    backgroundColor: colors.espresso,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 3,
    borderColor: colors.surface,
  },
  cam: {
    position: 'absolute',
    right: 2,
    bottom: 2,
    width: 22,
    height: 22,
    borderRadius: 11,
    backgroundColor: colors.coral,
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarText: { color: colors.white, fontFamily: fonts.display, fontSize: 24, fontWeight: '700' },
  avatarImg: { width: 66, height: 66, borderRadius: 33 },
  name: { fontFamily: fonts.display, fontSize: 28, color: colors.ink, marginTop: 12 },
  badgeRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 10 },
  badge: {
    backgroundColor: colors.coralSoft,
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 999,
  },
  badgeText: { color: colors.espresso, fontWeight: '800', fontSize: 12 },
  loc: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: colors.bg,
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 999,
    borderWidth: 1,
    borderColor: colors.line,
  },
  locText: { color: colors.ink, fontWeight: '700', fontSize: 13 },
  meta: { color: colors.muted, marginTop: 10, fontSize: 14 },
  emptySaved: { color: colors.muted, marginHorizontal: 24, marginTop: 18, fontSize: 14 },
  saveGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 4 },
  saveCell: { width: '32%', aspectRatio: 1, overflow: 'hidden', backgroundColor: colors.line },
  card: {
    marginHorizontal: 20,
    marginTop: 16,
    backgroundColor: colors.surface,
    borderRadius: 24,
    padding: 18,
    borderWidth: 1,
    borderColor: colors.line,
  },
  sectionKicker: {
    color: colors.coral,
    fontWeight: '700',
    letterSpacing: 1.2,
    fontSize: 11,
    textTransform: 'uppercase',
  },
  sectionTitle: { fontFamily: fonts.display, fontSize: 24, color: colors.ink, marginTop: 4, marginBottom: 14 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  tile: {
    width: '48%',
    height: 96,
    borderRadius: 18,
    overflow: 'hidden',
    justifyContent: 'flex-end',
    borderWidth: 2,
    borderColor: 'transparent',
  },
  tileOn: { borderColor: colors.coral },
  tileText: { color: colors.white, fontWeight: '800', padding: 10, fontSize: 14 },
  check: {
    position: 'absolute',
    top: 8,
    right: 8,
    width: 22,
    height: 22,
    borderRadius: 11,
    backgroundColor: colors.coral,
    alignItems: 'center',
    justifyContent: 'center',
  },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: {
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 999,
    backgroundColor: colors.bg,
    borderWidth: 1,
    borderColor: colors.line,
  },
  chipOn: { backgroundColor: colors.coral, borderColor: colors.coral },
  chipOnText: { color: colors.white, fontWeight: '700', fontSize: 13 },
  chipOffText: { color: colors.ink, fontWeight: '600', fontSize: 13 },
  radiusRow: { flexDirection: 'row', gap: 8 },
  radius: {
    flex: 1,
    backgroundColor: colors.bg,
    borderRadius: 16,
    paddingVertical: 12,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.line,
  },
  radiusOn: { backgroundColor: colors.espresso, borderColor: colors.espresso },
  radiusLabel: { fontWeight: '800', color: colors.ink, fontSize: 13 },
  radiusSub: { color: colors.muted, fontSize: 10, marginTop: 2, fontWeight: '600' },
  radiusOnText: { color: colors.white },
  save: {
    marginHorizontal: 20,
    marginTop: 16,
    backgroundColor: colors.coral,
    paddingVertical: 16,
    borderRadius: 18,
    alignItems: 'center',
  },
  saveBusy: { opacity: 0.7 },
  saveText: { color: colors.white, fontWeight: '800', fontSize: 16 },
  account: {
    marginHorizontal: 20,
    marginTop: 20,
    backgroundColor: colors.surface,
    borderRadius: 24,
    borderWidth: 1,
    borderColor: colors.line,
    overflow: 'hidden',
  },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 16, paddingVertical: 16 },
  rowText: { flex: 1, color: colors.ink, fontWeight: '700', fontSize: 15 },
  danger: { color: colors.coral },
  muted: { color: colors.muted },
  divider: { height: 1, backgroundColor: colors.line, marginLeft: 48 },
});
