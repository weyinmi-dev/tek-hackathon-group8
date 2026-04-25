"use client";

import React, { CSSProperties, ReactNode } from "react";

type Tone = "ok" | "warn" | "crit" | "info" | "neutral" | "accent";
const toneVar: Record<Tone, string> = {
  ok: "var(--ok)", warn: "var(--warn)", crit: "var(--crit)",
  info: "var(--info)", neutral: "var(--ink-2)", accent: "var(--accent)",
};

export function Card({ children, style, pad = 16, ...rest }: { children: ReactNode; style?: CSSProperties; pad?: number }) {
  return (
    <div style={{
      background: "var(--bg-1)", border: "1px solid var(--line)",
      borderRadius: 10, padding: pad, ...style,
    }} {...rest}>{children}</div>
  );
}

export function Section({ label, right, children, style }: {
  label: string; right?: ReactNode; children: ReactNode; style?: CSSProperties;
}) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10, ...style }}>
      <div style={{ display: "flex", alignItems: "baseline", justifyContent: "space-between" }}>
        <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".12em" }}>{label}</div>
        {right}
      </div>
      {children}
    </div>
  );
}

export function Pill({ tone = "info", dot = false, children, style }: {
  tone?: Tone; dot?: boolean; children: ReactNode; style?: CSSProperties;
}) {
  const c = toneVar[tone];
  return (
    <span className="mono uppr" style={{
      display: "inline-flex", alignItems: "center", gap: 6,
      height: 20, padding: "0 8px", borderRadius: 4,
      fontSize: 9.5, letterSpacing: ".10em", fontWeight: 600,
      color: c,
      background: `color-mix(in oklch, ${c} 14%, transparent)`,
      border: `1px solid color-mix(in oklch, ${c} 30%, transparent)`,
      ...style,
    }}>
      {dot && <span style={{ width: 5, height: 5, borderRadius: "50%", background: c, boxShadow: `0 0 6px ${c}` }} />}
      {children}
    </span>
  );
}

export function Spark({ data, color = "var(--accent)", height = 28 }: { data: number[]; color?: string; height?: number }) {
  if (data.length === 0) return null;
  const min = Math.min(...data), max = Math.max(...data), range = max - min || 1;
  const w = 100;
  const pts = data.map((v, i) => {
    const x = (i / (data.length - 1)) * w;
    const y = height - ((v - min) / range) * height;
    return `${x.toFixed(2)},${y.toFixed(2)}`;
  }).join(" ");
  const area = `0,${height} ${pts} ${w},${height}`;
  const id = "sp" + Math.random().toString(36).slice(2, 7);
  return (
    <svg viewBox={`0 0 ${w} ${height}`} preserveAspectRatio="none" style={{ width: "100%", height, display: "block", marginTop: 4 }}>
      <defs>
        <linearGradient id={id} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity=".35" />
          <stop offset="100%" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      <polygon points={area} fill={`url(#${id})`} />
      <polyline points={pts} fill="none" stroke={color} strokeWidth="1.2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function KPI({ label, value, unit, delta, trend, sub, spark, color }: {
  label: string; value: string; unit?: string; delta?: string;
  trend?: "up" | "down"; sub?: string; spark?: number[]; color?: string;
}) {
  return (
    <Card pad={14} style={{ display: "flex", flexDirection: "column", gap: 8, minHeight: 118, position: "relative", overflow: "hidden" }}>
      <div className="mono uppr" style={{ fontSize: 9.5, color: "var(--ink-3)", letterSpacing: ".12em" }}>{label}</div>
      <div style={{ display: "flex", alignItems: "baseline", gap: 6 }}>
        <div className="mono" style={{ fontSize: 30, fontWeight: 600, color: "var(--ink)", letterSpacing: "-.02em", lineHeight: 1 }}>{value}</div>
        {unit && <div className="mono" style={{ fontSize: 13, color: "var(--ink-3)" }}>{unit}</div>}
      </div>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
        <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>{sub}</div>
        {delta && (
          <div className="mono" style={{
            fontSize: 10, fontWeight: 600,
            color: trend === "up" ? "var(--ok)" : "var(--crit)"
          }}>
            {trend === "up" ? "▲" : "▼"} {delta}
          </div>
        )}
      </div>
      {spark && <Spark data={spark} color={color || "var(--accent)"} />}
    </Card>
  );
}

export function Btn(
  { children, primary, ghost, small, style, ...rest }:
    React.ButtonHTMLAttributes<HTMLButtonElement> & { primary?: boolean; ghost?: boolean; small?: boolean }
) {
  const base: CSSProperties = {
    appearance: "none", border: "1px solid var(--line-2)",
    background: "var(--bg-2)", color: "var(--ink)",
    borderRadius: 6, padding: small ? "5px 10px" : "7px 12px",
    fontSize: small ? 11 : 12, fontWeight: 500, cursor: "pointer",
    display: "inline-flex", alignItems: "center", gap: 6,
    transition: "all .12s",
  };
  const variants: CSSProperties = primary
    ? { background: "var(--accent)", color: "#001a10", border: "1px solid transparent", fontWeight: 600 }
    : ghost
      ? { background: "transparent", border: "1px solid transparent", color: "var(--ink-2)" }
      : {};
  return <button {...rest} style={{ ...base, ...variants, ...style }}>{children}</button>;
}

export function Bar({ pct, tone = "accent" }: { pct: number; tone?: Tone }) {
  const c = toneVar[tone];
  return (
    <div style={{ height: 4, background: "var(--bg-3)", borderRadius: 2, overflow: "hidden" }}>
      <div style={{ height: "100%", width: `${Math.max(0, Math.min(100, pct))}%`, background: c, borderRadius: 2, transition: "width .4s" }} />
    </div>
  );
}

export function Donut({ pct, color = "var(--accent)", size = 80 }: { pct: number; color?: string; size?: number }) {
  const r = (size - 12) / 2;
  const c = 2 * Math.PI * r;
  const off = c - (pct / 100) * c;
  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
      <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="var(--bg-3)" strokeWidth="6" />
      <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={color} strokeWidth="6"
        strokeDasharray={c} strokeDashoffset={off} strokeLinecap="round"
        transform={`rotate(-90 ${size / 2} ${size / 2})`} />
      <text x={size / 2} y={size / 2 + 4} textAnchor="middle"
        fontFamily="var(--mono)" fontSize="13" fontWeight="600" fill="var(--ink)">{pct}%</text>
    </svg>
  );
}
