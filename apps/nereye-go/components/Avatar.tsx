import { mediaUrl } from '@/lib/api';
import { colors, fonts } from '@/lib/theme';
import { Image } from 'expo-image';
import { StyleSheet, Text, View } from 'react-native';

export function initialsOf(name?: string | null) {
  const parts = (name ?? '').trim().split(/\s+/).filter(Boolean);
  const a = parts[0]?.[0] ?? 'N';
  const b = parts.length > 1 ? parts[parts.length - 1][0] : '';
  return (a + b).toLocaleUpperCase('tr-TR');
}

export function Avatar({ name, url, size = 40 }: { name?: string | null; url?: string | null; size?: number }) {
  const src = mediaUrl(url);
  return (
    <View style={[styles.wrap, { width: size, height: size, borderRadius: size / 2 }]}>
      {src ? (
        <Image source={{ uri: src }} style={StyleSheet.absoluteFill} contentFit="cover" transition={150} />
      ) : (
        <Text style={[styles.text, { fontSize: size * 0.38 }]}>{initialsOf(name)}</Text>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { backgroundColor: colors.espresso, alignItems: 'center', justifyContent: 'center', overflow: 'hidden' },
  text: { color: colors.white, fontFamily: fonts.display },
});
