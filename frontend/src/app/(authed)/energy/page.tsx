"use client";

import { useEffect, useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, KPI, Pill, Section } from "@/components/UI";
import { SiteDieselChart } from "@/components/EnergyCharts";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { isEngineer } from "@/lib/rbac";
import type { EnergyKpiDto, EnergySiteDto } from "@/lib/types";

const SRC_COLOR: Record<EnergySiteDto["source"], string> = {
  grid: "var(--info)",
  generator: "var(--warn)",
  battery: "var(--accent)",
  solar: "#f5d76e",
};
const SRC_LABEL: Record<EnergySiteDto["source"], string> = {
  grid: "GRID", generator: "GEN", battery: "BATT", solar: "SOLAR",
};
const HEALTH_TONE: Record<EnergySiteDto["health"], "ok" | "warn" | "crit"> = {
  ok: "ok", degraded: "warn", critical: "crit",
};

type Filter = "all" | EnergySiteDto["health"];

const KPI_COLORS = ["var(--accent)", "var(--accent)", "#f5d76e", "var(--info)", "var(--ok)", "var(--warn)"];

export default function EnergyPage() {
  const [sites, setSites] = useState<EnergySiteDto[]>([]);
  const [kpis, setKpis] = useState<EnergyKpiDto[]>([]);
  const [sel, setSel] = useState<EnergySiteDto | null>(null);
  const [trace, setTrace] = useState<{ at: string; dieselPct: number }[]>([]);
  const [filter, setFilter] = useState<Filter>("all");
  const [busy, setBusy] = useState<string | null>(null);
  const [toast, setToast] = useState<{ id: string; msg: string } | null>(null);
  const { user } = useAuth();
  const canMutate = isEngineer(user?.role);

  // Initial load + 30s refresh so the dashboards reflect ticker mutations.
  async function refresh() {
    const [s, k] = await Promise.all([api.energy.sites(), api.energy.kpis()]);
    setSites(s.sites);
    setKpis(k.kpis);
    setSel((prev) => prev
      ? (s.sites.find((x) => x.id === prev.id) ?? prev)
      : (s.sites.find((x) => x.health === "critical") ?? s.sites[0] ?? null));
  }
  useEffect(() => {
    void refresh();
    const id = setInterval(() => void refresh(), 30_000);
    return () => clearInterval(id);
  }, []);

  // Load the selected site's diesel trace whenever the selection changes.
  useEffect(() => {
    if (!sel) { setTrace([]); return; }
    let alive = true;
    api.energy.siteDieselTrace(sel.id, 24).then((r) => {
      if (alive) setTrace(r.points);
    }).catch(() => { if (alive) setTrace([]); });
    return () => { alive = false; };
  }, [sel?.id]);

  const list = filter === "all" ? sites : sites.filter((s) => s.health === filter);
  const counts = {
    ok: sites.filter((s) => s.health === "ok").length,
    degraded: sites.filter((s) => s.health === "degraded").length,
    critical: sites.filter((s) => s.health === "critical").length,
  };

  function flash(id: string, msg: string) {
    setToast({ id, msg });
    setTimeout(() => setToast((cur) => cur?.id === id ? null : cur), 2400);
  }

  async function switchSource(target: EnergySiteDto["source"]) {
    if (!sel) return;
    setBusy("switch");
    try {
      await api.energy.switchSource(sel.id, target);
      await refresh();
      flash(sel.id, `Source switched to ${target.toUpperCase()}`);
    } catch (e) {
      flash(sel.id, e instanceof Error ? e.message : "Switch failed");
    } finally { setBusy(null); }
  }

  async function dispatchRefuel() {
    if (!sel) return;
    setBusy("refuel");
    try {
      const r = await api.energy.refuel(sel.id, 60);
      await refresh();
      flash(sel.id, `Refuelled +${r.pctChange}% (now ${r.dieselPctAfter}%)`);
    } catch (e) {
      flash(sel.id, e instanceof Error ? e.message : "Refuel failed");
    } finally { setBusy(null); }
  }

  return (
    <>
      <TopBar
        title="Energy Sites"
        sub={`${sites.length} active sites · grid + diesel + battery + solar orchestration`}
        right={
          <div style={{ display: "flex", gap: 6, padding: 3, background: "var(--bg-1)", border: "1px solid var(--line)", borderRadius: 7 }}>
            {([
              ["all", "All", sites.length],
              ["critical", "Critical", counts.critical],
              ["degraded", "Degraded", counts.degraded],
              ["ok", "Healthy", counts.ok],
            ] as const).map(([k, l, n]) => (
              <button key={k} onClick={() => setFilter(k as Filter)} style={{
                appearance: "none", border: 0, padding: "5px 12px", borderRadius: 5,
                fontSize: 11, fontWeight: 500,
                background: filter === k ? "var(--bg-3)" : "transparent",
                color: filter === k ? "var(--ink)" : "var(--ink-3)",
                cursor: "pointer", display: "flex", alignItems: "center", gap: 6,
              }}>
                {l} <span className="mono" style={{ fontSize: 9.5, color: "var(--ink-3)" }}>{n}</span>
              </button>
            ))}
          </div>
        }
      />
      <div style={{ padding: 22, display: "flex", flexDirection: "column", gap: 14 }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(6,1fr)", gap: 10 }}>
          {kpis.map((k, i) => (
            <KPI key={k.label} {...k} color={KPI_COLORS[i] ?? "var(--accent)"} />
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
                const active = sel?.id === s.id;
                return (
                  <button key={s.id} onClick={() => setSel(s)} style={{
                    appearance: "none", width: "100%", textAlign: "left", cursor: "pointer",
                    padding: "12px 14px", borderBottom: i < list.length - 1 ? "1px solid var(--line)" : 0,
                    display: "grid", gridTemplateColumns: "1.6fr 90px 60px 1fr 1fr 90px 80px", gap: 10,
                    background: active ? "var(--bg-2)" : "transparent",
                    border: "none", borderLeft: "3px solid " + (active ? "var(--accent)" : "transparent"),
                    color: "var(--ink)", alignItems: "center", fontSize: 12.5,
                  }}>
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
                      ₦{(s.costNgn / 1000).toFixed(0)}K
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

          {sel && (
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
                <PowerMixBar src={sel.source} onSwitch={canMutate ? switchSource : undefined} disabled={busy !== null} />
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10, marginTop: 14 }}>
                  <Metric label="BATTERY" v={sel.battPct} unit="%" tone={sel.battPct < 30 ? "crit" : sel.battPct < 60 ? "warn" : "ok"} />
                  <Metric label="DIESEL"  v={sel.dieselPct} unit="%" tone={sel.dieselPct < 20 ? "crit" : sel.dieselPct < 50 ? "warn" : "ok"} />
                  <Metric label="SOLAR"   v={sel.solarKw} unit="kW" tone={sel.solarKw > 3 ? "ok" : sel.solarKw > 0 ? "warn" : "crit"} />
                  <Metric label="UPTIME"  v={sel.uptimePct} unit="%" tone={sel.uptimePct > 99 ? "ok" : sel.uptimePct > 97 ? "warn" : "crit"} />
                </div>
                {canMutate && (
                  <div style={{ display: "flex", gap: 6, marginTop: 14 }}>
                    <Btn primary small onClick={dispatchRefuel} disabled={busy !== null}>
                      {busy === "refuel" ? "Dispatching…" : "Dispatch Refuel +60L"}
                    </Btn>
                  </div>
                )}
                {toast?.id === sel.id && (
                  <div className="mono" style={{
                    marginTop: 10, fontSize: 10.5, color: "var(--accent)",
                    padding: "6px 10px", background: "var(--accent-dim)",
                    border: "1px solid var(--accent-line)", borderRadius: 5,
                    letterSpacing: ".06em",
                  }}>⌁ {toast.msg}</div>
                )}
              </Card>

              <Section label="24H DIESEL · LITERS">
                <Card pad={14}>
                  {trace.length > 0
                    ? <BackendDieselChart points={trace} />
                    : <SiteDieselChart pct={sel.dieselPct} health={sel.health} />}
                </Card>
              </Section>
            </div>
          )}
        </div>
      </div>
    </>
  );
}

function HealthDot({ h }: { h: EnergySiteDto["health"] }) {
  const c = h === "critical" ? "var(--crit)" : h === "degraded" ? "var(--warn)" : "var(--ok)";
  return <span style={{ display: "inline-block", width: 8, height: 8, borderRadius: "50%", background: c, boxShadow: `0 0 8px ${c}` }} />;
}

function SourceTag({ src }: { src: EnergySiteDto["source"] }) {
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

function PowerMixBar({ src, onSwitch, disabled }: {
  src: EnergySiteDto["source"];
  onSwitch?: (t: EnergySiteDto["source"]) => void;
  disabled?: boolean;
}) {
  const segs: { k: EnergySiteDto["source"]; label: string }[] = [
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
        const interactive = onSwitch && !active;
        return (
          <button
            key={s.k}
            type="button"
            disabled={disabled || !interactive}
            onClick={() => onSwitch?.(s.k)}
            style={{
              flex: 1, padding: 8, borderRadius: 5,
              background: active ? `color-mix(in oklch, ${c} 18%, transparent)` : "var(--bg-3)",
              border: `1px solid ${active ? c : "var(--line)"}`,
              textAlign: "center",
              cursor: interactive ? "pointer" : "default",
              opacity: disabled ? 0.6 : 1,
            }}
          >
            <div className="mono uppr" style={{ fontSize: 9, color: active ? c : "var(--ink-3)", letterSpacing: ".12em", fontWeight: 600 }}>
              {s.label}
            </div>
            <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 3 }}>
              {active ? "ACTIVE" : interactive ? "switch →" : "idle"}
            </div>
          </button>
        );
      })}
    </div>
  );
}

function BackendDieselChart({ points }: { points: { at: string; dieselPct: number }[] }) {
  if (points.length === 0) return null;
  const W = 300, H = 80;
  const data = points.map(p => p.dieselPct);
  const max = 100;
  return (
    <div>
      <svg viewBox={`0 0 ${W} ${H}`} style={{ width: "100%", height: 80, display: "block" }}>
        <line x1="0" y1="0" x2={W} y2="0" stroke="var(--line)" strokeWidth=".5" />
        <polyline
          points={data.map((v, i) => `${(i / (data.length - 1)) * W},${H - (v / max) * H}`).join(" ")}
          fill="none" stroke="var(--accent)" strokeWidth="1.5"
        />
      </svg>
      <div className="mono" style={{ fontSize: 9.5, color: "var(--ink-3)", display: "flex", justifyContent: "space-between", marginTop: 4 }}>
        <span>−24h</span><span>−12h</span><span>NOW</span>
      </div>
    </div>
  );
}
