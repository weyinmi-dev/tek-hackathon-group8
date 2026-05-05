"use client";

import { Suspense, useEffect } from "react";
import { useRouter } from "next/navigation";
import { observer } from "mobx-react-lite";
import { Sidebar } from "@/components/Sidebar";
import { useAuthStore } from "@/lib/stores/StoreProvider";

/**
 * Auth gate for every /(authed)/* route. Observes the AuthStore so that:
 *   - on first load, we wait for hydration before deciding to redirect
 *   - on logout (this tab OR another), we bounce to /login automatically
 *   - cross-tab login is reflected immediately
 *
 * The branded <Loader/> below is the single loader for everything authed —
 * reused by the auth-hydrating early return AND the route-segment Suspense
 * boundary that wraps {children} (which is why there is no loading.tsx file).
 */
export default observer(function AuthedLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const auth = useAuthStore();

  useEffect(() => {
    if (auth.hasHydrated && !auth.isAuthenticated) router.push("/login");
  }, [auth.hasHydrated, auth.isAuthenticated, router]);

  if (!auth.hasHydrated) return <Loader caption="initializing session" />;
  if (!auth.isAuthenticated) return null;

  return (
    <div style={{ display: "grid", gridTemplateColumns: "240px 1fr", minHeight: "100vh" }}>
      <Sidebar />
      <main style={{ display: "flex", flexDirection: "column", minHeight: 0, overflowY: "auto", height: "100vh" }}>
        <Suspense fallback={<Loader />}>
          {children}
        </Suspense>
      </main>
    </div>
  );
});

// Brand vocabulary lifted from Sidebar (◉ accent mark + glow) and the original
// "⌁ initializing session…" voice. The pulse-ring keyframe is shared with .dot
// in globals.css. min-height + flex: 1 means the same component fills the
// viewport whether it's the standalone auth-gate fallback or rendered inside
// the flex <main> as the route Suspense fallback.
function Loader({ caption = "loading" }: { caption?: string }) {
  return (
    <div style={{
      flex: 1,
      minHeight: "100vh",
      display: "grid",
      placeItems: "center",
      padding: 24,
    }}>
      <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 18 }}>
        <div style={{ position: "relative", width: 80, height: 80, display: "grid", placeItems: "center" }}>
          <span aria-hidden style={{
            position: "absolute", inset: 0, borderRadius: "50%",
            border: "1px solid var(--accent-line)",
            animation: "pulse-ring 1.6s infinite",
          }} />
          <span aria-hidden style={{
            position: "absolute", inset: 0, borderRadius: "50%",
            border: "1px solid var(--accent-line)",
            animation: "pulse-ring 1.6s infinite",
            animationDelay: ".8s",
          }} />
          <span style={{
            width: 36, height: 36, borderRadius: 7,
            background: "var(--accent)", color: "#001a10",
            display: "grid", placeItems: "center",
            fontFamily: "var(--mono)", fontSize: 18, fontWeight: 700,
            letterSpacing: "-.02em",
            boxShadow: "0 0 28px var(--accent-dim)",
          }}>◉</span>
        </div>
        <div className="mono uppr" style={{
          fontSize: 10, color: "var(--ink-3)", letterSpacing: ".18em",
          display: "flex", alignItems: "center", gap: 8,
        }}>
          <span style={{ color: "var(--accent)" }}>⌁</span>
          {caption}
        </div>
      </div>
    </div>
  );
}
