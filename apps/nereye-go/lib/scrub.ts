// Hata metninden kişisel veriyi ayıklar. Sunucudaki Nereye.Domain.Telemetry.ErrorScrubber'ın aynasıdır;
// birini değiştirirsen diğerini de değiştir. React Native içe aktarmaz: node ile test edilir.

export function scrub(text: string | null | undefined, maxLength = 4000): string {
  if (!text) {
    return '';
  }
  let s = text.replace(/[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}/g, '<e-posta>');
  s = s.replace(/Bearer\s+[A-Za-z0-9._\-]+/gi, 'Bearer <anahtar>');
  s = s.replace(/eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+(\.[A-Za-z0-9_\-]+)?/g, '<anahtar>');
  s = s.replace(/((?:https?:\/\/[^\s?#"']+)|(?:\/[A-Za-z0-9_\-./[\]]+))\?[^\s"')]+/g, '$1?<sorgu>');
  s = s.replace(/-?\d{1,3}\.\d{3,}\s*,\s*-?\d{1,3}\.\d{3,}/g, '<konum>');
  s = s.replace(/\b\d{1,3}(\.\d{1,3}){3}\b/g, '<ip>');
  s = s.replace(/\+?\d[\d\s\-]{8,}\d/g, '<numara>');
  return s.length > maxLength ? s.slice(0, maxLength) : s;
}
