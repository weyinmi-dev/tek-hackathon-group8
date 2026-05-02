"use client";

import { useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Card, Pill, Section } from "@/components/UI";
import { COST_BASELINE, COST_WITH_AI } from "@/lib/energy-data";

export default function OptimizePage() {
  const [solar, setSolar] = useState(44);
  const [diesel, setDiesel] = useState(900);
  const [batt, setBatt] = useState(70);

  const baseCost = 21;
  const solarSavings = solar * 0.12;
  const battSavings = (batt - 50) * 0.04;
  const dieselFactor = (diesel - 700) * 0.002;
  const optimizedCost = Math.max(8, baseCost - solarSavings - battSavings + dieselFactor);
  const annualSavingsM = (baseCost - optimizedCost) * 365;

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
              <Slider label="Sites on solar"             value={solar}  unit="%"   min={20}  max={100}  onChange={setSolar} />
              <Slider label="Diesel price"               value={diesel} unit="₦/L" min={700} max={1400} onChange={setDiesel} />
              <Slider label="Battery switch threshold"   value={batt}   unit="%"   min={30}  max={90}   onChange={setBatt} />
            </Card>
          </Section>

          <Section label="PROJECTED IMPACT · 30D">
            <Card pad={16}>
              <BigStat label="Daily OPEX"        value={`₦${optimizedCost.toFixed(1)}M`}                delta={`-₦${(baseCost - optimizedCost).toFixed(1)}M`} good />
              <BigStat label="Annual savings"    value={`₦${(annualSavingsM / 1000).toFixed(2)}B`}      delta="vs baseline" good />
              <BigStat label="Diesel reduction"  value={`-${(solar * 0.5 + (batt - 50) * 0.3).toFixed(0)}%`} delta="fleet-wide" good />
              <BigStat label="CO₂ avoided · yr"  value={`${(solar * 42).toFixed(0)} t`}                delta="based on diesel L → CO₂kg" good last />
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
              <Pill tone="accent" dot>SAVING ₦{(baseCost - optimizedCost).toFixed(1)}M/DAY</Pill>
            </div>
            <CostChart base={COST_BASELINE} opt={COST_WITH_AI} optScale={(baseCost - optimizedCost) / baseCost} />
          </Card>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <Card pad={16}>
              <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 10 }}>
                FLEET ENERGY MIX · NOW
              </div>
              <MixDonut />
            </Card>
            <Card pad={16}>
              <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 10 }}>
                RECOMMENDED ACTIONS
              </div>
              {([
                ["Convert 12 high-diesel sites to hybrid solar", "+₦4.2M/d savings", "accent"],
                ["Raise battery threshold 50→70% on 24 sites",   "+₦1.1M/d savings", "accent"],
                ["Replace 8 batteries (cycle >2000)",            "prevents 4 outages/m", "warn"],
                ["Renegotiate diesel contract — Lekki cluster",  "-₦80/L est.", "info"],
              ] as const).map(([t, v, tone], i, all) => (
                <div key={i} style={{
                  padding: "10px 0",
                  borderBottom: i < all.length - 1 ? "1px solid var(--line)" : 0,
                  display: "flex", justifyContent: "space-between", alignItems: "center", gap: 10,
                }}>
                  <span style={{ fontSize: 12, color: "var(--ink-2)", flex: 1, lineHeight: 1.4 }}>{t}</span>
                  <Pill tone={tone}>{v}</Pill>
                </div>
              ))}
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
      <input
        type="range" min={min} max={max} value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        style={{ width: "100%", accentColor: "var(--accent)" }}
      />
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

function CostChart({ base, opt, optScale }: { base: number[]; opt: number[]; optScale: number }) {
  const W = 600, H = 180, max = 25;
  const optAdj = opt.map((v) => Math.max(8, v * (1 - optScale * 0.3)));
  const pts = (arr: number[]) =>
    arr.map((v, i) => `${40 + (i / (arr.length - 1)) * (W - 40)},${H - (v / max) * H + 20}`).join(" ");
  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{ width: "100%", height: 180, display: "block" }}>
      {[5, 10, 15, 20].map((v) => (
        <g key={v}>
          <line x1="40" y1={H - (v / max) * H + 20} x2={W} y2={H - (v / max) * H + 20} stroke="var(--line)" strokeWidth=".5" strokeDasharray="2 3" />
          <text x="0" y={H - (v / max) * H + 24} fill="var(--ink-3)" fontSize="9" fontFamily="var(--mono)">₦{v}M</text>
        </g>
      ))}
      <polyline points={pts(base)} fill="none" stroke="var(--ink-3)" strokeWidth="1.5" strokeDasharray="4 3" />
      <polyline points={pts(optAdj)} fill="none" stroke="var(--accent)" strokeWidth="2" />
      <polygon points={`${pts(base)} ${pts(optAdj).split(" ").reverse().join(" ")}`} fill="rgba(0,229,160,.08)" />
      {[0, 7, 14, 21, 29].map((d) => (
        <text key={d} x={40 + (d / 29) * (W - 40)} y={H - 2} textAnchor="middle" fill="var(--ink-3)" fontSize="9" fontFamily="var(--mono)">d+{d}</text>
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

function MixDonut() {
  const data = [
    { k: "Diesel",  v: 38, c: "var(--warn)" },
    { k: "Grid",    v: 31, c: "var(--info)" },
    { k: "Battery", v: 18, c: "var(--accent)" },
    { k: "Solar",   v: 13, c: "#f5d76e" },
  ];
  const size = 140, r = 58, cx = size / 2, cy = size / 2;
  let acc = 0;
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        {data.map((d) => {
          const start = acc, end = acc + d.v;
          acc = end;
          const a0 = (start / 100) * Math.PI * 2 - Math.PI / 2;
          const a1 = (end / 100) * Math.PI * 2 - Math.PI / 2;
          const large = end - start > 50 ? 1 : 0;
          const x0 = cx + r * Math.cos(a0), y0 = cy + r * Math.sin(a0);
          const x1 = cx + r * Math.cos(a1), y1 = cy + r * Math.sin(a1);
          return (
            <path key={d.k} d={`M ${cx} ${cy} L ${x0} ${y0} A ${r} ${r} 0 ${large} 1 ${x1} ${y1} Z`} fill={d.c} opacity=".85" />
          );
        })}
        <circle cx={cx} cy={cy} r="34" fill="var(--bg-1)" />
        <text x={cx} y={cy - 2} textAnchor="middle" fontFamily="var(--mono)" fontSize="14" fontWeight="600" fill="var(--ink)">154</text>
        <text x={cx} y={cy + 10} textAnchor="middle" fontFamily="var(--mono)" fontSize="8" fill="var(--ink-3)" letterSpacing="1">SITES</text>
      </svg>
      <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: 6 }}>
        {data.map((d) => (
          <div key={d.k} style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 11.5 }}>
            <span style={{ width: 8, height: 8, borderRadius: 2, background: d.c }} />
            <span style={{ flex: 1, color: "var(--ink-2)" }}>{d.k}</span>
            <span className="mono" style={{ color: "var(--ink)" }}>{d.v}%</span>
          </div>
        ))}
      </div>
    </div>
  );
}
