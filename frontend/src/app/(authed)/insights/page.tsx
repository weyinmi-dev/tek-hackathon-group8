"use client";

import { useEffect } from "react";
import { observer } from "mobx-react-lite";
import { TopBar } from "@/components/TopBar";
import { Bar, Card, Donut, KPI } from "@/components/UI";
import { useInsightsStore } from "@/lib/stores/StoreProvider";
import type { EnergyMetricsResponse } from "@/lib/types";

const SPARK_COLORS = ["var(--accent)", "var(--warn)", "var(--crit)", "var(--info)", "var(--crit)", "var(--accent)"];

// Source colour palette for the energy mix donut/legend — kept aligned with the
// Energy Sites page so the same colour means the same source across the app.
const ENERGY_MIX_COLORS: Record<string, string> = {
  Diesel: "var(--warn)",
  Grid: "var(--info)",
  Battery: "var(--accent)",
  Solar: "#f5d76e",
};

const DashboardPage = observer(function DashboardPage() {
  const store = useInsightsStore();

  // Mount: kick off the 30s refresh loop. The store cache survives the
  // unmount so re-entering the page paints the previous data while the
  // new fetch resolves — no flash of empty cards.
  useEffect(() => {
    store.startAutoRefresh();
    return () => store.stopAutoRefresh();
  }, [store]);

  const m = store.metrics;
  const e = store.energy;

  const sparkBy = (i: number) => {
    if (!m) return [];
    return [m.sparks.uptime, m.sparks.latency, m.sparks.incident, m.sparks.towers, m.sparks.subs, m.sparks.queries][i] ?? [];
  };

  return (
    <>
      <TopBar title="Operations Dashboard" sub="Lagos metro · ops + energy · 24h rolling · auto-refresh 30s" />
      <div style={{ padding: 22, display: "flex", flexDirection: "column", gap: 14 }}>
        <SectionHeader label="OPS" />
        <div style={{ display: "grid", gridTemplateColumns: "repeat(6, 1fr)", gap: 10 }}>
          {(m?.kpis ?? []).map((k, i) => (
            <KPI key={k.label} {...k} spark={sparkBy(i)} color={SPARK_COLORS[i]} />
          ))}
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 14 }}>
          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 12 }}>NETWORK LATENCY · p95 · 24h</div>
            <BigChart series={m?.regionLatency ?? []} />
          </Card>
          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 12 }}>REGIONAL HEALTH · OPS</div>
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
            {(m?.topQueries ?? []).map((q) => (
              <div key={q.query} style={{ display: "flex", justifyContent: "space-between", padding: "7px 0", fontSize: 12, borderBottom: "1px solid var(--line)", gap: 10 }}>
                <span style={{ color: "var(--ink-2)", fontFamily: "var(--mono)", fontSize: 11, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                  &ldquo;{q.query}&rdquo;
                </span>
                <span className="mono" style={{ color: "var(--accent)", flexShrink: 0 }}>{q.count}</span>
              </div>
            ))}
            {(m?.topQueries ?? []).length === 0 && (
              <div className="mono" style={{ color: "var(--ink-3)", fontSize: 11, padding: "7px 0" }}>
                ⌁ no copilot queries in the last 24h
              </div>
            )}
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

        <SectionHeader label="ENERGY" />
        <EnergyKpiStrip e={e} />

        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 14 }}>
          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 12 }}>OPEX TREND · ₦M / DAY · 16d</div>
            <OpexChart points={e?.opexTrend ?? []} />
          </Card>
          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 12 }}>REGIONAL HEALTH · ENERGY</div>
            {(e?.regions ?? []).map((r, i, all) => (
              <div key={r.name} style={{ padding: "9px 0", borderBottom: i < all.length - 1 ? "1px solid var(--line)" : 0 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                  <div style={{ flex: 1, fontSize: 12.5 }}>{r.name}</div>
                  <div style={{ flex: 1.5 }}><Bar pct={r.avgUptimePct} tone={r.tone} /></div>
                  <div className="mono" style={{ fontSize: 11, color: r.tone === "crit" ? "var(--crit)" : r.tone === "warn" ? "var(--warn)" : "var(--ok)", width: 36, textAlign: "right" }}>{r.avgUptimePct}%</div>
                </div>
                <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 4, display: "flex", gap: 10 }}>
                  <span>{r.sites} sites</span>
                  {r.critical > 0 && <span style={{ color: "var(--crit)" }}>● {r.critical} crit</span>}
                  {r.degraded > 0 && <span style={{ color: "var(--warn)" }}>● {r.degraded} deg</span>}
                  <span style={{ color: "var(--ok)" }}>● {r.ok} ok</span>
                  <span style={{ marginLeft: "auto" }}>batt {r.avgBattPct}%</span>
                </div>
              </div>
            ))}
            {(e?.regions ?? []).length === 0 && (
              <div className="mono" style={{ color: "var(--ink-3)", fontSize: 11, padding: "7px 0" }}>
                ⌁ awaiting fleet telemetry
              </div>
            )}
          </Card>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 14 }}>
          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 10 }}>ANOMALIES BY KIND · 7d</div>
            {(e?.anomalyTypes ?? []).map((it, i) => {
              const max = Math.max(1, ...(e?.anomalyTypes ?? []).map(t => t.count));
              const c = ["var(--crit)", "var(--warn)", "var(--info)", "var(--accent)", "var(--ink-3)"][i] ?? "var(--ink-3)";
              return (
                <div key={it.kind} style={{ display: "flex", alignItems: "center", gap: 10, padding: "7px 0" }}>
                  <div style={{ flex: 1, fontSize: 12 }}>{it.kind}</div>
                  <div style={{ flex: 2, height: 6, background: "var(--bg-3)", borderRadius: 3, overflow: "hidden" }}>
                    <div style={{ height: "100%", width: `${(it.count / max) * 100}%`, background: c, borderRadius: 3 }} />
                  </div>
                  <div className="mono" style={{ fontSize: 11, width: 24, textAlign: "right" }}>{it.count}</div>
                </div>
              );
            })}
            {(e?.anomalyTypes ?? []).length === 0 && (
              <div className="mono" style={{ color: "var(--ink-3)", fontSize: 11, padding: "7px 0" }}>
                ⌁ no anomalies recorded
              </div>
            )}
          </Card>

          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 10 }}>FLEET POWER MIX · NOW</div>
            <EnergyMixDonut mix={e?.energyMix ?? []} />
          </Card>

          <Card pad={16}>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 10 }}>TOP DIESEL BURNERS · 24h</div>
            {(e?.topBurners ?? []).map((b, i, all) => (
              <div key={b.siteCode} style={{ padding: "8px 0", borderBottom: i < all.length - 1 ? "1px solid var(--line)" : 0 }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 8 }}>
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <div className="mono" style={{ fontSize: 10, color: "var(--accent)" }}>{b.siteCode}</div>
                    <div style={{ fontSize: 12, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{b.name}</div>
                  </div>
                  <div className="mono" style={{ fontSize: 11.5, color: "var(--warn)", textAlign: "right" }}>
                    {b.dailyDieselLitres}L
                  </div>
                </div>
                <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 2, display: "flex", justifyContent: "space-between" }}>
                  <span>{b.region}</span>
                  <span>₦{(b.dailyCostNgn / 1000).toFixed(0)}K/d</span>
                </div>
              </div>
            ))}
            {(e?.topBurners ?? []).length === 0 && (
              <div className="mono" style={{ color: "var(--ink-3)", fontSize: 11, padding: "7px 0" }}>
                ⌁ no diesel consumption recorded
              </div>
            )}
          </Card>
        </div>
      </div>
    </>
  );
});

