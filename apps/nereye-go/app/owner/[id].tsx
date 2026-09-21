import { api } from '@/lib/api';
import { friendlyError } from '@/lib/empty';
import { colors, fonts } from '@/lib/theme';
import type { PromotionItem } from '@/lib/types';
import { useSession } from '@/lib/session';
import * as ImagePicker from 'expo-image-picker';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';

export default function Owner() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { session } = useSession();
  const router = useRouter();
  const owner = Boolean(session?.roles.includes('VenueOwner'));
  const [message, setMessage] = useState('İşletmenin sahibiyim.');
  const [name, setName] = useState('');
  const [desc, setDesc] = useState('');
  const [reply, setReply] = useState('');
  const [reviewId, setReviewId] = useState('');
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [mine, setMine] = useState<PromotionItem[]>([]);
  const [geohash, setGeohash] = useState<string | null>(null);

  useEffect(() => {
    if (!session) {
      router.replace('/auth');
      return;
    }
    void (async () => {
      try {
        const v = await api.venue(id!);
        setGeohash(v.geohash ?? null);
        setName(v.name);
        setDesc(v.ownerDescription ?? '');
      } catch {
        /* ignore */
      }
      if (owner) {
        try {
          setMine(await api.myPromotions());
        } catch {
          setMine([]);
        }
      }
    })();
  }, [id, session, owner, router]);

  return (
    <ScrollView style={styles.wrap} contentContainerStyle={{ paddingBottom: 40, gap: 12 }}>
      <Text style={styles.h}>Sahiplenme</Text>
      <TextInput style={styles.input} value={message} onChangeText={setMessage} />
      <Pressable
        style={styles.btn}
        onPress={async () => {
          setError(null);
          try {
            await api.claim(id!, message);
            setStatus('Talep gönderildi. Admin onayından sonra yeniden giriş yap.');
          } catch (e) {
            setError(friendlyError(e));
          }
        }}>
        <Text style={styles.btnText}>Sahiplen</Text>
      </Pressable>
      {owner ? (
        <>
          <Text style={styles.h}>Profil</Text>
          <TextInput style={styles.input} placeholder="Görünen ad" placeholderTextColor={colors.muted} value={name} onChangeText={setName} />
          <TextInput
            style={[styles.input, { minHeight: 80 }]}
            placeholder="Açıklama"
            placeholderTextColor={colors.muted}
            value={desc}
            onChangeText={setDesc}
            multiline
          />
          <Pressable
            style={styles.btn}
            onPress={async () => {
              try {
                await api.updateOwner(id!, name, desc);
                setStatus('Profil güncellendi.');
              } catch (e) {
                setError(friendlyError(e));
              }
            }}>
            <Text style={styles.btnText}>Kaydet</Text>
          </Pressable>
          <Pressable
            style={styles.outline}
            onPress={async () => {
              const perm = await ImagePicker.requestMediaLibraryPermissionsAsync();
              if (perm.status !== 'granted') {
                setError('Galeri izni gerekir.');
                return;
              }
              const picked = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], quality: 0.72, base64: true });
              const asset = picked.assets?.[0];
              if (picked.canceled || !asset?.base64) {
                return;
              }
              try {
                await api.uploadOwnerPhoto(id!, asset.base64);
                setStatus('Fotoğraf yüklendi.');
              } catch (e) {
                setError(friendlyError(e));
              }
            }}>
            <Text style={styles.outlineText}>Fotoğraf yükle</Text>
          </Pressable>
          <Text style={styles.h}>Yorum yanıtı</Text>
          <TextInput style={styles.input} placeholder="Yorum id" placeholderTextColor={colors.muted} value={reviewId} onChangeText={setReviewId} />
          <TextInput style={styles.input} placeholder="Yanıt" placeholderTextColor={colors.muted} value={reply} onChangeText={setReply} />
          <Pressable
            style={styles.outline}
            onPress={async () => {
              try {
                await api.replyReview(reviewId, reply);
                setStatus('Yanıtlandı.');
              } catch (e) {
                setError(friendlyError(e));
              }
            }}>
            <Text style={styles.outlineText}>Yanıtla</Text>
          </Pressable>
          <Text style={styles.h}>Öne çıkarma</Text>
          {mine.map((p) => (
            <Text key={p.id} style={styles.status}>
              {p.targetIntent} · {p.status} · {p.targetGeohash}
            </Text>
          ))}
          <Pressable
            style={styles.btn}
            onPress={async () => {
              try {
                const campaign = await api.createPromotion({
                  placeId: id,
                  targetIntent: 'Kahve',
                  targetGeohash: geohash ?? '',
                  days: 7,
                  dailyBudget: 150,
                });
                const paid = await api.checkout(campaign.id);
                setStatus(`Kampanya ${paid.status}`);
                setMine(await api.myPromotions());
              } catch (e) {
                setError(friendlyError(e));
              }
            }}>
            <Text style={styles.btnText}>Sandbox öde</Text>
          </Pressable>
        </>
      ) : null}
      {error ? <Text style={styles.err}>{error}</Text> : null}
      {status ? <Text style={styles.status}>{status}</Text> : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  wrap: { flex: 1, padding: 16, backgroundColor: colors.bg },
  h: { fontSize: 22, fontFamily: fonts.display, color: colors.ink, marginTop: 8 },
  input: { borderRadius: 14, padding: 12, backgroundColor: colors.surface, color: colors.ink },
  btn: { backgroundColor: colors.coral, padding: 14, minHeight: 44, borderRadius: 16, alignItems: 'center' },
  btnText: { color: colors.white, fontWeight: '800' },
  outline: { padding: 14, minHeight: 44, borderRadius: 14, borderWidth: 1, borderColor: colors.line, alignItems: 'center' },
  outlineText: { color: colors.ink, fontWeight: '700' },
  status: { color: '#2E7D4F', fontWeight: '600' },
  err: { color: colors.coral },
});
