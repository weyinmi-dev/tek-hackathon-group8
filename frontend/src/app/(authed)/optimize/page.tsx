"use client";

import { useEffect, useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Card, Pill, Section } from "@/components/UI";
import { api } from "@/lib/api";
import type { EnergyRecommendation, OptimizationProjection } from "@/lib/types";

export default function OptimizePage() {
  const [solar, setSolar] = useState(44);
  const [diesel, setDiesel] = useState(900);
  const [batt, setBatt] = useState(70);
  const [proj, setProj] = useState<OptimizationProjection | null>(null);
  const [recs, setRecs] = useState<EnergyRecommendation[]>([]);

  // Recompute projection whenever the sliders change. Debounce to avoid
  // hammering the backend during a slider drag.
  useEffect(() => {
    const t = setTimeout(() => {
      void api.energy
        .optimization({ solarPct: solar, dieselPriceNgnPerLitre: diesel, batteryThresholdPct: batt })
        .then(setProj)
        .catch(() => { /* keep last known projection */ });
    }, 200);
    return () => clearTimeout(t);
  }, [solar, diesel, batt]);

  // Recommendations are derived from current site state, not from sliders, so
  // they only reload on mount + every 30s as the ticker mutates the fleet.
  useEffect(() => {
    async function load() {
      try {
        const r = await api.energy.recommendations();
        setRecs(r.recommendations);
      } catch { /* keep last list */ }
    }
    void load();
    const id = setInterval(() => void load(), 30_000);
    return () => clearInterval(id);
  }, []);

  const baselineDaily = proj?.baselineDailyOpexMillionsNgn ?? 21.0;
  const optimizedDaily = proj?.optimizedDailyOpexMillionsNgn ?? 14.7;
  const dailySaving = proj?.dailySavingsMillionsNgn ?? (baselineDaily - optimizedDaily);
  const annualSaving = proj?.annualSavingsBillionsNgn ?? (dailySaving * 365 / 1000);

  return (
    <>
      <TopBar
        title="Cost Optimization"
        sub="Simulate solar adoption · diesel pricing · battery thresholds — 30d horizon"
      />
      <div style={{ padding: 22, display: "grid", gridTemplateColumns: "380px 1fr", gap: 14 }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <Section label="SCENARIO INPUTS">
            <Card pad={16}>
              <Slider label="Sites on solar"           value={solar}  unit="%"   min={20}  max={100}  onChange={setSolar} />
              <Slider label="Diesel price"             value={diesel} unit="₦/L" min={700} max={1400} onChange={setDiesel} />
              <Slider label="Battery switch threshold" value={batt}   unit="%"   min={30}  max={90}   onChange={setBatt} />
            </Card>
          </Section>

          <Section label="PROJECTED IMPACT · 30D">
            <Card pad={16}>
              <BigStat label="Daily OPEX"        value={`₦${optimizedDaily.toFixed(1)}M`} delta={`-₦${dailySaving.toFixed(1)}M`} good />
              <BigStat label="Annual savings"    value={`₦${annualSaving.toFixed(2)}B`}    delta="vs baseline" good />
              <BigStat label="Diesel reduction"  value={`-${proj?.dieselReductionPct ?? 0}%`} delta="fleet-wide" good />
              <BigStat label="CO₂ avoided · yr"  value={`${proj?.co2AvoidedTonnesPerYear ?? 0} t`} delta="based on diesel L → CO₂kg" good last />
            </Card>
          </Section>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <Card pad={16}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 14 }}>
              <div>
                <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 4 }}>
                  OPEX PROJECTION · BASELINE vs AI-OPTIMIZED
                </div>
                <div style={{ fontSize: 14, fontWeight: 500 }}>30-day forward simulation · ₦M/day</div>
              </div>
              <Pill tone="accent" dot>SAVING ₦{dailySaving.toFixed(1)}M/DAY</Pill>
            </div>
            {proj && <CostChart base={proj.baselineSeries} opt={proj.optimizedSeries} />}
          </Card>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card pad={16}>
              <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 10 }}>
                FLEET ENERGY MIX · NOW
              </div>
              {proj && <MixDonut data={proj.energyMix} />}
            </Card>
            <Card pad={16}>
              <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 10 }}>
                RECOMMENDED ACTIONS
              </div>
              {recs.map((r, i) => (
                <div key={i} style={{
                  padding: "10px 0",
                  borderBottom: i < recs.length - 1 ? "1px solid var(--line)" : 0,
                  display: "flex", justifyContent: "space-between", alignItems: "center", gap: 10,
                }}>
                  <div style={{ flex: 1, fontSize: 12, color: "var(--ink-2)", lineHeight: 1.4 }}>
                    <div>{r.title}</div>
                    {r.detail && (
                      <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 2 }}>
                        {r.detail}
                      </div>
                    )}
                  </div>
                  <Pill tone={r.tone}>
                    {r.estimatedDailySavingsNgn > 0
                      ? `+₦${(r.estimatedDailySavingsNgn / 1_000_000).toFixed(1)}M/d`
                      : "action"}
                  </Pill>
                </div>
              ))}
              {recs.length === 0 && (
                <div className="mono" style={{ color: "var(--ink-3)", padding: 10, fontSize: 11 }}>
                  ⌁ no recommendations
                </div>
              )}
            </Card>
          </div>
        </div>
      </div>
    </>
  );
}

