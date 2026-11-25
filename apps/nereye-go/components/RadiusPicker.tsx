import { RADIUS_OPTIONS } from '@/lib/constants';
import { clampRadius, kmToMeters, metersToKm, radiusLabel } from '@/lib/radius';
import { colors, fonts, radii } from '@/lib/theme';
import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';

export function RadiusPicker({
  meters,
  onChange,
}: {
  meters: number;
  onChange: (meters: number) => void;
}) {
  const [text, setText] = useState(String(metersToKm(meters)));

  useEffect(() => {
    setText(String(metersToKm(meters)));
  }, [meters]);

  function commit(raw: string) {
    const km = Number(raw.replace(',', '.'));
    if (!Number.isFinite(km)) {
      setText(String(metersToKm(meters)));
      return;
    }
    onChange(kmToMeters(km));
  }

  return (
    <View>
      <View style={styles.row}>
        {RADIUS_OPTIONS.map((opt) => {
          const on = meters === opt.meters;
          return (
            <Pressable
              key={opt.meters}
              style={[styles.chip, on && styles.chipOn]}
              onPress={() => onChange(opt.meters)}
              accessibilityRole="button"
              accessibilityLabel={`${opt.label} ${opt.sub}`}
              accessibilityState={{ selected: on }} aria-selected={on}>
              <Text style={[styles.chipText, on && styles.chipTextOn]}>{opt.label}</Text>
            </Pressable>
          );
        })}
      </View>
      <View style={styles.custom}>
        <Text style={styles.hint}>Kendi mesafen (km)</Text>
        <View style={styles.inputRow}>
          <TextInput
            value={text}
            onChangeText={setText}
            onEndEditing={(e) => commit(e.nativeEvent.text)}
            onSubmitEditing={(e) => commit(e.nativeEvent.text)}
            keyboardType="decimal-pad"
            style={styles.input}
            accessibilityLabel="Arama çapı kilometre"
          />
          <Text style={styles.unit}>km</Text>
          <Text style={styles.now}>{radiusLabel(clampRadius(meters))}</Text>
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: {
    paddingHorizontal: 12,
    paddingVertical: 10,
    minHeight: 44,
    borderRadius: radii.pill,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.line,
    justifyContent: 'center',
  },
  chipOn: { backgroundColor: colors.coral, borderColor: colors.coral },
  chipText: { color: colors.ink, fontWeight: '700' },
  chipTextOn: { color: colors.white },
  custom: { marginTop: 12 },
  hint: { color: colors.muted, fontWeight: '700', fontSize: 12, marginBottom: 8 },
  inputRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  input: {
    minWidth: 88,
    minHeight: 44,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: colors.line,
    backgroundColor: colors.surface,
    paddingHorizontal: 12,
    color: colors.ink,
    fontFamily: fonts.sansBold,
    fontSize: 16,
  },
  unit: { color: colors.muted, fontWeight: '800' },
  now: { color: colors.ink, fontWeight: '700', marginLeft: 'auto' },
});
