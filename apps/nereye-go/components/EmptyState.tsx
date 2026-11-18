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
      {/* Halka içinde ikon: boş ekran bile markanın parçası gibi dursun. */}
      <View style={styles.halo}>
        <View style={styles.icon}>
          <Ionicons name={icon} size={30} color={colors.coral} />
        </View>
      </View>
      <Text style={styles.title}>{title}</Text>
      {body ? <Text style={styles.body}>{body}</Text> : null}
      {action && onAction ? <Button label={action} onPress={onAction} compact style={styles.btn} /> : null}
      {secondary && onSecondary ? <Button label={secondary} onPress={onSecondary} variant="ghost" compact /> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { paddingHorizontal: 32, paddingVertical: 32, alignItems: 'center', gap: 8 },
  halo: {
    width: 96,
    height: 96,
    borderRadius: 48,
    backgroundColor: colors.accentSoft,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 10,
  },
  icon: {
    width: 68,
    height: 68,
    borderRadius: 34,
    backgroundColor: colors.surface,
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: { color: colors.ink, fontSize: 20, lineHeight: 26, fontFamily: fonts.display, textAlign: 'center' },
  body: { color: colors.muted, fontSize: 14, lineHeight: 21, fontFamily: fonts.sans, textAlign: 'center', maxWidth: 300 },
  btn: { marginTop: 12, alignSelf: 'center', minWidth: 160 },
});
