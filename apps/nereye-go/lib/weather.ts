// Ana sayfadaki hava notu. React Native içe aktarmaz: node ile test edilir.
import { language, t } from './i18n.ts';
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

/** İngilizcede not istemcide kurulur (sunucu notu Türkçe). */
function englishNote(w: WeatherInfo): string {
  const deg = `${Math.round(w.temperatureC)}°`;
  if (w.kind === 'Rain' || w.kind === 'Snow' || w.kind === 'Storm') {
    const what = w.kind === 'Snow' ? "It's snowing" : w.kind === 'Storm' ? "There's a storm" : "It's raining";
    return w.suggestIntent === 'Kultur'
      ? `${what} (${deg}). You may get wet outdoors; museums and indoor places may be better.`
      : `${what} (${deg}). We moved outdoor places down and indoor places up.`;
  }
  return w.suggestIntent === 'AcikHava'
    ? `Lovely weather (${deg}). We moved places with outdoor seating up; try the outdoors too.`
    : `Lovely weather (${deg}). Perfect time to be outdoors.`;
}

/** Not yoksa (sıradan hava) hiçbir şey gösterilmez. */
export function weatherView(w: WeatherInfo | null | undefined) {
  if (!w?.note) {
    return null;
  }
  const intent = w.suggestIntent ?? undefined;
  return {
    icon: ICONS[w.kind] ?? 'partly-sunny-outline',
    text: language() === 'en' ? englishNote(w) : w.note,
    action: intent && ACTION_LABELS[intent] ? { intent, label: t(ACTION_LABELS[intent]) } : undefined,
  };
}
