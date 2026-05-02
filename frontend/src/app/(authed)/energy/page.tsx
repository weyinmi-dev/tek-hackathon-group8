"use client";

import { useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, KPI, Pill, Section } from "@/components/UI";
import { SiteDieselChart } from "@/components/EnergyCharts";
import {
  ENERGY_KPIS,
  SITES,
  type EnergySite,
  type SiteHealth,
  type SiteSource,
} from "@/lib/energy-data";

const SRC_COLOR: Record<SiteSource, string> = {
  grid: "var(--info)",
  generator: "var(--warn)",
  battery: "var(--accent)",
  solar: "#f5d76e",
};
const SRC_LABEL: Record<SiteSource, string> = {
  grid: "GRID",
  generator: "GEN",
  battery: "BATT",
  solar: "SOLAR",
};
const HEALTH_TONE: Record<SiteHealth, "ok" | "warn" | "crit"> = {
  ok: "ok",
  degraded: "warn",
  critical: "crit",
};

type Filter = "all" | SiteHealth;

export default function EnergyPage() {
  const [sel, setSel] = useState<EnergySite>(
    SITES.find((s) => s.health === "critical") ?? SITES[0],
  );
  const [filter, setFilter] = useState<Filter>("all");
  const list = filter === "all" ? SITES : SITES.filter((s) => s.health === filter);
  const counts = {
    ok: SITES.filter((s) => s.health === "ok").length,
    degraded: SITES.filter((s) => s.health === "degraded").length,
    critical: SITES.filter((s) => s.health === "critical").length,
  };

  return (
    <>
      <TopBar
        title="Energy Sites"
        sub={`${SITES.length} active sites · grid + diesel + battery + solar orchestration`}
        right={
          <div style={{ display: "flex", gap: 6, padding: 3, background: "var(--bg-1)", border: "1px solid var(--line)", borderRadius: 7 }}>
            {([
              ["all", "All", SITES.length],
              ["critical", "Critical", counts.critical],
              ["degraded", "Degraded", counts.degraded],
              ["ok", "Healthy", counts.ok],
            ] as const).map(([k, l, n]) => (
              <button
                key={k}
                onClick={() => setFilter(k as Filter)}
                style={{
                  appearance: "none", border: 0,
                  padding: "5px 12px", borderRadius: 5,
                  fontSize: 11, fontWeight: 500,
                  background: filter === k ? "var(--bg-3)" : "transparent",
                  color: filter === k ? "var(--ink)" : "var(--ink-3)",
                  cursor: "pointer",
                  display: "flex", alignItems: "center", gap: 6,
                }}
              >
                {l}
                <span className="mono" style={{ fontSize: 9.5, color: "var(--ink-3)" }}>{n}</span>
              </button>
            ))}
          </div>
        }
      />
      <div style={{ padding: 22, display: "flex", flexDirection: "column", gap: 14 }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(6,1fr)", gap: 10 }}>
          {ENERGY_KPIS.map((k, i) => (
            <KPI
              key={k.label}
              {...k}
              spark={KPI_SPARKS[i]}
              color={KPI_COLORS[i]}
            />
          ))}
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 380px", gap: 14, minHeight: 0 }}>
          <Card pad={0}>
            <div style={{
              padding: "12px 14px", borderBottom: "1px solid var(--line)",
              display: "grid", gridTemplateColumns: "1.6fr 90px 60px 1fr 1fr 90px 80px",
              gap: 10, fontFamily: "var(--mono)", fontSize: 10, color: "var(--ink-3)",
              letterSpacing: ".12em", textTransform: "uppercase",
            }}>
              <span>Site</span><span>Source</span><span>Grid</span><span>Battery</span><span>Diesel</span><span>Cost/d</span><span>Health</span>
            </div>
            <div style={{ maxHeight: "calc(100vh - 380px)", overflowY: "auto" }}>
              {list.map((s, i) => {
                const active = sel.id === s.id;
                return (
                  <button
                    key={s.id}
                    onClick={() => setSel(s)}
                    style={{
                      appearance: "none", width: "100%", textAlign: "left", cursor: "pointer",
                      padding: "12px 14px", borderBottom: i < list.length - 1 ? "1px solid var(--line)" : 0,
                      display: "grid", gridTemplateColumns: "1.6fr 90px 60px 1fr 1fr 90px 80px",
                      gap: 10,
                      background: active ? "var(--bg-2)" : "transparent",
                      border: "none",
                      borderLeft: "3px solid " + (active ? "var(--accent)" : "transparent"),
                      color: "var(--ink)", alignItems: "center", fontSize: 12.5,
                    }}
                  >
                    <div>
                      <div className="mono" style={{ fontSize: 10, color: "var(--accent)", marginBottom: 2 }}>{s.id}</div>
                      <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                        <span style={{ fontWeight: 500 }}>{s.name}</span>
                        <span style={{ color: "var(--ink-3)", fontSize: 11 }}>· {s.region}</span>
                        {s.solar && <span style={{ fontSize: 11, color: "#f5d76e" }}>☼</span>}
                      </div>
                    </div>
                    <SourceTag src={s.source} />
                    <span className="mono" style={{ fontSize: 10.5, color: s.gridUp ? "var(--ok)" : "var(--ink-3)" }}>
                      {s.gridUp ? "● UP" : "○ DOWN"}
                    </span>
                    <BarRow pct={s.battPct} tone={s.battPct < 30 ? "crit" : s.battPct < 60 ? "warn" : "ok"} />
                    <BarRow pct={s.dieselPct} tone={s.dieselPct < 20 ? "crit" : s.dieselPct < 50 ? "warn" : "ok"} />
                    <span className="mono" style={{ fontSize: 11, color: "var(--ink-2)" }}>
                      ₦{(s.costNGN / 1000).toFixed(0)}K
                    </span>
                    <span style={{ display: "flex", alignItems: "center", gap: 6 }}>
                      <HealthDot h={s.health} />
                      <span className="mono" style={{
                        fontSize: 10, textTransform: "uppercase",
                        color: s.health === "critical" ? "var(--crit)" : s.health === "degraded" ? "var(--warn)" : "var(--ok)",
                      }}>{s.health}</span>
                    </span>
                  </button>
                );
              })}
            </div>
          </Card>

          <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
            <Card pad={16}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 10 }}>
                <div>
                  <div className="mono" style={{ fontSize: 10, color: "var(--accent)", marginBottom: 3 }}>{sel.id}</div>
                  <div style={{ fontSize: 15, fontWeight: 600 }}>{sel.name}</div>
                  <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 2, letterSpacing: ".10em" }}>
                    {sel.region.toUpperCase()}
                  </div>
                </div>
                <Pill tone={HEALTH_TONE[sel.health]} dot>{sel.health}</Pill>
              </div>
              {sel.anomaly && (
                <div style={{
                  padding: 10, background: "var(--bg-3)", borderRadius: 6,
                  fontSize: 11.5, marginBottom: 10,
                  borderLeft: `2px solid ${sel.health === "critical" ? "var(--crit)" : "var(--warn)"}`,
                }}>
                  ⚠ {sel.anomaly}
                </div>
              )}
              <div className="mono uppr" style={{ fontSize: 9.5, color: "var(--ink-3)", letterSpacing: ".14em", marginTop: 4, marginBottom: 8 }}>
                POWER MIX · NOW
              </div>
              <PowerMixBar src={sel.source} />
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10, marginTop: 14 }}>
                <Metric label="BATTERY" v={sel.battPct} unit="%" tone={sel.battPct < 30 ? "crit" : sel.battPct < 60 ? "warn" : "ok"} />
                <Metric label="DIESEL"  v={sel.dieselPct} unit="%" tone={sel.dieselPct < 20 ? "crit" : sel.dieselPct < 50 ? "warn" : "ok"} />
                <Metric label="SOLAR"   v={sel.solarKw} unit="kW" tone={sel.solarKw > 3 ? "ok" : sel.solarKw > 0 ? "warn" : "crit"} />
                <Metric label="UPTIME"  v={sel.uptime} unit="%" tone={sel.uptime > 99 ? "ok" : sel.uptime > 97 ? "warn" : "crit"} />
              </div>
              <div style={{ display: "flex", gap: 6, marginTop: 14 }}>
                <Btn primary small>Switch Source</Btn>
                <Btn small>Dispatch Refuel</Btn>
                <Btn ghost small>Open in Copilot →</Btn>
              </div>
            </Card>

            <Section label="24H DIESEL · LITERS">
              <Card pad={14}>
                <SiteDieselChart pct={sel.dieselPct} health={sel.health} />
              </Card>
            </Section>
          </div>
        </div>
      </div>
    </>
  );
}

