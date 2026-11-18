import { BackButton } from '@/components/BackButton';
import { isEvening } from '@/lib/copy';
import { colors, fonts, gradients } from '@/lib/theme';
import { LinearGradient } from 'expo-linear-gradient';
import { StatusBar } from 'expo-status-bar';
import type { ReactNode } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/**
 * Uygulamanın ortak üst alanı: marka yeşili degrade, kavisli alt kenar, büyük başlık.
 * Ana sayfadaki menüyle aynı dil. `children` başlığın altına (ör. arama kutusu, filtre çipleri) girer;
 * `overlap` verilirse alt kenara o kadar boşluk bırakılır ve içerik kenardan taşabilir.
 */
export function ScreenHeader({
  title,
  subtitle,
  kicker,
  back = false,
  right,
  children,
  overlap = 0,
  compact = false,
}: {
  title: string;
  subtitle?: string;
  /** Başlığın üstündeki küçük etiket. */
  kicker?: string;
  back?: boolean;
  right?: ReactNode;
  children?: ReactNode;
  overlap?: number;
  /** Gezinme başlığı olarak: daha kısa, başlık geri düğmesinin yanında. */
  compact?: boolean;
}) {
  const insets = useSafeAreaInsets();
  return (
    <LinearGradient
      colors={isEvening() ? gradients.evening : gradients.hero}
      style={[styles.wrap, { paddingTop: insets.top + (compact ? 8 : 12), paddingBottom: (compact ? 18 : 22) + overlap }]}>
      <StatusBar style="light" />
      {compact ? (
        <View style={styles.compactRow}>
          {back ? <BackButton light /> : <View style={styles.spacer} />}
          <View style={styles.compactText}>
            <Text style={styles.compactTitle} numberOfLines={1} accessibilityRole="header">
              {title}
            </Text>
            {subtitle ? (
              <Text style={styles.compactSub} numberOfLines={1}>
                {subtitle}
              </Text>
            ) : null}
          </View>
          {right ?? <View style={styles.spacer} />}
        </View>
      ) : (
        <>
          {back || right ? (
            <View style={styles.bar}>
              {back ? <BackButton light /> : <View />}
              {right}
            </View>
          ) : null}
          {kicker ? <Text style={styles.kicker}>{kicker}</Text> : null}
          <Text style={styles.title} accessibilityRole="header">
            {title}
          </Text>
          {subtitle ? <Text style={styles.sub}>{subtitle}</Text> : null}
        </>
      )}
      {children}
    </LinearGradient>
  );
}

/** Yığın ekranları için gezinme başlığı: `options={{ header: gradientHeader('alt başlık') }}`. */
export function gradientHeader(subtitle?: string) {
  return function Header({ options }: { options: { title?: string } }) {
    return <ScreenHeader compact back title={options.title ?? ''} subtitle={subtitle} />;
  };
}

const styles = StyleSheet.create({
  wrap: {
    paddingHorizontal: 20,
    borderBottomLeftRadius: 28,
    borderBottomRightRadius: 28,
    zIndex: 2,
  },
  bar: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 },
  kicker: {
    color: colors.onDarkMuted,
    fontFamily: fonts.sansBold,
    fontSize: 12,
    letterSpacing: 1.2,
    textTransform: 'uppercase',
    marginBottom: 4,
  },
  title: { fontFamily: fonts.display, fontSize: 32, lineHeight: 38, color: colors.white },
  sub: { color: colors.onDarkMuted, fontFamily: fonts.sans, fontSize: 15, lineHeight: 21, marginTop: 4 },
  compactRow: { flexDirection: 'row', alignItems: 'center', gap: 12, minHeight: 48 },
  compactText: { flex: 1, alignItems: 'center' },
  compactTitle: { fontFamily: fonts.display, fontSize: 21, color: colors.white },
  compactSub: { color: colors.onDarkMuted, fontFamily: fonts.sans, fontSize: 13, marginTop: 2 },
  spacer: { width: 44 },
});
