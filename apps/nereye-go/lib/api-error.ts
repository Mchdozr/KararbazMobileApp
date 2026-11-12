/** Sunucudan dönen hata. `message` sunucunun kullanıcıya dönük Türkçe metnidir (varsa). */
export class ApiError extends Error {
  readonly status: number;
  /** 0 = ağa hiç ulaşılamadı ya da zaman aşımı. */
  readonly kind: 'network' | 'timeout' | 'http';

  constructor(status: number, message: string, kind: ApiError['kind'] = 'http') {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.kind = kind;
  }
}

/** `{ "error": "..." }` gövdesinden mesajı çıkarır; gövde JSON değilse boş döner. */
export function serverMessage(body: string): string {
  const text = body.trim();
  if (!text.startsWith('{')) {
    return '';
  }
  try {
    const parsed = JSON.parse(text) as { error?: unknown; title?: unknown };
    if (typeof parsed.error === 'string') {
      return parsed.error;
    }
    return typeof parsed.title === 'string' ? parsed.title : '';
  } catch {
    return '';
  }
}