const KPI_SPARKS: number[][] = [
  [820,790,760,740,720,700,680,520,420,360,310,280,260,250,240],
  [21,20,19,18,18,17,17,16,16,16,15,15,15,15,14],
  [58,60,62,63,64,65,65,66,66,67,67,68,68,68,68],
  [99.92,99.91,99.92,99.93,99.92,99.90,99.91,99.92,99.90,99.88,99.87,99.85,99.85,99.84,99.85],
  [5,5,4,4,4,3,3,3,3,3,3,3,3,3,3],
  [88.2,88.1,88,87.9,87.8,87.7,87.7,87.6,87.6,87.5,87.5,87.4,87.4,87.4,87.4],
];
const KPI_COLORS = ["var(--accent)", "var(--accent)", "#f5d76e", "var(--info)", "var(--ok)", "var(--warn)"];

function HealthDot({ h }: { h: SiteHealth }) {
  const c = h === "critical" ? "var(--crit)" : h === "degraded" ? "var(--warn)" : "var(--ok)";
  return <span style={{ display: "inline-block", width: 8, height: 8, borderRadius: "50%", background: c, boxShadow: `0 0 8px ${c}` }} />;
}

function SourceTag({ src }: { src: SiteSource }) {
  const c = SRC_COLOR[src];
  return (
    <span className="mono uppr" style={{
      fontSize: 9, letterSpacing: ".10em", padding: "2px 6px", borderRadius: 3,
      color: c,
      background: `color-mix(in oklch, ${c} 14%, transparent)`,
      border: `1px solid color-mix(in oklch, ${c} 30%, transparent)`,
      fontWeight: 600,
    }}>{SRC_LABEL[src]}</span>
  );
}

