import { api } from '@/lib/api';
import { CrashQueue, toReport, type CrashReport } from '@/lib/crash-queue';
import { getLargeJson, setLargeJson } from '@/lib/storage';
import Constants from 'expo-constants';
import { Platform } from 'react-native';

/**
 * Hata ve çökme raporlama. Kişisel veri cihazda ayıklanır (lib/scrub.ts), sunucuda yeniden ayıklanır;
 * rapor hesaba bağlanmaz. Ölümcül hata diske yazılır ve bir sonraki açılışta gönderilir (uygulama kapanmadan
 * ağ isteği bitmeyebilir).
 */
const queue = new CrashQueue();
const PENDING = 'crash-pending';
let screen: string | undefined;
let timer: ReturnType<typeof setTimeout> | null = null;
let installed = false;
const appVersion = Constants.expoConfig?.version;

export function setCrashScreen(path: string) {
  screen = path;
}

export function reportError(error: unknown, opts: { fatal?: boolean; componentStack?: string } = {}) {
  const report = toReport(error, {
    fatal: Boolean(opts.fatal),
    platform: Platform.OS,
    screen,
    appVersion,
    extraStack: opts.componentStack,
  });
  if (!queue.add(report)) {
    return;
  }
  if (opts.fatal) {
    void persist([report]);
  }
  schedule();
}

function schedule() {
  if (timer) {
    return;
  }
  timer = setTimeout(() => {
    timer = null;
    void flush();
  }, 3000);
}

async function flush() {
  const batch = queue.take();
  if (batch.length === 0) {
    return;
  }
  const sent = await api.reportErrors(batch);
  if (!sent) {
    queue.restore(batch);
    return;
  }
  if (batch.some((r) => r.fatal)) {
    await setLargeJson(PENDING, []);
  }
  if (queue.pending > 0) {
    schedule();
  }
}

async function persist(reports: CrashReport[]) {
  const prev = (await getLargeJson<CrashReport[]>(PENDING)) ?? [];
  await setLargeJson(PENDING, [...prev, ...reports].slice(-5));
}

type GlobalHandler = (error: unknown, isFatal?: boolean) => void;
type ErrorUtilsShape = { getGlobalHandler(): GlobalHandler; setGlobalHandler(h: GlobalHandler): void };

/** Uygulama açılışında bir kez: global yakalayıcıları kurar, önceki oturumdan kalan çökme raporunu gönderir. */
export function installCrashReporting() {
  if (installed) {
    return;
  }
  installed = true;

  void getLargeJson<CrashReport[]>(PENDING).then((saved) => {
    for (const r of saved ?? []) {
      queue.add(r);
    }
    if (saved?.length) {
      schedule();
    }
  });

  if (Platform.OS === 'web') {
    if (typeof window !== 'undefined') {
      window.addEventListener('error', (e) => reportError(e.error ?? e.message));
      window.addEventListener('unhandledrejection', (e) => reportError(e.reason));
    }
    return;
  }

  const utils = (globalThis as { ErrorUtils?: ErrorUtilsShape }).ErrorUtils;
  if (!utils) {
    return;
  }
  const previous = utils.getGlobalHandler();
  utils.setGlobalHandler((error, isFatal) => {
    reportError(error, { fatal: isFatal });
    if (isFatal) {
      // Önce diske yazılsın (en fazla yarım saniye), sonra varsayılan davranış (uygulama kapanır).
      void Promise.race([persist([]), new Promise((r) => setTimeout(r, 500))]).finally(() => previous(error, isFatal));
      return;
    }
    previous(error, isFatal);
  });
}
