import { BrandLogo } from '@/components/BrandLogo';
import { INTENT_PHOTOS } from '@/lib/photos';
import { api } from '@/lib/api';
import { markOnboarded, getOnboardedAt } from '@/lib/onboard';
import { useSession } from '@/lib/session';
import { colors } from '@/lib/theme';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, StyleSheet, Switch, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

export default function AuthScreen() {
  const { signIn, apiBase, setBase } = useSession();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [name, setName] = useState('Gezgin');
  const [login, setLogin] = useState(true);
  const [kvkk, setKvkk] = useState(false);
  const [base, setBaseField] = useState(apiBase);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function goTabs() {
    const onboarded = await getOnboardedAt();
    router.replace(onboarded ? '/(tabs)' : '/onboarding');
  }

  async function submit() {
    if (!email.includes('@') || password.length < 8) {
      setError('E-posta ve en az 8 karakter şifre.');
      return;
    }
    if (!login && !kvkk) {
      setError('Kayıt için KVKK onayını işaretle.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await setBase(base);
      const session = login
        ? await api.login(email.trim(), password)
        : await api.register(email.trim(), password, name.trim(), kvkk);
      await signIn(session);
      if (kvkk && login) {
        void api.consent('kvkk', true);
      }
      await goTabs();
    } catch {
      setError('Bağlanamadı. Aynı Wi-Fi ve doğru API adresi.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <View style={styles.bg}>
      <View style={[styles.hero, { paddingTop: insets.top + 24 }]}>
        <Image source={INTENT_PHOTOS.AcikHava} style={StyleSheet.absoluteFill} contentFit="cover" />
        <LinearGradient colors={['rgba(15,20,25,0.2)', 'rgba(15,20,25,0.82)']} style={StyleSheet.absoluteFill} />
        <BrandLogo size={72} />
        <Text style={styles.sub}>Karar otuz saniye.</Text>
      </View>
      <View style={styles.card}>
        {!login ? (
          <TextInput style={styles.input} placeholder="Ad" placeholderTextColor={colors.muted} value={name} onChangeText={setName} />
        ) : null}
        <TextInput
          style={styles.input}
          placeholder="E-posta"
          placeholderTextColor={colors.muted}
          autoCapitalize="none"
          keyboardType="email-address"
          value={email}
          onChangeText={setEmail}
        />
        <TextInput
          style={styles.input}
          placeholder="Şifre"
          placeholderTextColor={colors.muted}
          secureTextEntry
          value={password}
          onChangeText={setPassword}
        />
        {!login ? (
          <View style={styles.kvkk}>
            <Switch value={kvkk} onValueChange={setKvkk} trackColor={{ true: colors.coral }} />
            <Text style={styles.kvkkText}>KVKK metnini okudum, kişisel verilerimin işlenmesini kabul ediyorum.</Text>
          </View>
        ) : null}
        <Text style={styles.hint}>API</Text>
        <TextInput style={styles.input} value={base} onChangeText={setBaseField} autoCapitalize="none" />
        {error ? <Text style={styles.err}>{error}</Text> : null}
        <Pressable style={styles.btn} onPress={() => void submit()} disabled={busy} accessibilityRole="button">
          <Text style={styles.btnText}>{login ? 'Giriş' : 'Kayıt'}</Text>
        </Pressable>
        <Pressable onPress={() => setLogin(!login)} accessibilityRole="button">
          <Text style={styles.link}>{login ? 'Hesap oluştur' : 'Zaten hesabım var'}</Text>
        </Pressable>
        <Pressable
          onPress={async () => {
            await markOnboarded();
            router.replace('/(tabs)');
          }}
          accessibilityRole="button"
          accessibilityLabel="Keşfet’e devam">
          <Text style={styles.link}>Keşfet’e devam</Text>
        </Pressable>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  hero: { height: 280, justifyContent: 'flex-end', paddingHorizontal: 24, paddingBottom: 28 },
  sub: { marginTop: 10, color: colors.muted, fontSize: 16 },
  card: {
    margin: 20,
    marginTop: -20,
    backgroundColor: colors.surface,
    borderRadius: 24,
    padding: 18,
    gap: 10,
    borderWidth: 1,
    borderColor: colors.line,
  },
  input: { borderRadius: 14, padding: 14, backgroundColor: colors.bg, color: colors.ink, minHeight: 44 },
  hint: { fontSize: 12, color: colors.muted, marginTop: 6 },
  err: { color: colors.coral },
  btn: { backgroundColor: colors.coral, padding: 16, minHeight: 44, borderRadius: 16, alignItems: 'center', marginTop: 8 },
  btnText: { color: colors.white, fontWeight: '800' },
  link: { textAlign: 'center', marginTop: 12, color: colors.coral, fontWeight: '700' },
  kvkk: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  kvkkText: { flex: 1, color: colors.muted, fontSize: 12 },
});