function BarRow({ pct, tone }: { pct: number; tone: "ok" | "warn" | "crit" }) {
  const c = tone === "crit" ? "var(--crit)" : tone === "warn" ? "var(--warn)" : "var(--ok)";
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
      <div style={{ flex: 1, height: 4, background: "var(--bg-3)", borderRadius: 2, overflow: "hidden" }}>
        <div style={{ height: "100%", width: `${pct}%`, background: c, borderRadius: 2 }} />
      </div>
      <span className="mono" style={{ fontSize: 10.5, color: c, width: 32, textAlign: "right" }}>{pct}%</span>
    </div>
  );
}

function Metric({ label, v, unit, tone }: { label: string; v: number; unit: string; tone: "ok" | "warn" | "crit" }) {
  return (
    <div>
      <div className="mono uppr" style={{ fontSize: 9, color: "var(--ink-3)", letterSpacing: ".12em", marginBottom: 4 }}>{label}</div>
      <div className="mono" style={{
        fontSize: 18, fontWeight: 600, marginBottom: 5,
        color: tone === "crit" ? "var(--crit)" : tone === "warn" ? "var(--warn)" : "var(--ok)",
      }}>
        {v}<span style={{ color: "var(--ink-3)", fontSize: 11, marginLeft: 2 }}>{unit}</span>
      </div>
    </div>
  );
}

function PowerMixBar({ src }: { src: SiteSource }) {
  const segs: { k: SiteSource; label: string }[] = [
    { k: "grid", label: "GRID" },
    { k: "generator", label: "DIESEL" },
    { k: "battery", label: "BATTERY" },
    { k: "solar", label: "SOLAR" },
  ];
  return (
    <div style={{ display: "flex", gap: 6 }}>
      {segs.map((s) => {
        const active = src === s.k;
        const c = SRC_COLOR[s.k];
        return (
          <div key={s.k} style={{
            flex: 1, padding: 8, borderRadius: 5,
            background: active ? `color-mix(in oklch, ${c} 18%, transparent)` : "var(--bg-3)",
            border: `1px solid ${active ? c : "var(--line)"}`,
            textAlign: "center",
          }}>
            <div className="mono uppr" style={{ fontSize: 9, color: active ? c : "var(--ink-3)", letterSpacing: ".12em", fontWeight: 600 }}>
              {s.label}
            </div>
            <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 3 }}>
              {active ? "ACTIVE" : "idle"}
            </div>
          </div>
        );
      })}
    </div>
  );
}

