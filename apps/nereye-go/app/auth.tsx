import { BrandLogo } from '@/components/BrandLogo';
import { Button } from '@/components/Button';
import { TextField } from '@/components/TextField';
import { api } from '@/lib/api';
import { friendlyError } from '@/lib/empty';
import { haptic } from '@/lib/haptics';
import { getOnboardedAt, markOnboarded } from '@/lib/onboard';
import { INTENT_PHOTOS } from '@/lib/photos';
import { useSession } from '@/lib/session';
import { colors, fonts, gradients, shadows } from '@/lib/theme';
import { toast } from '@/lib/toast';
import { emailProblem, nameProblem, passwordProblem, passwordStrength } from '@/lib/validation';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useRef, useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
  type TextInputInstance,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

const STRENGTH = ['', 'İdare eder', 'İyi', 'Güçlü'] as const;

export default function AuthScreen() {
  const { signIn, apiBase, setBase } = useSession();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [name, setName] = useState('');
  // Kullanım koşulları kabulü: ayrı, önceden işaretsiz kutu. Aydınlatma metni onay gerektirmez, yalnızca bağlantıdır.
  const [terms, setTerms] = useState(false);
  const [base, setBaseField] = useState(apiBase);
  const [showServer, setShowServer] = useState(false);
  const [touched, setTouched] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const passRef = useRef<TextInputInstance>(null);
  const emailRef = useRef<TextInputInstance>(null);
  const taps = useRef(0);

  const register = mode === 'register';
  const emailErr = emailProblem(email);
  // Girişte yalnızca boşluğu kontrol et: eski hesapların şifresi yeni kurala uymayabilir.
  const passErr = register ? passwordProblem(password) : password ? null : 'Şifreni yaz.';
  const nameErr = register ? nameProblem(name) : null;
  const strength = register ? passwordStrength(password) : 0;

  async function leave() {
    if (router.canGoBack()) {
      router.back();
      return;
    }
    router.replace((await getOnboardedAt()) ? '/(tabs)' : '/onboarding');
  }

  async function submit() {
    setTouched(true);
    if (emailErr || passErr || nameErr) {
      haptic.warn();
      return;
    }
    if (register && !terms) {
      setError('Devam etmek için kullanım koşullarını kabul et.');
      haptic.warn();
      return;
    }
    setBusy(true);
    setError(null);
    try {
      if (__DEV__ && showServer && base.trim() && base.trim() !== apiBase) {
        await setBase(base.trim().replace(/\/$/, ''));
      }
      const session = register
        ? await api.register(email.trim(), password, name.trim(), terms)
        : await api.login(email.trim(), password);
      await signIn(session);
      haptic.success();
      if (register || !(await getOnboardedAt())) {
        router.replace('/onboarding');
      } else {
        await leave();
      }
    } catch (e) {
      haptic.warn();
      setError(friendlyError(e));
    } finally {
      setBusy(false);
    }
  }

  // Şifremi unuttum: bağlantı e-postayla gelir, yeni şifre sunucudaki sayfada belirlenir.
  // Kayıtlı olsun olmasın aynı yanıt döner; sağlayıcı yoksa sunucu "şifre sıfırlama şu an kapalı" der.
  async function forgot() {
    if (emailErr) {
      setTouched(true);
      setError('Şifre sıfırlama bağlantısı için önce e-postanı yaz.');
      haptic.warn();
      emailRef.current?.focus();
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await api.forgotPassword(email.trim());
      haptic.success();
      toast.success(res?.message || 'Bu adres kayıtlıysa şifre sıfırlama bağlantısı gelecek.');
    } catch (e) {
      haptic.warn();
      setError(friendlyError(e));
    } finally {
      setBusy(false);
    }
  }

  function switchMode(next: 'login' | 'register') {
    setMode(next);
    setError(null);
    setTouched(false);
  }

  return (
    <KeyboardAvoidingView style={styles.bg} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <StatusBar style="light" />
      <ScrollView
        contentContainerStyle={{ paddingBottom: insets.bottom + 28 }}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}>
        <View style={[styles.hero, { paddingTop: insets.top + 16 }]}>
          <Image source={INTENT_PHOTOS.AcikHava} style={StyleSheet.absoluteFill} contentFit="cover" />
          <LinearGradient colors={[colors.scrimSoft, gradients.hero[1]]} style={StyleSheet.absoluteFill} />
          <Pressable
            style={[styles.close, { top: insets.top + 8 }]}
            onPress={() => void leave()}
            hitSlop={10}
            accessibilityRole="button"
            accessibilityLabel="Kapat">
            <Ionicons name="close" size={22} color={colors.white} />
          </Pressable>
          <Pressable
            onPress={() => {
              // Geliştirici kısayolu (yalnızca __DEV__): logoya beş kez dokununca sunucu adresi alanı açılır.
              if (!__DEV__) {
                return;
              }
              taps.current += 1;
              if (taps.current >= 5) {
                taps.current = 0;
                setShowServer((s) => !s);
              }
            }}
            accessible={false}>
            <BrandLogo size={64} />
          </Pressable>
          <Text style={styles.title}>{register ? 'Aramıza katıl' : 'Tekrar hoş geldin'}</Text>
          <Text style={styles.sub}>
            {register ? 'Favorilerin ve yorumların her cihazında seninle.' : 'Nereye gideceğine otuz saniyede karar ver.'}
          </Text>
        </View>

        <View style={styles.card}>
          <View style={styles.tabs} accessibilityRole="tablist">
            {(['login', 'register'] as const).map((m) => (
              <Pressable
                key={m}
                style={[styles.tab, mode === m && styles.tabOn]}
                onPress={() => switchMode(m)}
                accessibilityRole="tab"
                accessibilityState={{ selected: mode === m }}
                aria-selected={mode === m}>
                <Text style={[styles.tabText, mode === m && styles.tabTextOn]}>{m === 'login' ? 'Giriş yap' : 'Kayıt ol'}</Text>
              </Pressable>
            ))}
          </View>

          {register ? (
            <TextField
              label="Adın"
              icon="person-outline"
              placeholder="Nasıl hitap edelim?"
              value={name}
              onChangeText={setName}
              autoCapitalize="words"
              autoComplete="name"
              textContentType="name"
              returnKeyType="next"
              maxLength={40}
              onSubmitEditing={() => emailRef.current?.focus()}
              error={touched ? nameErr : null}
            />
          ) : null}
          <TextField
            ref={emailRef}
            label="E-posta"
            icon="mail-outline"
            placeholder="ornek@eposta.com"
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            autoCorrect={false}
            keyboardType="email-address"
            autoComplete="email"
            textContentType="emailAddress"
            returnKeyType="next"
            onSubmitEditing={() => passRef.current?.focus()}
            error={touched ? emailErr : null}
          />
          <TextField
            ref={passRef}
            label="Şifre"
            icon="lock-closed-outline"
            placeholder={register ? 'En az 8 karakter, harf ve rakam' : 'Şifren'}
            value={password}
            onChangeText={setPassword}
            secret
            autoCapitalize="none"
            autoComplete={register ? 'new-password' : 'current-password'}
            textContentType={register ? 'newPassword' : 'password'}
            returnKeyType="go"
            onSubmitEditing={() => void submit()}
            error={touched ? passErr : null}
            hint={register && strength > 0 ? `Şifre gücü: ${STRENGTH[strength]}` : undefined}
          />
          {register && password.length > 0 ? (
            <View style={styles.meter} accessibilityElementsHidden>
              {[1, 2, 3].map((n) => (
                <View key={n} style={[styles.meterBar, strength >= n && (strength === 1 ? styles.meterWeak : styles.meterOk)]} />
              ))}
            </View>
          ) : null}
          {!register ? (
            <Pressable
              style={styles.forgot}
              onPress={() => void forgot()}
              disabled={busy}
              accessibilityRole="button"
              accessibilityLabel="Şifremi unuttum: e-postama sıfırlama bağlantısı gönder"
              hitSlop={8}>
              <Text style={styles.forgotText}>Şifremi unuttum</Text>
            </Pressable>
          ) : null}

          {register ? (
            <>
              <Text style={styles.kvkkText}>
                Kişisel verilerinin nasıl işlendiğini{' '}
                <Text style={styles.link} onPress={() => router.push({ pathname: '/legal', params: { doc: 'privacy' } })}>
                  KVKK aydınlatma metninde
                </Text>{' '}
                okuyabilirsin.
              </Text>
              <Pressable
                style={styles.kvkk}
                onPress={() => {
                  haptic.tap();
                  setTerms((v) => !v);
                }}
                accessibilityRole="checkbox"
                accessibilityState={{ checked: terms }}
                aria-checked={terms}
                accessibilityLabel="Kullanım koşullarını okudum ve kabul ediyorum">
                <View style={[styles.box, terms && styles.boxOn]}>
                  {terms ? <Ionicons name="checkmark" size={16} color={colors.white} /> : null}
                </View>
                <Text style={styles.kvkkText}>
                  <Text style={styles.link} onPress={() => router.push({ pathname: '/legal', params: { doc: 'terms' } })}>
                    Kullanım koşullarını
                  </Text>{' '}
                  okudum ve kabul ediyorum. Yorum ve fotoğraflarımın bu koşullara uyacağını biliyorum.
                </Text>
              </Pressable>
            </>
          ) : null}

          {__DEV__ && showServer ? (
            <TextField
              label="Sunucu adresi (geliştirici)"
              icon="server-outline"
              value={base}
              onChangeText={setBaseField}
              autoCapitalize="none"
              autoCorrect={false}
              keyboardType="url"
              hint="Telefon ve bilgisayar aynı Wi-Fi'de olmalı."
            />
          ) : null}

          {error ? (
            <View style={styles.err} accessibilityRole="alert">
              <Ionicons name="alert-circle" size={18} color={colors.danger} />
              <Text style={styles.errText}>{error}</Text>
            </View>
          ) : null}

          <Button label={register ? 'Hesap oluştur' : 'Giriş yap'} onPress={() => void submit()} loading={busy} />
          <Button
            label="Üye olmadan keşfet"
            variant="ghost"
            onPress={() => {
              void markOnboarded().then(() => router.replace('/(tabs)'));
            }}
          />
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  hero: { minHeight: 268, justifyContent: 'flex-end', paddingHorizontal: 24, paddingBottom: 44 },
  close: {
    position: 'absolute',
    right: 16,
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.glass,
    borderWidth: 1,
    borderColor: colors.glassLine,
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: { fontFamily: fonts.display, color: colors.white, fontSize: 30, marginTop: 14 },
  sub: { marginTop: 6, color: colors.onDarkMuted, fontSize: 15, lineHeight: 21, fontFamily: fonts.sans },
  card: {
    marginHorizontal: 16,
    marginTop: -24,
    backgroundColor: colors.surface,
    borderRadius: 24,
    padding: 20,
    gap: 12,
    ...shadows.float,
  },
  tabs: { flexDirection: 'row', backgroundColor: colors.bg, borderRadius: 14, padding: 4, marginBottom: 4 },
  tab: { flex: 1, minHeight: 44, borderRadius: 11, alignItems: 'center', justifyContent: 'center' },
  tabOn: { backgroundColor: colors.coral },
  tabText: { fontFamily: fonts.sansMed, color: colors.muted, fontSize: 15 },
  tabTextOn: { fontFamily: fonts.sansBold, color: colors.white },
  meter: { flexDirection: 'row', gap: 6, marginTop: -6 },
  meterBar: { flex: 1, height: 4, borderRadius: 2, backgroundColor: colors.line },
  meterWeak: { backgroundColor: colors.gold },
  meterOk: { backgroundColor: colors.sage },
  kvkk: { flexDirection: 'row', alignItems: 'flex-start', gap: 12, minHeight: 44, paddingVertical: 4 },
  box: {
    width: 24,
    height: 24,
    borderRadius: 7,
    borderWidth: 1.5,
    borderColor: colors.muted,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 1,
  },
  boxOn: { backgroundColor: colors.coral, borderColor: colors.coral },
  kvkkText: { flex: 1, color: colors.muted, fontSize: 13, lineHeight: 19, fontFamily: fonts.sans },
  link: { color: colors.coral, fontFamily: fonts.sansBold, textDecorationLine: 'underline' },
  forgot: { alignSelf: 'flex-end', minHeight: 44, justifyContent: 'center', paddingHorizontal: 4 },
  forgotText: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 14 },
  err: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    backgroundColor: colors.dangerSoft,
    borderRadius: 12,
    padding: 12,
  },
  errText: { flex: 1, color: colors.espresso, fontFamily: fonts.sansMed, fontSize: 14 },
});
