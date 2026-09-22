const DAYS = [
  { keys: ['mo', 'monday', 'pzt', 'pazartesi'], label: 'Pzt' },
  { keys: ['tu', 'tuesday', 'sal', 'salı'], label: 'Sal' },
  { keys: ['we', 'wednesday', 'çar', 'car', 'çarşamba'], label: 'Çar' },
  { keys: ['th', 'thursday', 'per', 'perşembe'], label: 'Per' },
  { keys: ['fr', 'friday', 'cum', 'cuma'], label: 'Cum' },
  { keys: ['sa', 'saturday', 'cmt', 'cumartesi'], label: 'Cmt' },
  { keys: ['su', 'sunday', 'paz', 'pazar'], label: 'Paz' },
] as const;

export type HourRow = { day: string; hours: string };

const RANGE = /\b(\d{1,2}:\d{2})\s*[-–]\s*(\d{1,2}:\d{2})\b/;

export function parseOpeningHours(raw?: string | null): HourRow[] {
  if (!raw?.trim()) {
    return [];
  }

  const chunks = raw
    .split(/[;\n|]+/)
    .map((s) => s.trim())
    .filter(Boolean);
  const byDay = new Map<string, string>();

  for (const chunk of chunks) {
    const hours = RANGE.exec(chunk);
    const off = /off|kapalı|closed/i.test(chunk);
    const text = off ? 'Kapalı' : hours ? `${hours[1]}–${hours[2]}` : chunk.replace(/^[^:]+:\s*/, '').trim();
    const labels = expandDays(chunk);
    for (const label of labels) {
      byDay.set(label, text || 'Kapalı');
    }
  }

  if (byDay.size === 0) {
    return [];
  }

  return DAYS.map((d) => ({ day: d.label, hours: byDay.get(d.label) ?? '—' }));
}

function expandDays(chunk: string): string[] {
  const lower = chunk.toLocaleLowerCase('tr-TR');
  const found: string[] = [];
  for (const d of DAYS) {
    if (d.keys.some((k) => lower.includes(k))) {
      found.push(d.label);
    }
  }
  const span = lower.match(/\b(mo|tu|we|th|fr|sa|su|pzt|sal|çar|car|per|cum|cmt|paz)\s*[-–]\s*(mo|tu|we|th|fr|sa|su|pzt|sal|çar|car|per|cum|cmt|paz)\b/);
  if (span) {
    const a = indexOfKey(span[1]);
    const b = indexOfKey(span[2]);
    if (a >= 0 && b >= 0) {
      const out: string[] = [];
      for (let i = a; ; i = (i + 1) % DAYS.length) {
        out.push(DAYS[i].label);
        if (i === b) {
          break;
        }
      }
      return out;
    }
  }
  return found;
}

function indexOfKey(token: string) {
  return DAYS.findIndex((d) => (d.keys as readonly string[]).includes(token));
}
