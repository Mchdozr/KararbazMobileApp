import { useCallback, useEffect, useRef } from 'react';

/**
 * Ekranın "yalnızca son istek önemli" kuralı: her next() çağrısı bir önceki isteği ağda durdurur.
 * Ekran kapanınca süren istek de durur. (Sunucu soğuk liste çekimini yine de bitirip önbelleğe yazar.)
 */
export function useLatestRequest() {
  const current = useRef<AbortController | null>(null);
  useEffect(() => () => current.current?.abort(), []);
  return useCallback(() => {
    current.current?.abort();
    const ctrl = new AbortController();
    current.current = ctrl;
    return ctrl.signal;
  }, []);
}