export default DashboardPage;

function SectionHeader({ label }: { label: string }) {
  return (
    <div className="mono uppr" style={{
      fontSize: 10, color: "var(--accent)", letterSpacing: ".18em",
      paddingTop: 4, display: "flex", alignItems: "center", gap: 10,
    }}>
      <span style={{ width: 6, height: 6, borderRadius: "50%", background: "var(--accent)", boxShadow: "0 0 8px var(--accent)" }} />
      {label}
      <span style={{ flex: 1, height: 1, background: "var(--line)" }} />
    </div>
  );
}

function EnergyKpiStrip({ e }: { e: EnergyMetricsResponse | null }) {
  if (!e) {
    return (
      <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 10 }}>
        {[0, 1, 2, 3].map(i => (
          <Card key={i} pad={14} style={{ minHeight: 64 }}>
            <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>⌁ awaiting fleet telemetry</div>
          </Card>
        ))}
      </div>
    );
  }
  const fleetTone = e.criticalSites > 0 ? "var(--crit)" : e.fleetUptimePct > 99 ? "var(--ok)" : "var(--warn)";
  const battTone = e.avgBatteryPct < 30 ? "var(--crit)" : e.avgBatteryPct < 60 ? "var(--warn)" : "var(--ok)";
  const anomTone = e.openAnomalies > 0 ? "var(--warn)" : "var(--ok)";
  return (
    <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 10 }}>
      <SmallStat label="Fleet uptime" value={e.fleetUptimePct.toFixed(2)} unit="%" sub={`${e.criticalSites} site${e.criticalSites === 1 ? "" : "s"} in critical`} color={fleetTone} />
      <SmallStat label="Avg battery" value={e.avgBatteryPct.toFixed(1)} unit="%" sub="across active fleet" color={battTone} />
      <SmallStat label="OPEX · today" value={`₦${(e.dailyOpexNgn / 1_000_000).toFixed(1)}`} unit="M" sub="all sites · NGN" color="var(--accent)" />
      <SmallStat label="Open anomalies" value={e.openAnomalies.toString()} unit="" sub="awaiting acknowledgement" color={anomTone} />
    </div>
  );
}

