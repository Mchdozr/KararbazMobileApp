import { INTENT_PHOTOS } from '@/lib/photos';
import { colors, fonts } from '@/lib/theme';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { Pressable, StyleSheet, Text, View } from 'react-native';

export function PromoBanner({ onOpenNow }: { onOpenNow: () => void }) {
  return (
    <Pressable style={styles.wrap} onPress={onOpenNow} accessibilityRole="button" accessibilityLabel="Şimdi açık mekanlara bak">
      <Image source={INTENT_PHOTOS.AcikHava} style={StyleSheet.absoluteFill} contentFit="cover" />
      <LinearGradient colors={[colors.scrimSoft, colors.scrim]} style={StyleSheet.absoluteFill} />
      <View style={styles.copy}>
        <Text style={styles.kicker}>Şimdi açık</Text>
        <Text style={styles.title}>Sahil, kahve,{'\n'}canlı bir masa.</Text>
        <View style={styles.cta}>
          <Text style={styles.ctaText}>Açık mekanlar</Text>
        </View>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  wrap: {
    marginHorizontal: 20,
    marginTop: 18,
    height: 168,
    borderRadius: 24,
    overflow: 'hidden',
    backgroundColor: colors.espresso,
    justifyContent: 'flex-end',
  },
  copy: { padding: 18 },
  kicker: { color: colors.coralSoft, fontWeight: '700', letterSpacing: 1.2, fontSize: 11, textTransform: 'uppercase' },
  title: { fontFamily: fonts.display, color: colors.white, fontSize: 26, lineHeight: 30, marginTop: 6 },
  cta: {
    alignSelf: 'flex-start',
    marginTop: 12,
    backgroundColor: colors.coral,
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderRadius: 999,
  },
  ctaText: { color: colors.onCoral, fontWeight: '800', fontSize: 13 },
});