function Slider({ label, value, unit, min, max, onChange }: {
  label: string; value: number; unit: string; min: number; max: number; onChange: (v: number) => void;
}) {
  return (
    <div style={{ padding: "10px 0", borderBottom: "1px solid var(--line)" }}>
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 8, fontSize: 11.5 }}>
        <span style={{ color: "var(--ink-2)" }}>{label}</span>
        <span className="mono" style={{ color: "var(--accent)", fontWeight: 600 }}>
          {value.toLocaleString()}{unit}
        </span>
      </div>
      <input type="range" min={min} max={max} value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        style={{ width: "100%", accentColor: "var(--accent)" }} />
      <div className="mono" style={{ display: "flex", justifyContent: "space-between", fontSize: 9.5, color: "var(--ink-3)", marginTop: 2 }}>
        <span>{min.toLocaleString()}{unit}</span>
        <span>{max.toLocaleString()}{unit}</span>
      </div>
    </div>
  );
}

function BigStat({ label, value, delta, good, last }: {
  label: string; value: string; delta: string; good?: boolean; last?: boolean;
}) {
  return (
    <div style={{ padding: "10px 0", borderBottom: last ? 0 : "1px solid var(--line)" }}>
      <div className="mono uppr" style={{ fontSize: 9.5, color: "var(--ink-3)", letterSpacing: ".12em", marginBottom: 4 }}>
        {label}
      </div>
      <div style={{ display: "flex", alignItems: "baseline", gap: 8 }}>
        <div className="mono" style={{
          fontSize: 24, fontWeight: 600,
          color: good ? "var(--accent)" : "var(--ink)",
          letterSpacing: "-.02em",
        }}>{value}</div>
        <div className="mono" style={{ fontSize: 11, color: good ? "var(--ok)" : "var(--ink-3)" }}>{delta}</div>
      </div>
    </div>
  );
}

function CostChart({ base, opt }: { base: number[]; opt: number[] }) {
  const W = 600, H = 180;
  const max = Math.max(25, ...base, ...opt);
  const pts = (arr: number[]) =>
    arr.map((v, i) => `${40 + (i / Math.max(1, arr.length - 1)) * (W - 40)},${H - (v / max) * H + 20}`).join(" ");
  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{ width: "100%", height: 180, display: "block" }}>
      {[5, 10, 15, 20].map((v) => (
        <g key={v}>
          <line x1="40" y1={H - (v / max) * H + 20} x2={W} y2={H - (v / max) * H + 20}
            stroke="var(--line)" strokeWidth=".5" strokeDasharray="2 3" />
          <text x="0" y={H - (v / max) * H + 24} fill="var(--ink-3)" fontSize="9" fontFamily="var(--mono)">₦{v}M</text>
        </g>
      ))}
      <polyline points={pts(base)} fill="none" stroke="var(--ink-3)" strokeWidth="1.5" strokeDasharray="4 3" />
      <polyline points={pts(opt)} fill="none" stroke="var(--accent)" strokeWidth="2" />
      <polygon points={`${pts(base)} ${pts(opt).split(" ").reverse().join(" ")}`} fill="rgba(0,229,160,.08)" />
      {[0, 7, 14, 21, 29].map((d) => (
        <text key={d} x={40 + (d / 29) * (W - 40)} y={H - 2} textAnchor="middle"
          fill="var(--ink-3)" fontSize="9" fontFamily="var(--mono)">d+{d}</text>
      ))}
      <g transform={`translate(${W - 180}, 30)`}>
        <rect width="170" height="44" fill="rgba(10,14,22,.7)" stroke="var(--line)" rx="4" />
        <line x1="10" y1="16" x2="28" y2="16" stroke="var(--ink-3)" strokeWidth="1.5" strokeDasharray="4 3" />
        <text x="34" y="19" fill="var(--ink-2)" fontSize="10" fontFamily="var(--mono)">Baseline</text>
        <line x1="10" y1="32" x2="28" y2="32" stroke="var(--accent)" strokeWidth="2" />
        <text x="34" y="35" fill="var(--ink-2)" fontSize="10" fontFamily="var(--mono)">AI-optimized</text>
      </g>
    </svg>
  );
}

function MixDonut({ data }: { data: { source: string; pct: number }[] }) {
  const colors: Record<string, string> = {
    Diesel: "var(--warn)", Grid: "var(--info)", Battery: "var(--accent)", Solar: "#f5d76e",
  };
  const size = 140, r = 58, cx = size / 2, cy = size / 2;
  let acc = 0;
  const total = Math.max(1, data.reduce((s, d) => s + d.pct, 0));
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        {data.map((d) => {
          const c = colors[d.source] ?? "var(--ink-3)";
          const start = acc, end = acc + (d.pct / total) * 100;
          acc = end;
          const a0 = (start / 100) * Math.PI * 2 - Math.PI / 2;
          const a1 = (end / 100) * Math.PI * 2 - Math.PI / 2;
          const large = end - start > 50 ? 1 : 0;
          const x0 = cx + r * Math.cos(a0), y0 = cy + r * Math.sin(a0);
          const x1 = cx + r * Math.cos(a1), y1 = cy + r * Math.sin(a1);
          return (
            <path key={d.source} d={`M ${cx} ${cy} L ${x0} ${y0} A ${r} ${r} 0 ${large} 1 ${x1} ${y1} Z`}
              fill={c} opacity=".85" />
          );
        })}
        <circle cx={cx} cy={cy} r="34" fill="var(--bg-1)" />
        <text x={cx} y={cy + 4} textAnchor="middle" fontFamily="var(--mono)" fontSize="11" fill="var(--ink-3)" letterSpacing="1">FLEET</text>
      </svg>
      <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: 6 }}>
        {data.map((d) => (
          <div key={d.source} style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 11.5 }}>
            <span style={{ width: 8, height: 8, borderRadius: 2, background: colors[d.source] ?? "var(--ink-3)" }} />
            <span style={{ flex: 1, color: "var(--ink-2)" }}>{d.source}</span>
            <span className="mono" style={{ color: "var(--ink)" }}>{d.pct}%</span>
          </div>
        ))}
      </div>
    </div>
  );
}
