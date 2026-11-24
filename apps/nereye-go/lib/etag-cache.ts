// Koşullu GET için küçük bellek önbelleği: sunucu ETag verdiyse gövde saklanır, sonraki istekte
// If-None-Match gönderilir; 304 gelince gövde buradan okunur (mobil veride liste yeniden inmez).
// React Native içe aktarmaz: node ile test edilir.

export type RawResult = { ok: boolean; status: number; text: string };

type Entry = { etag: string; text: string };

export class EtagCache {
  private readonly entries = new Map<string, Entry>();
  private readonly limit: number;
  private readonly maxBytes: number;

  constructor(limit = 40, maxBytes = 400_000) {
    this.limit = limit;
    this.maxBytes = maxBytes;
  }

  get size() {
    return this.entries.size;
  }

  headers(key: string): Record<string, string> {
    const hit = this.entries.get(key);
    return hit ? { 'If-None-Match': hit.etag } : {};
  }

  remember(key: string, status: number, etag: string | null | undefined, text: string) {
    if (status !== 200 || !etag || !text || text.length > this.maxBytes) {
      return;
    }
    this.entries.delete(key);
    this.entries.set(key, { etag, text });
    while (this.entries.size > this.limit) {
      const oldest = this.entries.keys().next().value;
      if (oldest === undefined) {
        break;
      }
      this.entries.delete(oldest);
    }
  }

  /** 304 ise saklanan gövdeyi 200 olarak döner; aksi halde sonuç olduğu gibi. */
  resolve(key: string, result: RawResult): RawResult {
    if (result.status !== 304) {
      return result;
    }
    const hit = this.entries.get(key);
    if (!hit) {
      return result;
    }
    this.entries.delete(key);
    this.entries.set(key, hit);
    return { ok: true, status: 200, text: hit.text };
  }
}
