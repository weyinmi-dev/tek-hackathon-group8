"use client";

import { useEffect, useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill } from "@/components/UI";
import { api } from "@/lib/api";
import type { AuditEntry } from "@/lib/types";

export default function AuditPage() {
  const [actor, setActor] = useState<string>("all");
  const [rows, setRows] = useState<AuditEntry[]>([]);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    api.audit(100).then(setRows).catch(e => setErr(String(e)));
  }, []);

  const actors = ["all", ...Array.from(new Set(rows.map(r => r.actor)))];
  const filtered = actor === "all" ? rows : rows.filter(r => r.actor === actor);

  function exportCsv(): void {
    const header = ["time", "actor", "role", "action", "target", "ip"];
    const escape = (s: string = ""): string =>
      /[",\n]/.test(s) ? `"${s.replaceAll('"', '""')}"` : s;
    const lines = [
      header.join(","),
      ...filtered.map(r => [r.time, r.actor, r.role, r.action, r.target, r.ip].map(escape).join(",")),
    ];
    const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `telcopilot-audit-${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }

  return (
    <>
      <TopBar
        title="Audit Log"
        sub="Immutable · all actions logged · SOC2 ready"
        right={
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <span className="mono" style={{ fontSize: 10.5, color: "var(--ink-3)" }}>FILTER</span>
            <select value={actor} onChange={e => setActor(e.target.value)} style={{
              appearance: "none", background: "var(--bg-1)", border: "1px solid var(--line)",
              color: "var(--ink)", padding: "5px 10px", borderRadius: 5, fontSize: 11.5, fontFamily: "var(--mono)",
            }}>
              {actors.map(a => <option key={a} value={a}>{a}</option>)}
            </select>
            <Btn onClick={exportCsv} disabled={filtered.length === 0}>Export CSV</Btn>
          </div>
        }
      />
      <div style={{ padding: 22 }}>
        {err && <div className="mono" style={{ color: "var(--crit)", marginBottom: 12 }}>⚠ {err} (manager+ required)</div>}
        <Card pad={0}>
          <div style={{
            padding: "12px 16px", borderBottom: "1px solid var(--line)",
            display: "grid", gridTemplateColumns: "100px 140px 90px 160px 1fr 110px", gap: 14,
            fontFamily: "var(--mono)", fontSize: 10, color: "var(--ink-3)",
            letterSpacing: ".12em", textTransform: "uppercase",
          }}>
            <span>TIME</span><span>ACTOR</span><span>ROLE</span><span>ACTION</span><span>TARGET</span><span>SOURCE IP</span>
          </div>
          {filtered.map((a, i) => (
            <div key={i} style={{
              padding: "10px 16px",
              borderBottom: i < filtered.length - 1 ? "1px solid var(--line)" : 0,
              display: "grid", gridTemplateColumns: "100px 140px 90px 160px 1fr 110px", gap: 14,
              fontSize: 11.5, fontFamily: "var(--mono)", alignItems: "center",
            }}>
              <span style={{ color: "var(--ink-3)" }}>{a.time}</span>
              <span style={{ color: a.actor === "system" ? "var(--info)" : "var(--ink)" }}>{a.actor === "system" ? "⚙ system" : a.actor}</span>
              <span><Pill tone={a.role === "admin" ? "crit" : a.role === "manager" ? "warn" : a.role === "engineer" ? "accent" : "info"}>{a.role}</Pill></span>
              <span style={{ color: a.action.startsWith("rbac") || a.action.startsWith("auth") ? "var(--warn)" : "var(--ink-2)" }}>{a.action}</span>
              <span style={{ color: "var(--ink)", fontFamily: "var(--sans)", fontSize: 12 }}>{a.target}</span>
              <span style={{ color: "var(--ink-3)" }}>{a.ip}</span>
            </div>
          ))}
        </Card>
        <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 14, letterSpacing: ".06em" }}>
          ⌁ {filtered.length} OF {rows.length} ENTRIES · ROLLING WINDOW
        </div>
      </div>
    </>
  );
}
