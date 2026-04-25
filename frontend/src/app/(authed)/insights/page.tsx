"use client";

import { useEffect, useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Bar, Card, Donut, KPI } from "@/components/UI";
import { api } from "@/lib/api";
import type { MetricsResponse } from "@/lib/types";

const SPARK_COLORS = ["var(--accent)", "var(--warn)", "var(--crit)", "var(--info)", "var(--crit)", "var(--accent)"];

export default function DashboardPage() {
  const [m, setM] = useState<MetricsResponse | null>(null);
  useEffect(() => { api.metrics().then(setM); }, []);

  const sparkBy = (i: number) => {
    if (!m) return [];
    return [m.sparks.uptime, m.sparks.latency, m.sparks.incident, m.sparks.towers, m.sparks.subs, m.sparks.queries][i] ?? [];
  };

  return (
    <>
      <TopBar title="Operations Dashboard" sub="Lagos metro · 24h rolling · auto-refresh 30s" />
      <div style={{ padding: 22, display: "flex", flexDirection: "column", gap: 14 }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(6, 1fr)", gap: 10 }}>
          {(m?.kpis ?? []).map((k, i) => (
            <KPI key={k.label} {...k} spark={sparkBy(i)} color={SPARK_COLORS[i]} />
          ))}
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 14 }}>
          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 12 }}>NETWORK LATENCY · p95 · 24h</div>
            <BigChart />
          </Card>
          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 12 }}>REGIONAL HEALTH</div>
            {(m?.regions ?? []).map((r, i, all) => (
              <div key={r.name} style={{ padding: "9px 0", borderBottom: i < all.length - 1 ? "1px solid var(--line)" : 0, display: "flex", alignItems: "center", gap: 10 }}>
                <div style={{ flex: 1, fontSize: 12.5 }}>{r.name}</div>
                <div style={{ flex: 1.5 }}><Bar pct={r.avgSignal} tone={r.tone === "crit" ? "crit" : r.tone === "warn" ? "warn" : "ok"} /></div>
                <div className="mono" style={{ fontSize: 11, color: r.tone === "crit" ? "var(--crit)" : r.tone === "warn" ? "var(--warn)" : "var(--ok)", width: 32, textAlign: "right" }}>{r.avgSignal}%</div>
              </div>
            ))}
          </Card>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 14 }}>
          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 10 }}>INCIDENTS BY TYPE · 7d</div>
            {(m?.incidentTypes ?? []).map((it, i) => {
              const max = Math.max(1, ...(m?.incidentTypes ?? []).map(t => t.count));
              const c = ["var(--crit)", "var(--warn)", "var(--info)", "var(--accent)", "var(--ink-3)"][i] ?? "var(--ink-3)";
              return (
                <div key={it.type} style={{ display: "flex", alignItems: "center", gap: 10, padding: "7px 0" }}>
                  <div style={{ flex: 1, fontSize: 12 }}>{it.type}</div>
                  <div style={{ flex: 2, height: 6, background: "var(--bg-3)", borderRadius: 3, overflow: "hidden" }}>
                    <div style={{ height: "100%", width: `${(it.count / max) * 100}%`, background: c, borderRadius: 3 }} />
                  </div>
                  <div className="mono" style={{ fontSize: 11, width: 24, textAlign: "right" }}>{it.count}</div>
                </div>
              );
            })}
          </Card>
          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 10 }}>COPILOT TOP QUERIES · 24h</div>
            {[
              ['"why is lagos west slow"', 142],
              ['"show outages last 2h"', 98],
              ['"predict next failure"', 76],
              ['"compare lekki vs VI"', 54],
              ['"packet loss ikeja"', 41],
            ].map(([q, n]) => (
              <div key={q as string} style={{ display: "flex", justifyContent: "space-between", padding: "7px 0", fontSize: 12, borderBottom: "1px solid var(--line)" }}>
                <span style={{ color: "var(--ink-2)", fontFamily: "var(--mono)", fontSize: 11 }}>{q}</span>
                <span className="mono" style={{ color: "var(--accent)" }}>{n}</span>
              </div>
            ))}
          </Card>
          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 10 }}>SLA COMPLIANCE</div>
            <div style={{ display: "flex", alignItems: "center", gap: 14, marginTop: 6 }}>
              <Donut pct={99.847} color="var(--accent)" size={84} />
              <div style={{ flex: 1 }}>
                <div className="mono" style={{ fontSize: 9.5, color: "var(--ink-3)", marginBottom: 4 }}>TARGET 99.95%</div>
                <div className="mono" style={{ fontSize: 11, color: "var(--crit)", marginBottom: 8 }}>▼ 0.103 BELOW</div>
                <div style={{ fontSize: 11, color: "var(--ink-2)", lineHeight: 1.5 }}>Recovery ETA <span className="mono" style={{ color: "var(--ink)" }}>2.4h</span> if active criticals resolve</div>
              </div>
            </div>
          </Card>
        </div>
      </div>
    </>
  );
}

function BigChart() {
  const series = [
    { name: "Lagos West",    color: "var(--crit)",   data: [34,36,40,44,52,62,76,90,108,124,138,142,140,138,135,132] },
    { name: "Ikeja",         color: "var(--warn)",   data: [28,30,32,34,36,40,44,48,52,55,58,56,52,50,48,46] },
    { name: "V.I. / Ikoyi",  color: "var(--accent)", data: [22,23,24,24,25,26,28,30,32,34,36,34,32,30,28,28] },
  ];
  const max = 160, W = 800, H = 180;
  return (
    <div>
      <svg viewBox={`0 0 ${W} ${H}`} style={{ width: "100%", height: 180, display: "block" }}>
        {[40, 80, 120, 160].map(v => (
          <g key={v}>
            <line x1="40" y1={H - (v / max) * H + 20} x2={W} y2={H - (v / max) * H + 20} stroke="var(--line)" strokeWidth=".5" strokeDasharray="2 3" />
            <text x="0" y={H - (v / max) * H + 24} fill="var(--ink-3)" fontSize="9" fontFamily="var(--mono)">{v}ms</text>
          </g>
        ))}
        {series.map(s => {
          const pts = s.data.map((v, i) => `${40 + (i / (s.data.length - 1)) * (W - 40)},${H - (v / max) * H + 20}`).join(" ");
          return (
            <g key={s.name}>
              <polyline points={pts} fill="none" stroke={s.color} strokeWidth="1.5" strokeLinecap="round" />
              <circle cx={40 + (W - 40)} cy={H - (s.data[s.data.length - 1] / max) * H + 20} r="3" fill={s.color} />
            </g>
          );
        })}
      </svg>
      <div style={{ display: "flex", gap: 18, marginTop: 8 }}>
        {series.map(s => (
          <div key={s.name} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 11.5, color: "var(--ink-2)" }}>
            <span style={{ width: 10, height: 2, background: s.color }} />{s.name}
          </div>
        ))}
      </div>
    </div>
  );
}
