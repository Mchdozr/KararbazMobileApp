// Android ana ekran bileşeninin verisi. Uygulama ana sayfa listesi gelince bu anlık görüntüyü belge klasörüne
// (Android: context.filesDir) widget-nearby.json olarak yazar; bileşen (plugins/withNearbyWidget.js) oradan okur.
// React Native içe aktarmaz: node ile test edilir.
import { intentName } from './intent-name.ts';
import { distanceReason } from './decide.ts';
import { t } from './i18n.ts';
import { openLine } from './open-status.ts';
import type { VenueCard } from './types.ts';

export const WIDGET_FILE = 'widget-nearby';
const IST_OFFSET_MS = 3 * 60 * 60 * 1000;

export type WidgetSnapshot = {
  updatedAt: number;
  updatedLabel: string;
  title: string;
  items: { placeId: string; name: string; meta: string }[];
};

export function widgetSnapshot(
  cards: readonly VenueCard[],
  ctx: { label: string; intent: string; now: number; nowMinutes: number },
): WidgetSnapshot {
  const items = cards
    .filter((c) => c.kind !== 'Sponsored' && c.openNow === true)
    .slice(0, 3)
    .map((c) => {
      const parts = [openLine(c, ctx.nowMinutes)?.text, distanceReason(c.distanceMeters)?.text].filter(Boolean);
      return { placeId: c.placeId, name: c.name, meta: parts.join(' · ') };
    });
  const d = new Date(ctx.now + IST_OFFSET_MS);
  const hhmm = `${String(d.getUTCHours()).padStart(2, '0')}:${String(d.getUTCMinutes()).padStart(2, '0')}`;
  return {
    updatedAt: ctx.now,
    updatedLabel: t('Güncelleme {time}', { time: hhmm }),
    title: `${intentName(ctx.intent)} · ${ctx.label}`,
    items,
  };
}
