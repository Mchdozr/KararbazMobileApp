import { Button } from '@/components/Button';
import { colors, fonts } from '@/lib/theme';
import { Ionicons } from '@expo/vector-icons';
import type { ComponentProps } from 'react';
import { StyleSheet, Text, View } from 'react-native';

export function EmptyState({
  title,
  body,
  icon = 'compass-outline',
  action,
  onAction,
  secondary,
  onSecondary,
}: {
  title: string;
  body?: string;
  icon?: ComponentProps<typeof Ionicons>['name'];
  action?: string;
  onAction?: () => void;
  secondary?: string;
  onSecondary?: () => void;
}) {
  return (
    <View style={styles.wrap}>
      <View style={styles.icon}>
        <Ionicons name={icon} size={26} color={colors.sage} />
      </View>
      <Text style={styles.title}>{title}</Text>
      {body ? <Text style={styles.body}>{body}</Text> : null}
      {action && onAction ? <Button label={action} onPress={onAction} compact style={styles.btn} /> : null}
      {secondary && onSecondary ? <Button label={secondary} onPress={onSecondary} variant="ghost" compact /> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { paddingHorizontal: 32, paddingVertical: 28, alignItems: 'center', gap: 8 },
  icon: {
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: colors.coralSoft,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 6,
  },
  title: { color: colors.ink, fontSize: 16, lineHeight: 23, fontFamily: fonts.sansMed, textAlign: 'center' },
  body: { color: colors.muted, fontSize: 14, lineHeight: 20, fontFamily: fonts.sans, textAlign: 'center' },
  btn: { marginTop: 8, alignSelf: 'center' },
});
