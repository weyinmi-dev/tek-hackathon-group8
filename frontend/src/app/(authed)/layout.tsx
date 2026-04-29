"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { observer } from "mobx-react-lite";
import { Sidebar } from "@/components/Sidebar";
import { useAuthStore } from "@/lib/stores/StoreProvider";

/**
 * Auth gate for every /(authed)/* route. Observes the AuthStore so that:
 *   - on first load, we wait for hydration before deciding to redirect
 *   - on logout (this tab OR another), we bounce to /login automatically
 *   - cross-tab login is reflected immediately
 */
export default observer(function AuthedLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const auth = useAuthStore();

  useEffect(() => {
    if (auth.hasHydrated && !auth.isAuthenticated) router.push("/login");
  }, [auth.hasHydrated, auth.isAuthenticated, router]);

  if (!auth.hasHydrated) {
    return (
      <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", color: "var(--ink-3)", fontFamily: "var(--mono)", fontSize: 12 }}>
        ⌁ initializing session…
      </div>
    );
  }
  if (!auth.isAuthenticated) return null;

  return (
    <div style={{ display: "grid", gridTemplateColumns: "240px 1fr", minHeight: "100vh" }}>
      <Sidebar />
      <main style={{ display: "flex", flexDirection: "column", minHeight: 0, overflowY: "auto", height: "100vh" }}>
        {children}
      </main>
    </div>
  );
});
