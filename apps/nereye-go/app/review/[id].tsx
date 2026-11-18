import { Button } from '@/components/Button';
import { StarInput } from '@/components/Stars';
import { TextField } from '@/components/TextField';
import { api, mediaUrl } from '@/lib/api';
import { VIBES, vibeLabel } from '@/lib/constants';
import { friendlyError } from '@/lib/empty';
import { haptic } from '@/lib/haptics';
import { REVIEW_MAX, REVIEW_MAX_PHOTOS } from '@/lib/reviews';
import { useSession } from '@/lib/session';
import { colors, fonts } from '@/lib/theme';
import { toast } from '@/lib/toast';
import { Ionicons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import * as ImagePicker from 'expo-image-picker';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/** Sunucuya yüklenmiş (url) ya da yüklenmeyi bekleyen (base64) fotoğraf. */
type Photo = { key: string; uri: string; url?: string; base64?: string };

export default function Review() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { session, ready } = useSession();
  const [stars, setStars] = useState(0);
  const [text, setText] = useState('');
  const [tags, setTags] = useState<string[]>([]);
  const [photos, setPhotos] = useState<Photo[]>([]);
  const [editing, setEditing] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const userId = session?.userId;

  useEffect(() => {
    if (ready && !session) {
      router.replace('/auth');
    }
  }, [ready, session, router]);

  // Daha önce yorum yazdıysa formu onunla doldur: gönderim yeni yorum değil, güncelleme olur.
  useEffect(() => {
    if (!id || !userId) {
      return;
    }
    let alive = true;
    api
      .reviews(id)
      .then((list) => {
        const mine = (list ?? []).find((r) => r.userId === userId);
        if (alive && mine) {
          setEditing(true);
          setStars(mine.stars);
          setText(mine.text ?? '');
          setTags(mine.tags ?? []);
          setPhotos((mine.photoUrls ?? []).map((url) => ({ key: url, url, uri: mediaUrl(url) ?? url })));
        }
      })
      .catch(() => {})
      .finally(() => {
        if (alive) {
          setLoading(false);
        }
      });
    return () => {
      alive = false;
    };
  }, [id, userId]);

  async function pick() {
    const room = REVIEW_MAX_PHOTOS - photos.length;
    if (room <= 0) {
      return;
    }
    // Sistem fotoğraf seçicisi izin istemez (Android PickVisualMedia); galeri izni pakette yok.
    const picked = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      quality: 0.6,
      base64: true,
      allowsMultipleSelection: true,
      selectionLimit: room,
    });
    if (picked.canceled) {
      return;
    }
    const added = picked.assets
      .filter((a) => a.base64)
      .slice(0, room)
      .map((a, i) => ({ key: `${Date.now()}-${i}`, uri: a.uri, base64: a.base64! }));
    setPhotos((prev) => [...prev, ...added]);
    setError(null);
  }

  async function submit() {
    if (!stars) {
      haptic.warn();
      setError('Önce puanını seç.');
      return;
    }
    if (text.length > REVIEW_MAX) {
      setError(`Yorum en fazla ${REVIEW_MAX} karakter olabilir.`);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const urls: string[] = [];
      for (const p of photos) {
        if (p.url) {
          urls.push(p.url);
          continue;
        }
        const up = await api.uploadPhoto(p.base64!);
        if (!up?.url) {
          throw new Error('Fotoğraf yüklenemedi. Daha küçük bir fotoğraf dene.');
        }
        urls.push(up.url);
      }
      await api.writeReview(id!, stars, text.trim(), tags, urls);
      haptic.success();
      toast.success(editing ? 'Yorumun güncellendi' : 'Yorumun yayında. Teşekkürler!');
      router.back();
    } catch (e) {
      haptic.warn();
      setError(friendlyError(e));
    } finally {
      setBusy(false);
    }
  }

  if (loading) {
    return (
      <View style={[styles.bg, styles.center]}>
        <ActivityIndicator color={colors.coral} />
      </View>
    );
  }

  return (
    <KeyboardAvoidingView style={styles.bg} behavior={Platform.OS === 'ios' ? 'padding' : undefined} keyboardVerticalOffset={64}>
      <ScrollView contentContainerStyle={styles.wrap} keyboardShouldPersistTaps="handled" showsVerticalScrollIndicator={false}>
        <Text style={styles.h}>{editing ? 'Yorumunu güncelle' : 'Nasıldı?'}</Text>
        <StarInput value={stars} onChange={setStars} />

        <TextField
          label="Deneyimin"
          placeholder="Ortam, servis, lezzet… Başkalarına ne söylemek istersin?"
          value={text}
          onChangeText={setText}
          multiline
          maxLength={REVIEW_MAX}
          counter={REVIEW_MAX}
          hint="İsteğe bağlı"
        />

        <Text style={styles.label}>Burası nasıl bir yer?</Text>
        <View style={styles.row}>
          {VIBES.map((v) => {
            const on = tags.includes(v);
            return (
              <Pressable
                key={v}
                style={[styles.chip, on && styles.on]}
                onPress={() => {
                  haptic.tap();
                  setTags(on ? tags.filter((t) => t !== v) : tags.length >= 5 ? tags : [...tags, v]);
                }}
                accessibilityRole="checkbox"
                accessibilityState={{ checked: on }}
                accessibilityLabel={vibeLabel(v)}>
                <Text style={[styles.chipText, on && styles.chipTextOn]}>{vibeLabel(v)}</Text>
              </Pressable>
            );
          })}
        </View>

        <Text style={styles.label}>
          Fotoğraflar <Text style={styles.muted}>({photos.length}/{REVIEW_MAX_PHOTOS})</Text>
        </Text>
        <View style={styles.row}>
          {photos.map((p) => (
            <View key={p.key} style={styles.thumb}>
              <Image source={{ uri: p.uri }} style={StyleSheet.absoluteFill} contentFit="cover" />
              <Pressable
                style={styles.remove}
                onPress={() => setPhotos((prev) => prev.filter((x) => x.key !== p.key))}
                hitSlop={8}
                accessibilityRole="button"
                accessibilityLabel="Fotoğrafı kaldır">
                <Ionicons name="close" size={14} color={colors.white} />
              </Pressable>
            </View>
          ))}
          {photos.length < REVIEW_MAX_PHOTOS ? (
            <Pressable style={[styles.thumb, styles.add]} onPress={() => void pick()} accessibilityRole="button" accessibilityLabel="Fotoğraf ekle">
              <Ionicons name="camera-outline" size={24} color={colors.coral} />
              <Text style={styles.addText}>Ekle</Text>
            </Pressable>
          ) : null}
        </View>

        {error ? (
          <View style={styles.err} accessibilityRole="alert">
            <Ionicons name="alert-circle" size={18} color={colors.danger} />
            <Text style={styles.errText}>{error}</Text>
          </View>
        ) : null}
      </ScrollView>
      <View style={[styles.foot, { paddingBottom: insets.bottom + 12 }]}>
        <Button label={editing ? 'Güncelle' : 'Yorumu gönder'} onPress={() => void submit()} loading={busy} />
      </View>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  bg: { flex: 1, backgroundColor: colors.bg },
  center: { alignItems: 'center', justifyContent: 'center' },
  wrap: { padding: 20, gap: 14, paddingBottom: 32 },
  h: { fontFamily: fonts.display, fontSize: 28, color: colors.ink, textAlign: 'center', marginBottom: 4 },
  label: { color: colors.ink, fontFamily: fonts.sansBold, fontSize: 13, marginTop: 4 },
  muted: { color: colors.muted, fontFamily: fonts.sans },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: {
    paddingHorizontal: 14,
    minHeight: 44,
    justifyContent: 'center',
    borderRadius: 22,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
  },
  on: { backgroundColor: colors.coral, borderColor: colors.coral },
  chipText: { color: colors.ink, fontFamily: fonts.sansMed, fontSize: 14 },
  chipTextOn: { color: colors.white, fontFamily: fonts.sansBold },
  thumb: { width: 76, height: 76, borderRadius: 14, overflow: 'hidden', backgroundColor: colors.line },
  add: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.surface,
    borderWidth: 1.5,
    borderColor: colors.line,
    borderStyle: 'dashed',
    gap: 2,
  },
  addText: { color: colors.coral, fontFamily: fonts.sansBold, fontSize: 12 },
  remove: {
    position: 'absolute',
    top: 4,
    right: 4,
    width: 22,
    height: 22,
    borderRadius: 11,
    backgroundColor: 'rgba(0,0,0,0.6)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  err: { flexDirection: 'row', alignItems: 'center', gap: 8, backgroundColor: colors.dangerSoft, borderRadius: 12, padding: 12 },
  errText: { flex: 1, color: colors.espresso, fontFamily: fonts.sansMed, fontSize: 14 },
  foot: { paddingHorizontal: 20, paddingTop: 12, borderTopWidth: 1, borderTopColor: colors.line, backgroundColor: colors.bg },
});
