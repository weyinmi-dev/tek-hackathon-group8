"use client";

import { createContext, useContext, useState } from "react";
import { RootStore } from "./RootStore";

const StoreContext = createContext<RootStore | null>(null);

/**
 * Single source of truth for the React tree. Construct ONCE per page session via
 * useState's initializer (instead of `new RootStore()` at render time) so HMR /
 * StrictMode double-invokes don't multiply hydration / autoruns.
 *
 * The provider is a client component → SSR renders without stores; on the
 * client, the stores hydrate from localStorage in their constructors and the
 * tree re-renders observers automatically.
 */
export function StoreProvider({ children }: { children: React.ReactNode }) {
  const [store] = useState(() => new RootStore());
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
