import { api, applySession, hydrateAuth, pickReachableApiBase, setApiBase, setAuthEvents } from '@/lib/api';
import { DEFAULT_API } from '@/lib/constants';
import { loadFavorites } from '@/lib/favorites';
import { applyPreferences } from '@/lib/filters';
import type { Session } from '@/lib/types';
import { getItem, removeItem, setItem } from '@/lib/storage';
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

type Ctx = {
  session: Session | null;
  ready: boolean;
  apiBase: string;
  setBase: (url: string) => Promise<void>;
  signIn: (s: Session) => Promise<void>;
  signOut: () => Promise<void>;
  patchSession: (partial: Partial<Session>) => Promise<void>;
};

const SessionContext = createContext<Ctx | null>(null);

async function save(key: string, value: string) {
  try {
    await setItem(key, value);
  } catch {
    /* web */
  }
}

async function remove(key: string) {
  try {
    await removeItem(key);
  } catch {
    /* web */
  }
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [ready, setReady] = useState(false);
  const [apiBase, setApiBaseState] = useState(DEFAULT_API);

  useEffect(() => {
    setAuthEvents({
      onRefreshed: (s) => {
        setSession(s);
        void save('session', JSON.stringify(s));
      },
      onCleared: () => {
        setSession(null);
        void remove('session');
      },
    });

    (async () => {
      try {
        const savedBase = await getItem('apiBase');
        const base = await pickReachableApiBase(savedBase);
        setApiBase(base);
        setApiBaseState(base);
        await save('apiBase', base);
        const raw = await getItem('session');
        if (raw) {
          const parsed = JSON.parse(raw) as Session;
          applySession(parsed);
          setSession(parsed);
          // Açılışı ağ beklemesin: token yenileme ve tercihler arka planda gelir.
          void hydrateAuth()
            .then(() => api.preferences())
            .then(applyPreferences)
            .catch(() => {});
        }
      } catch {
        setApiBase(DEFAULT_API);
      }
      setReady(true);
    })();

    return () => setAuthEvents({});
  }, []);

  const userId = session?.userId ?? null;
  useEffect(() => {
    if (ready) {
      void loadFavorites(userId).catch(() => {});
    }
  }, [ready, userId]);

  const value = useMemo<Ctx>(
    () => ({
      session,
      ready,
      apiBase,
      setBase: async (url: string) => {
        setApiBase(url);
        setApiBaseState(url);
        await save('apiBase', url);
      },
      signIn: async (s: Session) => {
        applySession(s);
        setSession(s);
        await save('session', JSON.stringify(s));
        try {
          applyPreferences(await api.preferences());
        } catch {
          /* ignore */
        }
      },
      signOut: async () => {
        const token = session?.refreshToken;
        if (token) {
          try {
            await api.logout(token);
          } catch {
            /* ignore */
          }
        }
        applySession(null);
        setSession(null);
        await remove('session');
      },
      patchSession: async (partial: Partial<Session>) => {
        const current = session;
        if (!current) {
          return;
        }
        const next = { ...current, ...partial };
        applySession(next);
        setSession(next);
        await save('session', JSON.stringify(next));
      },
    }),
    [session, ready, apiBase],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession() {
  const ctx = useContext(SessionContext);
  if (!ctx) {
    throw new Error('SessionProvider missing');
  }
  return ctx;
}
