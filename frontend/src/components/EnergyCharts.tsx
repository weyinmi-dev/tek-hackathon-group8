"use client";

import { FUEL_TRACE_THEFT, type SiteHealth } from "@/lib/energy-data";

export function SiteDieselChart({ pct, health }: { pct: number; health: SiteHealth }) {
  const data = health === "critical"
    ? FUEL_TRACE_THEFT
    : Array.from({ length: 24 }, (_, i) => Math.max(20, pct + 4 * Math.sin(i / 4) - i * 0.4));
  const max = 100, W = 300, H = 80;
  return (
    <div>
      <svg viewBox={`0 0 ${W} ${H}`} style={{ width: "100%", height: 80, display: "block" }}>
        <line x1="0" y1="0" x2={W} y2="0" stroke="var(--line)" strokeWidth=".5" />
        <polyline
          points={data.map((v, i) => `${(i / (data.length - 1)) * W},${H - (v / max) * H}`).join(" ")}
          fill="none"
          stroke={health === "critical" ? "var(--crit)" : "var(--accent)"}
          strokeWidth="1.5"
        />
        {health === "critical" && (
          <g>
            <circle cx={(18 / 23) * W} cy={H - (61 / 100) * H} r="3" fill="var(--crit)" />
            <text x={(18 / 23) * W} y={H - (61 / 100) * H - 6} textAnchor="middle" fill="var(--crit)" fontSize="8" fontFamily="var(--mono)">⚠ THEFT</text>
          </g>
        )}
      </svg>
      <div className="mono" style={{ fontSize: 9.5, color: "var(--ink-3)", display: "flex", justifyContent: "space-between", marginTop: 4 }}>
        <span>−24h</span><span>−12h</span><span>NOW</span>
      </div>
    </div>
  );
}

export function PredFaultMini() {
  const W = 300, H = 80;
  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{ width: "100%", height: 80, display: "block" }}>
      <line x1={W / 2} y1="0" x2={W / 2} y2={H} stroke="var(--line-2)" strokeDasharray="2 2" />
      <text x={W / 2 + 4} y="10" fill="var(--ink-3)" fontSize="8" fontFamily="var(--mono)">NOW</text>
      <polyline points={`0,72 60,68 100,62 ${W / 2},54`} fill="none" stroke="var(--ink-2)" strokeWidth="1.5" />
      <polyline points={`${W / 2},54 200,38 250,20 ${W},10`} fill="none" stroke="var(--warn)" strokeWidth="1.5" strokeDasharray="3 2" />
      <polygon points={`${W / 2},54 200,38 250,20 ${W},10 ${W},${H} ${W / 2},${H}`} fill="rgba(255,181,71,.10)" />
      <text x={W - 4} y={20} textAnchor="end" fill="var(--warn)" fontSize="9" fontFamily="var(--mono)">87% by 18:42</text>
    </svg>
  );
}
