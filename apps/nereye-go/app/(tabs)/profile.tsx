import { initialsOf } from '@/components/Avatar';
import { Button } from '@/components/Button';
import { RadiusPicker } from '@/components/RadiusPicker';
import { INTENTS, VIBES, intentLabel, vibeLabel } from '@/lib/constants';
import { clampRadius, radiusLabel as formatRadius } from '@/lib/radius';
import { api, mediaUrl } from '@/lib/api';
import { setAnalyticsConsent, useAnalyticsConsent } from '@/lib/analytics';
import { friendlyError } from '@/lib/empty';
import { useFavorites } from '@/lib/favorites';
import { useFilters } from '@/lib/filters';
import { haptic } from '@/lib/haptics';
import { toast } from '@/lib/toast';
import { hydrateSavedPlaces, useSavedPlaces } from '@/lib/saved-places';
import { INTENT_PHOTOS } from '@/lib/photos';
import { useSession } from '@/lib/session';
import { colors, fonts, gradients, shadows } from '@/lib/theme';
import { isEvening } from '@/lib/copy';
import type { Preferences, Profile as ProfileInfo } from '@/lib/types';
import Constants from 'expo-constants';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import * as ImagePicker from 'expo-image-picker';
import { LinearGradient } from 'expo-linear-gradient';
import { useFocusEffect, useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useCallback, useEffect, useMemo, useState, type ComponentProps } from 'react';
import { ActivityIndicator, Alert, Pressable, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

const EMPTY: Preferences = { defaultRadiusMeters: 1500, intents: [], vibes: [] };

function Row({
  icon,
  label,
  value,
  onPress,
  tone = 'ink',
}: {
  icon: ComponentProps<typeof Ionicons>['name'];
  label: string;
  value?: string;
  onPress: () => void;
  tone?: 'ink' | 'danger';
}) {
  const color = tone === 'danger' ? colors.danger : colors.ink;
  return (
    <Pressable
      style={({ pressed }) => [styles.row, pressed && { backgroundColor: colors.bg }]}
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={value ? `${label}, ${value}` : label}>
      <Ionicons name={icon} size={20} color={color} />
      <Text style={[styles.rowText, { color }]}>{label}</Text>
      {value ? <Text style={styles.rowValue}>{value}</Text> : null}
      <Ionicons name="chevron-forward" size={18} color={colors.muted} />
    </Pressable>
  );
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
  const [info, setInfo] = useState<ProfileInfo | null>(null);
  const [uploading, setUploading] = useState(false);
  const favorites = useFavorites();
  const analytics = useAnalyticsConsent();

  useEffect(() => {
    if (!session) {
      setPrefs(EMPTY);
      return;
    }
    void api
      .preferences()
      .then((p) => setPrefs({ ...p, defaultRadiusMeters: clampRadius(p.defaultRadiusMeters) }))
      .catch(() => setPrefs(EMPTY));
    void hydrateSavedPlaces(true);
  }, [session]);

  // Sekmeye her dönüşte sayaçlar güncel olsun (yeni yorum, yeni kayıt).
  useFocusEffect(
    useCallback(() => {
      if (!session) {
        setInfo(null);
        return;
      }
      void api.profile().then((p) => setInfo(p ?? null));
    }, [session]),
  );

  const name = session?.displayName ?? 'Gezgin';
  const role = session?.roles.includes('Admin') ? 'Admin' : session?.roles.includes('VenueOwner') ? 'İşletme' : 'Gezgin';
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
      if (!session) {
        // Misafir: tercihler cihazda kalır, öneriler yine kişiselleşir.
        filters.setRadius(prefs.defaultRadiusMeters);
        filters.setVibes(prefs.vibes);
        if (prefs.intents[0]) {
          filters.setIntent(prefs.intents[0]);
        }
        setSavedOk(true);
        haptic.success();
        toast.success('Tercihlerin bu cihazda kaydedildi');
        return;
      }
      const saved = await api.savePreferences(prefs);
      if (!saved) {
        toast.error('Kaydedilemedi. Bağlantını kontrol edip tekrar dene.');
        return;
      }
      setPrefs(saved);
      filters.setRadius(saved.defaultRadiusMeters);
      filters.setVibes(saved.vibes);
      if (saved.intents[0]) {
        filters.setIntent(saved.intents[0]);
      }
      setSavedOk(true);
      haptic.success();
      toast.success('Tercihlerin kaydedildi');
    } finally {
      setSaving(false);
    }
  }

  async function pickAvatar() {
    if (!session) {
      router.push('/auth');
      return;
    }
    // Sistem fotoğraf seçicisi izin istemez (Android PickVisualMedia); galeri izni pakette yok.
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
    setUploading(true);
    try {
      const res = await api.uploadAvatar(asset.base64);
      if (res?.avatarUrl) {
        // Dosya adı aynı kalır; önbelleği aşmak için sürüm ekle.
        await patchSession({ avatarUrl: `${res.avatarUrl.split('?')[0]}?v=${Date.now()}` });
        toast.success('Profil fotoğrafın güncellendi');
      } else {
        toast.error('Fotoğraf yüklenemedi. Daha küçük bir fotoğraf dene.');
      }
    } catch (e) {
      toast.error(friendlyError(e));
    } finally {
      setUploading(false);
    }
  }

  function confirmSignOut() {
    Alert.alert('Çıkış yap', 'Bu cihazdaki oturumun kapatılacak.', [
      { text: 'Vazgeç', style: 'cancel' },
      {
        text: 'Çıkış yap',
        style: 'destructive',
        onPress: () => {
          void signOut().then(() => toast.info('Çıkış yapıldı'));
        },
      },
    ]);
  }

  return (
    <ScrollView style={styles.bg} contentContainerStyle={styles.wrap} showsVerticalScrollIndicator={false}>
      <StatusBar style="light" />
      {/* Diğer ekranlarla aynı yeşil degrade üst alan. */}
      <LinearGradient colors={isEvening() ? gradients.evening : gradients.hero} style={styles.hero}>
        <View style={[styles.heroCopy, { paddingTop: insets.top + 12 }]}>
          <Text style={styles.kicker}>Profil</Text>
          <Text style={styles.heroLine}>Karar tarzın.</Text>
        </View>
      </LinearGradient>

      <View style={styles.identity}>
        <Pressable
          style={styles.avatar}
          onPress={() => void pickAvatar()}
          disabled={uploading}
          accessibilityRole="button"
          accessibilityLabel={session ? 'Profil fotoğrafını değiştir' : 'Giriş yap'}>
          {session?.avatarUrl ? (
            <Image source={{ uri: mediaUrl(session.avatarUrl) ?? undefined }} style={styles.avatarImg} contentFit="cover" />
          ) : (
            <Text style={styles.avatarText}>{session ? initialsOf(name) : '?'}</Text>
          )}
          <View style={styles.cam}>
            {uploading ? <ActivityIndicator size="small" color={colors.white} /> : <Ionicons name="camera" size={12} color={colors.white} />}
          </View>
        </Pressable>
        {session ? (
          <>
            <Text style={styles.name}>{name}</Text>
            <View style={styles.badgeRow}>
              <View style={styles.badge}>
                <Text style={styles.badgeText}>{role}</Text>
              </View>
              <Pressable style={styles.loc} onPress={() => router.push('/places')} accessibilityRole="button" accessibilityLabel={`Konum ${filters.label}, değiştir`}>
                <Ionicons name="location" size={13} color={colors.coral} />
                <Text style={styles.locText}>{filters.label}</Text>
                <Ionicons name="chevron-down" size={13} color={colors.ink} />
              </Pressable>
            </View>
            <View style={styles.stats}>
              {(
                [
                  ['Kayıt', info?.favoriteCount ?? favorites.cards.length, '/favorites'],
                  ['Yorum', info?.reviewCount ?? 0, '/my-reviews'],
                  ['Konum', savedPlaces.items.length, '/places'],
                ] as const
              ).map(([label, value, href]) => (
                <Pressable
                  key={label}
                  style={styles.stat}
                  onPress={() => router.push(href)}
                  accessibilityRole="button"
                  accessibilityLabel={`${value} ${label}`}>
                  <Text style={styles.statValue}>{value}</Text>
                  <Text style={styles.statLabel}>{label}</Text>
                </Pressable>
              ))}
            </View>
          </>
        ) : (
          <>
            <Text style={styles.name}>Merhaba, gezgin</Text>
            <Text style={styles.guest}>Giriş yap; kaydettiklerin, yorumların ve tercihlerin her cihazında seninle olsun.</Text>
            <Button label="Giriş yap ya da kayıt ol" onPress={() => router.push('/auth')} style={styles.guestBtn} />
          </>
        )}
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

      {prefs ? (
        <>
          <View style={styles.card}>
            <Text style={styles.sectionKicker}>Niyet</Text>
            <Text style={styles.sectionTitle}>Ne arıyorsun?</Text>
            <View style={styles.grid}>
              {INTENTS.map((intent) => {
                const on = prefs.intents.includes(intent);
                return (
                  <Pressable
                    key={intent}
                    onPress={() => toggleIntent(intent)}
                    style={[styles.tile, on && styles.tileOn]}
                    accessibilityRole="checkbox"
                    accessibilityState={{ checked: on }} aria-checked={on}
                    accessibilityLabel={intentLabel(intent)}>
                    <Image source={INTENT_PHOTOS[intent]} style={StyleSheet.absoluteFill} contentFit="cover" />
                    <LinearGradient colors={gradients.photo} style={StyleSheet.absoluteFill} />
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
                  <Pressable
                    key={vibe}
                    onPress={() => toggleVibe(vibe)}
                    style={[styles.chip, on && styles.chipOn]}
                    accessibilityRole="checkbox"
                    accessibilityState={{ checked: on }} aria-checked={on}
                    accessibilityLabel={vibeLabel(vibe)}>
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

          <Button
            label={savedOk ? 'Kaydedildi' : 'Tercihleri kaydet'}
            icon={savedOk ? 'checkmark' : undefined}
            loading={saving}
            onPress={() => void save()}
            style={styles.save}
          />
        </>
      ) : null}

      <View style={styles.account}>
        {session ? (
          <>
            <Row icon="bookmarks-outline" label="Listelerim" onPress={() => router.push('/collections')} />
            <View style={styles.divider} />
            <Row icon="heart-outline" label="Kaydedilenler" value={String(info?.favoriteCount ?? favorites.cards.length)} onPress={() => router.push('/favorites')} />
            <View style={styles.divider} />
            <Row icon="chatbubble-ellipses-outline" label="Yorumlarım" value={String(info?.reviewCount ?? 0)} onPress={() => router.push('/my-reviews')} />
            <View style={styles.divider} />
            <Row
              icon={info?.emailVerified === false ? 'mail-unread-outline' : 'person-circle-outline'}
              label="Hesap ve güvenlik"
              // Doğrulanmamış hesap yorum ve fotoğraf yazamaz: kısa uyarı.
              value={info?.emailVerified === false ? 'E-postanı doğrula' : undefined}
              onPress={() => router.push('/account')}
            />
            <View style={styles.divider} />
          </>
        ) : null}
        <Row icon="navigate-outline" label="Konumu değiştir" value={filters.label} onPress={() => router.push('/places')} />
        <View style={styles.divider} />
        <View style={styles.row}>
          <Ionicons name="stats-chart-outline" size={20} color={colors.ink} />
          <View style={{ flex: 1 }}>
            <Text style={styles.toggleText}>Anonim kullanım istatistikleri</Text>
            <Text style={styles.rowHint}>Kimlik ve konum gönderilmez</Text>
          </View>
          <Switch
            value={analytics === 'granted'}
            onValueChange={(v) => void setAnalyticsConsent(v, Boolean(session))}
            trackColor={{ true: colors.sage, false: colors.line }}
            thumbColor={colors.white}
            accessibilityLabel="Anonim kullanım istatistikleri"
          />
        </View>
        <View style={styles.divider} />
        <Row icon="shield-checkmark-outline" label="Gizlilik ve KVKK" onPress={() => router.push('/legal')} />
        {session ? (
          <>
            <View style={styles.divider} />
            <Row icon="log-out-outline" label="Çıkış yap" tone="danger" onPress={confirmSignOut} />
          </>
        ) : null}
      </View>
      <Text style={styles.version}>Nereye · sürüm {Constants.expoConfig?.version ?? '1.0.0'}</Text>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { paddingBottom: 56 },
  hero: { height: 220, borderBottomLeftRadius: 28, borderBottomRightRadius: 28 },
  heroCopy: { flex: 1, justifyContent: 'flex-end', paddingHorizontal: 20, paddingBottom: 84 },
  kicker: {
    color: colors.onDarkMuted,
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
    ...shadows.card,
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
    ...shadows.card,
  },
  sectionKicker: {
    color: colors.accent,
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
    minHeight: 44,
    justifyContent: 'center',
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
  save: { marginHorizontal: 20, marginTop: 16 },
  stats: { flexDirection: 'row', alignSelf: 'stretch', marginTop: 16, borderTopWidth: 1, borderTopColor: colors.line, paddingTop: 12 },
  stat: { flex: 1, alignItems: 'center', minHeight: 48, justifyContent: 'center' },
  statValue: { fontFamily: fonts.display, fontSize: 22, color: colors.ink },
  statLabel: { color: colors.muted, fontFamily: fonts.sans, fontSize: 12, marginTop: 2 },
  guest: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, lineHeight: 20, textAlign: 'center', marginTop: 8 },
  guestBtn: { alignSelf: 'stretch', marginTop: 16 },
  rowValue: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, maxWidth: 140 },
  version: { textAlign: 'center', color: colors.muted, fontFamily: fonts.sans, fontSize: 12, marginTop: 20 },
  saveBusy: { opacity: 0.7 },
  saveText: { color: colors.white, fontWeight: '800', fontSize: 16 },
  account: {
    marginHorizontal: 20,
    marginTop: 20,
    backgroundColor: colors.surface,
    borderRadius: 24,
    ...shadows.card,
  },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 16, minHeight: 56 },
  rowText: { flex: 1, fontFamily: fonts.sansMed, fontSize: 15 },
  danger: { color: colors.danger },
  muted: { color: colors.muted },
  divider: { height: 1, backgroundColor: colors.line, marginLeft: 48 },
  toggleText: { color: colors.ink, fontFamily: fonts.sansMed, fontSize: 15 },
  rowHint: { color: colors.muted, fontFamily: fonts.sans, fontSize: 12, marginTop: 1 },
});
