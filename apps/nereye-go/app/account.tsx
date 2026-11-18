import { Button } from '@/components/Button';
import { TextField } from '@/components/TextField';
import { api } from '@/lib/api';
import { friendlyError } from '@/lib/empty';
import { haptic } from '@/lib/haptics';
import { useSession } from '@/lib/session';
import { colors, fonts } from '@/lib/theme';
import { toast } from '@/lib/toast';
import { nameProblem, passwordProblem } from '@/lib/validation';
import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { Alert, KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

export default function Account() {
  const { session, patchSession, signIn, signOut } = useSession();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const [email, setEmail] = useState('');
  // null: henüz bilinmiyor (kart gösterilmez).
  const [verified, setVerified] = useState<boolean | null>(null);
  const [name, setName] = useState(session?.displayName ?? '');
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [marketing, setMarketing] = useState(false);
  const [blocked, setBlocked] = useState<{ userId: string; displayName: string }[]>([]);
  const [busy, setBusy] = useState<string | null>(null);
  const [passError, setPassError] = useState<string | null>(null);

  useEffect(() => {
    if (!session) {
      router.replace('/auth');
      return;
    }
    void api.profile().then((p) => {
      if (p) {
        setEmail(p.email);
        setName(p.displayName);
      }
    });
    void api.consents().then((c) => setMarketing(Boolean(c.marketing)));
    void api.blocks().then(setBlocked);
    // Yalnızca ekran açılırken: oturum yenilenince kullanıcının yazdığını ezmeyelim.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Ekran her odaklandığında doğrulama durumu yeniden okunur: tarayıcıda doğrulayıp dönen kullanıcıda kart kalkar.
  useFocusEffect(
    useCallback(() => {
      if (!session) {
        return;
      }
      let alive = true;
      void api.profile().then((p) => {
        if (alive && p) {
          setVerified(p.emailVerified ?? false);
        }
      });
      return () => {
        alive = false;
      };
    }, [session]),
  );

  if (!session) {
    return <View style={styles.bg} />;
  }

  const nameErr = nameProblem(name);
  const nameChanged = name.trim() !== session.displayName;

  async function sendVerification() {
    setBusy('verify');
    try {
      const res = await api.resendVerification();
      if (res.alreadyVerified) {
        // Başka yerde (ör. tarayıcıda) doğrulanmış: mektup gitmedi, "gönderildi" denmez.
        setVerified(true);
        toast.info('E-postan zaten doğrulanmış.');
        return;
      }
      if (!res.sent) {
        toast.error('Bağlantı gönderilemedi. Biraz sonra tekrar dene.');
        return;
      }
      haptic.success();
      toast.success('Doğrulama bağlantısı e-postana gönderildi.');
    } catch (e) {
      toast.error(friendlyError(e));
    } finally {
      setBusy(null);
    }
  }

  async function saveName() {
    setBusy('name');
    try {
      const p = await api.updateProfile(name.trim());
      await patchSession({ displayName: p.displayName });
      setName(p.displayName);
      haptic.success();
      toast.success('Adın güncellendi');
    } catch (e) {
      toast.error(friendlyError(e));
    } finally {
      setBusy(null);
    }
  }

  async function savePassword() {
    const problem = !current ? 'Mevcut şifreni yaz.' : passwordProblem(next);
    if (problem) {
      setPassError(problem);
      haptic.warn();
      return;
    }
    setBusy('pass');
    setPassError(null);
    try {
      // Sunucu diğer cihazlardaki oturumları kapatır ve bu cihaza yenisini verir.
      await signIn(await api.changePassword(current, next));
      setCurrent('');
      setNext('');
      haptic.success();
      toast.success('Şifren değişti. Diğer cihazlardaki oturumlar kapatıldı.');
    } catch (e) {
      haptic.warn();
      setPassError(friendlyError(e));
    } finally {
      setBusy(null);
    }
  }

  function confirmDelete() {
    Alert.alert(
      'Hesabı kalıcı olarak sil',
      'Kaydettiğin mekanlar, konumlar ve tercihlerin silinir; yorum metinlerin anonimleştirilir. Bu işlem geri alınamaz.',
      [
        { text: 'Vazgeç', style: 'cancel' },
        {
          text: 'Hesabımı sil',
          style: 'destructive',
          onPress: () => {
            setBusy('delete');
            api
              .deleteMe()
              .then(async () => {
                await signOut();
                toast.info('Hesabın silindi.');
                router.replace('/(tabs)');
              })
              .catch((e) => toast.error(friendlyError(e)))
              .finally(() => setBusy(null));
          },
        },
      ],
    );
  }

  return (
    <KeyboardAvoidingView style={styles.bg} behavior={Platform.OS === 'ios' ? 'padding' : undefined} keyboardVerticalOffset={64}>
      <ScrollView
        contentContainerStyle={[styles.wrap, { paddingBottom: insets.bottom + 32 }]}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}>
        <View style={styles.card}>
          <Text style={styles.h}>Profil</Text>
          {email ? <TextField label="E-posta" value={email} editable={false} icon="mail-outline" hint="E-posta değiştirilemez." /> : null}
          <TextField
            label="Adın"
            value={name}
            onChangeText={setName}
            icon="person-outline"
            maxLength={40}
            autoCapitalize="words"
            error={nameChanged ? nameErr : null}
          />
          <Button label="Adı kaydet" compact disabled={!nameChanged || Boolean(nameErr)} loading={busy === 'name'} onPress={() => void saveName()} />
        </View>

        {verified === false ? (
          <View style={styles.card}>
            <Text style={styles.h}>E-postanı doğrula</Text>
            <Text style={styles.sub}>
              Yorum yazmak ve fotoğraf yüklemek için e-posta adresini doğrulaman gerekiyor. Gelen bağlantıyı açman yeterli.
            </Text>
            <Button
              label="Doğrulama bağlantısı gönder"
              compact
              icon="mail-unread-outline"
              loading={busy === 'verify'}
              onPress={() => void sendVerification()}
            />
          </View>
        ) : null}

        <View style={styles.card}>
          <Text style={styles.h}>Şifre</Text>
          <TextField
            label="Mevcut şifre"
            value={current}
            onChangeText={setCurrent}
            secret
            icon="lock-closed-outline"
            autoComplete="current-password"
            textContentType="password"
          />
          <TextField
            label="Yeni şifre"
            value={next}
            onChangeText={setNext}
            secret
            icon="key-outline"
            autoComplete="new-password"
            textContentType="newPassword"
            hint="En az 8 karakter, harf ve rakam."
            error={passError}
          />
          <Button label="Şifreyi değiştir" compact variant="outline" loading={busy === 'pass'} onPress={() => void savePassword()} />
        </View>

        {blocked.length ? (
          <View style={styles.card}>
            <Text style={styles.h}>Engellediğin kullanıcılar</Text>
            <Text style={styles.sub}>Bu kişilerin yorumlarını görmüyorsun.</Text>
            {blocked.map((b) => (
              <View key={b.userId} style={styles.toggle}>
                <Text style={[styles.toggleTitle, { flex: 1 }]} numberOfLines={1}>
                  {b.displayName}
                </Text>
                <Button
                  label="Engeli kaldır"
                  variant="outline"
                  compact
                  onPress={() => {
                    const before = blocked;
                    setBlocked(blocked.filter((x) => x.userId !== b.userId));
                    api.unblock(b.userId).catch((e) => {
                      setBlocked(before);
                      toast.error(friendlyError(e));
                    });
                  }}
                />
              </View>
            ))}
          </View>
        ) : null}

        <View style={styles.card}>
          <Text style={styles.h}>Gizlilik</Text>
          <View style={styles.toggle}>
            <View style={{ flex: 1 }}>
              <Text style={styles.toggleTitle}>Kampanya ve öneri bildirimleri</Text>
              <Text style={styles.toggleSub}>İstediğin an kapatabilirsin.</Text>
            </View>
            <Switch
              value={marketing}
              onValueChange={(v) => {
                setMarketing(v);
                void api.consent('marketing', v);
              }}
              trackColor={{ true: colors.sage, false: colors.line }}
              thumbColor={colors.white}
              accessibilityLabel="Kampanya ve öneri bildirimleri"
            />
          </View>
          <Button label="Gizlilik ve KVKK metni" variant="outline" compact icon="shield-checkmark-outline" onPress={() => router.push('/legal')} />
        </View>

        <View style={[styles.card, styles.dangerCard]}>
          <Text style={styles.h}>Hesabı sil</Text>
          <Text style={styles.sub}>Hesabın ve kişisel verilerin kalıcı olarak kaldırılır.</Text>
          <Button label="Hesabımı sil" variant="danger" compact icon="trash-outline" loading={busy === 'delete'} onPress={confirmDelete} />
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  wrap: { padding: 16, gap: 14 },
  card: { backgroundColor: colors.surface, borderRadius: 22, borderWidth: 1, borderColor: colors.line, padding: 18, gap: 12 },
  dangerCard: { borderColor: colors.dangerSoft },
  h: { fontFamily: fonts.display, fontSize: 22, color: colors.ink },
  sub: { color: colors.muted, fontFamily: fonts.sans, fontSize: 14, lineHeight: 20 },
  toggle: { flexDirection: 'row', alignItems: 'center', gap: 12, minHeight: 48 },
  toggleTitle: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 15 },
  toggleSub: { color: colors.muted, fontFamily: fonts.sans, fontSize: 13, marginTop: 2 },
});
