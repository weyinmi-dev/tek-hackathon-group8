"use client";

import { createContext, useContext, useEffect, useState } from "react";
import { RootStore } from "./RootStore";

const StoreContext = createContext<RootStore | null>(null);

/**
 * Single source of truth for the React tree. Construct ONCE per page session via
 * useState's initializer (instead of `new RootStore()` at render time) so HMR /
 * StrictMode double-invokes don't multiply hydration / autoruns.
 *
 * SSR safety: the RootStore constructor only wires DI — it does NOT touch localStorage.
 * Each store exposes a boot() method we invoke in a useEffect, guaranteeing that the
 * server-rendered markup and the client's first paint are identical (every store's
 * `hasHydrated` is false on both). After mount, boot() reads localStorage and the
 * tree updates. Without this split we'd mismatch SSR vs CSR — React would regenerate
 * the entire tree, the store would remount, and any in-flight request would lose its
 * Bearer header (manifests as a 401 on the first chat call after a refresh).
 */
export function StoreProvider({ children }: { children: React.ReactNode }) {
  const [store] = useState(() => new RootStore());

  useEffect(() => {
    // wireApi must run against the kept instance — see RootStore.wireApi for why.
    // Order matters: wire the token provider first so any request kicked off by
    // boot() (e.g. ChatStore's listConversations autorun) sees the bearer header.
    store.wireApi();
    store.auth.boot();
    store.ui.boot();
    store.chat.boot();
  }, [store]);

  return <StoreContext.Provider value={store}>{children}</StoreContext.Provider>;
}

export function useStores(): RootStore {
  const ctx = useContext(StoreContext);
  if (!ctx) throw new Error("useStores must be used inside <StoreProvider>");
  return ctx;
}

// Convenience hooks — keep call sites narrow ("I only need auth").
export const useAuthStore = () => useStores().auth;
export const useChatStore = () => useStores().chat;
export const useUiStore   = () => useStores().ui;
