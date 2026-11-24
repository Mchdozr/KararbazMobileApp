// Hata raporu kuyruğu. React Native içe aktarmaz: node ile test edilir. Sınırlar sunucudaki ClientErrorRules ile aynı.
import { scrub } from './scrub.ts';

export type CrashReport = {
  message: string;
  stack?: string;
  screen?: string;
  platform: string;
  appVersion?: string;
  fatal: boolean;
};

export const MAX_BATCH = 10;
const MAX_SESSION = 30;
const MAX_SAME = 3;

export function toReport(
  error: unknown,
  ctx: { fatal: boolean; platform: string; screen?: string; appVersion?: string; extraStack?: string },
): CrashReport {
  let message: string;
  let stack: string | undefined;
  if (error instanceof Error) {
    message = `${error.name}: ${error.message}`;
    stack = error.stack;
  } else if (typeof error === 'string') {
    message = error;
  } else {
    message = 'Bilinmeyen hata';
  }
  if (ctx.extraStack) {
    stack = `${stack ?? ''}\n${ctx.extraStack}`.trim();
  }
  return {
    message: scrub(message, 500),
    stack: stack ? scrub(stack, 4000) : undefined,
    screen: ctx.screen ? scrub(ctx.screen, 80) : undefined,
    platform: ctx.platform,
    appVersion: ctx.appVersion?.slice(0, 40),
    fatal: ctx.fatal,
  };
}

function keyOf(r: CrashReport) {
  const frame = (r.stack ?? '').split('\n').find((l) => l.trim().startsWith('at')) ?? '';
  return `${r.message.replace(/\d+/g, '#')}|${frame.replace(/\d+/g, '#')}`;
}

/** Oturumda sel olmasın: aynı hata en fazla 3, toplam 30 rapor. */
export class CrashQueue {
  private queue: CrashReport[] = [];
  private seen = new Map<string, number>();
  private total = 0;

  get pending() {
    return this.queue.length;
  }

  add(report: CrashReport): boolean {
    const key = keyOf(report);
    const n = this.seen.get(key) ?? 0;
    if (n >= MAX_SAME || this.total >= MAX_SESSION) {
      return false;
    }
    this.seen.set(key, n + 1);
    this.total++;
    this.queue.push(report);
    return true;
  }

  take(): CrashReport[] {
    return this.queue.splice(0, MAX_BATCH);
  }

  /** Gönderilemeyen paket sıranın başına döner. */
  restore(batch: CrashReport[]) {
    this.queue = [...batch, ...this.queue].slice(0, MAX_SESSION);
  }
}