function SmallStat({ label, value, unit, sub, color }: { label: string; value: string; unit: string; sub: string; color: string }) {
  return (
    <Card pad={14}>
      <div className="mono uppr" style={{ fontSize: 9, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 6 }}>{label}</div>
      <div className="mono" style={{ fontSize: 22, fontWeight: 600, color, lineHeight: 1 }}>
        {value}<span style={{ fontSize: 12, color: "var(--ink-3)", marginLeft: 3 }}>{unit}</span>
      </div>
      <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 6 }}>{sub}</div>
    </Card>
  );
}

function OpexChart({ points }: { points: number[] }) {
  const W = 800, H = 180;
  if (points.length === 0) {
    return (
      <div className="mono" style={{ color: "var(--ink-3)", fontSize: 11, padding: 20, textAlign: "center" }}>
        ⌁ awaiting OPEX telemetry
      </div>
    );
  }
  const max = Math.max(...points) * 1.08;
  const min = Math.min(...points) * 0.92;
  const range = Math.max(0.1, max - min);
  const xy = (v: number, i: number) => {
    const x = 40 + (i / Math.max(1, points.length - 1)) * (W - 40);
    const y = H - ((v - min) / range) * (H - 30) + 10;
    return [x, y] as const;
  };
  const linePts = points.map((v, i) => xy(v, i).join(",")).join(" ");
  const [endX, endY] = xy(points.at(-1) ?? 0, points.length - 1);
  const areaPts = `40,${H} ${linePts} ${endX},${H}`;

  // Y-axis ticks (4 evenly spaced from min..max).
  const ticks = [0, 1, 2, 3].map(i => +(min + (range * i) / 3).toFixed(1));
  return (
    <div>
      <svg viewBox={`0 0 ${W} ${H}`} style={{ width: "100%", height: 180, display: "block" }}>
        {ticks.map(v => {
          const [, y] = xy(v, 0);
          return (
            <g key={v}>
              <line x1="40" y1={y} x2={W} y2={y} stroke="var(--line)" strokeWidth=".5" strokeDasharray="2 3" />
              <text x="0" y={y + 3} fill="var(--ink-3)" fontSize="9" fontFamily="var(--mono)">₦{v}M</text>
            </g>
          );
        })}
        <polyline points={areaPts} fill="var(--accent-dim)" opacity="0.4" />
        <polyline points={linePts} fill="none" stroke="var(--accent)" strokeWidth="1.5" strokeLinecap="round" />
        <circle cx={endX} cy={endY} r="3" fill="var(--accent)" />
      </svg>
      <div className="mono" style={{ fontSize: 9.5, color: "var(--ink-3)", display: "flex", justifyContent: "space-between", marginTop: 4 }}>
        <span>−16d</span><span>−8d</span><span>NOW · ₦{(points.at(-1) ?? 0).toFixed(1)}M</span>
      </div>
    </div>
  );
}

