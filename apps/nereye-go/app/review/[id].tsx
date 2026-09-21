import { VIBES, vibeLabel } from '@/lib/constants';
import { api } from '@/lib/api';
import { friendlyError } from '@/lib/empty';
import { useSession } from '@/lib/session';
import { colors, fonts } from '@/lib/theme';
import * as ImagePicker from 'expo-image-picker';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';

export default function Review() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { session, ready } = useSession();
  const [stars, setStars] = useState(5);
  const [text, setText] = useState('');
  const [tags, setTags] = useState<string[]>([]);
  const [photo, setPhoto] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (ready && !session) {
      router.replace('/auth');
    }
  }, [ready, session, router]);

  async function pick() {
    const perm = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (perm.status !== 'granted') {
      setError('Galeri izni gerekir.');
      return;
    }
    const picked = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], quality: 0.7, base64: true });
    const asset = picked.assets?.[0];
    if (!picked.canceled && asset?.base64) {
      setPhoto(asset.base64);
    }
  }

  return (
    <View style={styles.wrap}>
      <Text style={styles.label}>Puan</Text>
      <View style={styles.row}>
        {[1, 2, 3, 4, 5].map((n) => (
          <Pressable key={n} onPress={() => setStars(n)} accessibilityRole="button" accessibilityLabel={`${n} yıldız`} hitSlop={8}>
            <Text style={{ fontSize: 32, color: n <= stars ? colors.coral : colors.line }}>★</Text>
          </Pressable>
        ))}
      </View>
      <TextInput
        style={styles.input}
        placeholder="Nasıl bir yerdi?"
        placeholderTextColor={colors.muted}
        value={text}
        onChangeText={setText}
        multiline
      />
      <View style={styles.row}>
        {VIBES.map((v) => {
          const on = tags.includes(v);
          return (
            <Pressable key={v} style={[styles.chip, on && styles.on]} onPress={() => setTags(on ? tags.filter((t) => t !== v) : [...tags, v])}>
              <Text style={{ color: on ? colors.white : colors.ink }}>{vibeLabel(v)}</Text>
            </Pressable>
          );
        })}
      </View>
      <Pressable style={styles.outline} onPress={() => void pick()}>
        <Text style={styles.outlineText}>{photo ? 'Fotoğraf seçildi' : 'Fotoğraf ekle'}</Text>
      </Pressable>
      {error ? <Text style={styles.err}>{error}</Text> : null}
      <Pressable
        style={styles.btn}
        disabled={busy}
        onPress={async () => {
          setBusy(true);
          setError(null);
          try {
            const urls: string[] = [];
            if (photo) {
              const up = await api.uploadPhoto(photo);
              if (up?.url) {
                urls.push(up.url);
              }
            }
            await api.writeReview(id!, stars, text, tags, urls);
            router.back();
          } catch (e) {
            setError(friendlyError(e));
          } finally {
            setBusy(false);
          }
        }}>
        <Text style={styles.btnText}>{busy ? 'Gönderiliyor…' : 'Gönder'}</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { flex: 1, padding: 16, gap: 12, backgroundColor: colors.bg },
  label: { color: colors.ink, fontWeight: '700', fontFamily: fonts.sansBold },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  input: { minHeight: 90, borderRadius: 14, padding: 12, backgroundColor: colors.surface, color: colors.ink },
  chip: { paddingHorizontal: 10, paddingVertical: 10, minHeight: 44, borderRadius: 14, backgroundColor: colors.surface },
  on: { backgroundColor: colors.coral },
  outline: { padding: 14, minHeight: 44, borderRadius: 14, borderWidth: 1, borderColor: colors.line, alignItems: 'center' },
  outlineText: { color: colors.ink, fontWeight: '700' },
  err: { color: colors.coral },
  btn: { backgroundColor: colors.coral, padding: 14, minHeight: 44, borderRadius: 16, alignItems: 'center' },
  btnText: { color: colors.white, fontWeight: '800' },
});
