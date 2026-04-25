"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { Sidebar } from "@/components/Sidebar";
import { useAuth } from "@/lib/auth";

export default function AuthedLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const { user, ready } = useAuth();

  useEffect(() => {
    if (ready && !user) router.push("/login");
  }, [ready, user, router]);

  if (!ready) {
    return (
      <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", color: "var(--ink-3)", fontFamily: "var(--mono)", fontSize: 12 }}>
        ⌁ initializing session…
      </div>
    );
  }
  if (!user) return null;

  return (
    <div style={{ display: "grid", gridTemplateColumns: "240px 1fr", minHeight: "100vh" }}>
      <Sidebar />
      <main style={{ display: "flex", flexDirection: "column", minHeight: 0, overflowY: "auto", height: "100vh" }}>
        {children}
      </main>
    </div>
  );
}