function EnergyMixDonut({ mix }: { mix: { source: string; pct: number }[] }) {
  if (mix.length === 0 || mix.every(m => m.pct === 0)) {
    return (
      <div className="mono" style={{ color: "var(--ink-3)", fontSize: 11, padding: 12 }}>
        ⌁ awaiting fleet telemetry
      </div>
    );
  }
  const size = 120, r = (size - 18) / 2;
  const c = 2 * Math.PI * r;
  let acc = 0;
  // Total may not sum to 100 (integer rounding) — normalise so the ring closes.
  const total = mix.reduce((s, m) => s + m.pct, 0) || 100;
  const segments = mix.map(m => {
    const frac = m.pct / total;
    const length = c * frac;
    const seg = { src: m.source, pct: m.pct, dasharray: `${length} ${c - length}`, offset: -acc };
    acc += length;
    return seg;
  });

  return (
    <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} style={{ flexShrink: 0 }}>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="var(--bg-3)" strokeWidth="9" />
        {segments.map(s => (
          <circle
            key={s.src}
            cx={size / 2} cy={size / 2} r={r}
            fill="none"
            stroke={ENERGY_MIX_COLORS[s.src] ?? "var(--ink-3)"}
            strokeWidth="9"
            strokeDasharray={s.dasharray}
            strokeDashoffset={s.offset}
            transform={`rotate(-90 ${size / 2} ${size / 2})`}
          />
        ))}
      </svg>
      <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: 6 }}>
        {mix.map(m => (
          <div key={m.source} style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 11.5 }}>
            <span style={{ width: 8, height: 8, borderRadius: 2, background: ENERGY_MIX_COLORS[m.source] ?? "var(--ink-3)" }} />
            <span style={{ color: "var(--ink-2)", flex: 1 }}>{m.source}</span>
            <span className="mono" style={{ color: "var(--ink)", fontSize: 11 }}>{m.pct}%</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function BigChart({ series }: { series: { name: string; color: string; series: number[] }[] }) {
  const max = 160, W = 800, H = 180;
  if (series.length === 0) {
    return (
      <div className="mono" style={{ color: "var(--ink-3)", fontSize: 11, padding: 20, textAlign: "center" }}>
        ⌁ awaiting region telemetry
      </div>
    );
  }
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
          const data = s.series;
          if (data.length === 0) return null;
          const pts = data.map((v, i) => `${40 + (i / Math.max(1, data.length - 1)) * (W - 40)},${H - (v / max) * H + 20}`).join(" ");
          const last = data.at(-1) ?? 0;
          return (
            <g key={s.name}>
              <polyline points={pts} fill="none" stroke={s.color} strokeWidth="1.5" strokeLinecap="round" />
              <circle cx={40 + (W - 40)} cy={H - (last / max) * H + 20} r="3" fill={s.color} />
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
