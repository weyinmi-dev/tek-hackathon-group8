"use client";

// Compatibility shim — delegates to the MobX AuthStore so older callers (Sidebar,
// AuthedLayout, login page) continue to work while the rest of the app moves to
// useAuthStore() directly. New code should import from @/lib/stores/StoreProvider.

import { useAuthStore } from "@/lib/stores/StoreProvider";
import type { AuthUser } from "./types";

export function useAuth() {
  const auth = useAuthStore();
  return {
    user: auth.user as AuthUser | null,
    ready: auth.hasHydrated,
    login: auth.login,
    logout: auth.logout,
  };
}

/** Legacy helpers kept so any direct callers don't break. New code should use the store. */
export function readUser(): AuthUser | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = window.localStorage.getItem("tp_auth_v1");
    if (!raw) return null;
    return (JSON.parse(raw) as { user: AuthUser | null }).user ?? null;
  } catch { return null; }
}
