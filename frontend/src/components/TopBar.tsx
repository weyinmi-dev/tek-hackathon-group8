"use client";

import { ReactNode, useEffect, useState } from "react";

export function TopBar({ title, sub, right }: { title: string; sub?: string; right?: ReactNode }) {
  const [now, setNow] = useState<Date | null>(null);
  useEffect(() => {
    setNow(new Date());
    const i = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(i);
  }, []);
  const t = now ? now.toTimeString().slice(0, 8) : "--:--:--";
  return (
    <div style={{
      display: "flex", alignItems: "center", justifyContent: "space-between",
      padding: "14px 22px", borderBottom: "1px solid var(--line)",
      background: "var(--bg)", position: "sticky", top: 0, zIndex: 5,
      backdropFilter: "blur(8px)",
    }}>
      <div style={{ display: "flex", flexDirection: "column", lineHeight: 1.2 }}>
        <div style={{ fontSize: 18, fontWeight: 600, letterSpacing: "-.01em" }}>{title}</div>
        {sub && <div className="mono" style={{ fontSize: 11, color: "var(--ink-3)", marginTop: 3 }}>{sub}</div>}
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
        {right}
        <div className="mono" style={{
          display: "flex", alignItems: "center", gap: 8,
          fontSize: 11, color: "var(--ink-2)",
          padding: "6px 10px", background: "var(--bg-1)",
          border: "1px solid var(--line)", borderRadius: 6,
        }}>
          <span className="dot" />
          <span>LIVE · WAT {t}</span>
        </div>
      </div>
    </div>
  );
}
