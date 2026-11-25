// Ana sayfadaki hava notu. React Native içe aktarmaz: node ile test edilir.
export type WeatherInfo = { kind: string; temperatureC: number; note?: string | null; suggestIntent?: string | null };

type Icon = 'sunny-outline' | 'partly-sunny-outline' | 'rainy-outline' | 'snow-outline' | 'thunderstorm-outline' | 'cloudy-outline';

const ICONS: Record<string, Icon> = {
  Clear: 'sunny-outline',
  Cloudy: 'partly-sunny-outline',
  Fog: 'cloudy-outline',
  Rain: 'rainy-outline',
  Snow: 'snow-outline',
  Storm: 'thunderstorm-outline',
};

const ACTION_LABELS: Record<string, string> = { AcikHava: 'Açık havaya bak', Kultur: 'Kültüre bak' };

/** Not yoksa (sıradan hava) hiçbir şey gösterilmez. */
export function weatherView(w: WeatherInfo | null | undefined) {
  if (!w?.note) {
    return null;
  }
  const intent = w.suggestIntent ?? undefined;
  return {
    icon: ICONS[w.kind] ?? 'partly-sunny-outline',
    text: w.note,
    action: intent && ACTION_LABELS[intent] ? { intent, label: ACTION_LABELS[intent] } : undefined,
  };
}
