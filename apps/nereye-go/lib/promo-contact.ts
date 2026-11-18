// İşletmenin "öne çıkarma" için başvuracağı adres. React Native içe aktarmaz: node ile test edilir.

/**
 * v1'de uygulama içi tahsilat yok. Adres insan tarafından derlemeye verilir
 * (`EXPO_PUBLIC_PROMOTION_CONTACT_URL`, https ya da mailto). Yoksa null: düğme kapalı kalır.
 */
export function promoContactUrl(raw?: string | null): string | null {
  const value = raw?.trim() ?? '';
  if (/^https:\/\/[^\s/]+/i.test(value)) {
    return value;
  }
  return /^mailto:[^\s@]+@[^\s@]+\.[^\s@]+$/i.test(value) ? value : null;
}
