// Yerel görsellerin küçültülmüş sürümü (sunucu: /media/venues|uploads/…?w=400|800|1200).
// React Native içe aktarmaz: node ile test edilir. Sunucudaki karşılığı: Nereye.Domain.Media.ImageVariants.

export const VARIANT_WIDTHS = [400, 800, 1200] as const;

/** Kart görselleri için tek ölçü (164–200 pt arası kartlar aynı sürüme düşer; önden indirme boşa gitmez). */
export const CARD_IMAGE_PT = 200;

/** Gösterilecek genişlik (pt) x piksel yoğunluğu → sürüm genişliği; null = özgün dosya. */
export function variantWidth(points: number, pixelRatio: number): number | null {
  const px = Math.round(points * pixelRatio);
  if (!(px > 0)) {
    return null;
  }
  return VARIANT_WIDTHS.find((w) => w >= px) ?? null;
}

const VARIANT_PATH = /\/media\/(venues|uploads)\/[^?#]+\.(jpe?g|png|webp)(\?|#|$)/i;

export function withVariant(uri: string, width: number | null | undefined): string {
  if (!width || !VARIANT_PATH.test(uri) || /[?&]w=/.test(uri)) {
    return uri;
  }
  return `${uri}${uri.includes('?') ? '&' : '?'}w=${width}`;
}
