import { colors, fonts, radii } from '@/lib/theme';
import { Pressable, StyleSheet, Text, View } from 'react-native';

export function EmptyState({
  title,
  action,
  onAction,
}: {
  title: string;
  action?: string;
  onAction?: () => void;
}) {
  return (
    <View style={styles.wrap}>
      <Text style={styles.title}>{title}</Text>
      {action && onAction ? (
        <Pressable
          style={styles.btn}
          onPress={onAction}
          accessibilityRole="button"
          accessibilityLabel={action}
          hitSlop={8}>
          <Text style={styles.btnText}>{action}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { paddingHorizontal: 20, paddingVertical: 16, gap: 10 },
  title: { color: colors.muted, fontSize: 15, lineHeight: 22, fontFamily: fonts.sans },
  btn: {
    alignSelf: 'flex-start',
    backgroundColor: colors.coral,
    paddingHorizontal: 14,
    paddingVertical: 10,
    minHeight: 44,
    borderRadius: radii.sm,
    justifyContent: 'center',
  },
  btnText: { color: colors.white, fontWeight: '800' },
});
