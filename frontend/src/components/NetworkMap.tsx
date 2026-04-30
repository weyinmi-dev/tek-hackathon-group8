"use client";

import { useState } from "react";
import type { Tower } from "@/lib/types";

const REGIONS: { t: string; x: string; y: string }[] = [
  { t: "IKEJA",      x: "30%", y: "24%" },
  { t: "AGEGE",      x: "12%", y: "18%" },
  { t: "LAGOS WEST", x: "18%", y: "58%" },
  { t: "IKOYI",      x: "58%", y: "60%" },
  { t: "V.I.",       x: "64%", y: "66%" },
  { t: "LEKKI",      x: "78%", y: "70%" },
  { t: "APAPA",      x: "26%", y: "70%" },
  { t: "FESTAC",     x: "6%",  y: "64%" },
];

export function NetworkMap({
  towers,
  compact = false,
  onSelect,
  selectedId,
}: {
  towers: Tower[];
  compact?: boolean;
  onSelect?: (t: Tower) => void;
  selectedId?: string;
}) {
  const [hover, setHover] = useState<Tower | null>(null);

  return (
    <div style={{
      position: "relative", width: "100%", height: "100%",
      background: "var(--bg-map)",
      border: "1px solid var(--line)", borderRadius: 10, overflow: "hidden",
    }}>
      {/* Grid */}
      <svg viewBox="0 0 100 100" preserveAspectRatio="none"
        style={{ position: "absolute", inset: 0, width: "100%", height: "100%", opacity: .4 }}>
        <defs>
          <pattern id="mapgrid" width="5" height="5" patternUnits="userSpaceOnUse">
            <path d="M 5 0 L 0 0 0 5" fill="none" stroke="var(--grid-stroke)" strokeWidth="0.2" />
          </pattern>
        </defs>
        <rect width="100" height="100" fill="url(#mapgrid)" />
      </svg>

      {/* Lagos abstract shape */}
      <svg viewBox="0 0 100 100" preserveAspectRatio="none"
        style={{ position: "absolute", inset: 0, width: "100%", height: "100%" }}>
        <path d="M 40 55 Q 55 50 70 60 Q 80 65 75 75 Q 60 80 50 72 Q 42 65 40 55 Z"
          fill="rgba(91,140,255,.06)" stroke="rgba(91,140,255,.18)" strokeWidth=".2" />
        <path d="M 5 18 Q 20 12 38 16 Q 55 18 72 14 Q 88 16 95 28 L 96 50 Q 92 62 86 70 Q 80 82 72 88 Q 55 92 42 88 Q 28 84 18 76 Q 8 68 5 52 Z"
          fill="none" stroke="var(--map-shape-stroke)" strokeWidth=".25" />
        <path d="M 8 30 L 95 32" stroke="var(--map-shape-stroke)" strokeWidth=".15" strokeDasharray="2 1.5" />
        <path d="M 30 8 L 32 90" stroke="var(--map-shape-stroke)" strokeWidth=".15" strokeDasharray="2 1.5" />
        <circle cx="74" cy="74" r="14" fill="rgba(255,84,112,.08)" />
        <circle cx="74" cy="74" r="9"  fill="rgba(255,84,112,.12)" />
      </svg>

      {/* Region labels */}
      <div style={{ position: "absolute", inset: 0, pointerEvents: "none", color: "var(--ink-3)", fontFamily: "var(--mono)" }}>
        {REGIONS.map((r, i) => (
          <div key={i} className="uppr" style={{
            position: "absolute", left: r.x, top: r.y,
            fontSize: 9, letterSpacing: ".18em", opacity: .5,
            transform: "translate(-50%,-50%)",
          }}>{r.t}</div>
        ))}
      </div>

      {/* Towers */}
      {towers.map(t => {
        const c = t.status === "critical" ? "var(--crit)" : t.status === "warn" ? "var(--warn)" : "var(--accent)";
        const sel = selectedId === t.id;
        return (
          <div key={t.id}
            onMouseEnter={() => setHover(t)} onMouseLeave={() => setHover(null)}
            onClick={() => onSelect?.(t)}
            style={{
              position: "absolute", left: `${t.x}%`, top: `${t.y}%`,
              transform: "translate(-50%,-50%)", cursor: "pointer",
            }}>
            {t.status !== "ok" && (
              <div style={{
                position: "absolute", left: "50%", top: "50%",
                width: 24, height: 24, marginLeft: -12, marginTop: -12, borderRadius: "50%",
                background: c, opacity: .4,
                animation: "pulse-ring 1.8s infinite",
              }} />
            )}
            <div style={{
              width: sel ? 14 : 10, height: sel ? 14 : 10, borderRadius: 2,
              background: c,
              boxShadow: `0 0 0 2px var(--bg), 0 0 12px ${c}`,
              transform: "rotate(45deg)",
              transition: "all .15s",
            }} />
          </div>
        );
      })}

      {hover && (
        <div style={{
          position: "absolute",
          left: `${hover.x}%`, top: `${hover.y}%`,
          transform: `translate(${hover.x > 60 ? "-110%" : "10%"}, -50%)`,
          background: "var(--bg-2)", border: "1px solid var(--line-2)",
          padding: "8px 10px", borderRadius: 6, minWidth: 180,
          fontSize: 11, pointerEvents: "none", zIndex: 5,
          boxShadow: "var(--map-overlay-shadow)",
          animation: "fadein .12s ease-out",
        }}>
          <div className="mono" style={{ fontSize: 10, color: "var(--accent)", marginBottom: 3 }}>{hover.id}</div>
          <div style={{ fontWeight: 500, marginBottom: 4 }}>{hover.name}</div>
          <div className="mono" style={{ display: "flex", justifyContent: "space-between", color: "var(--ink-3)", fontSize: 10 }}>
            <span>SIG {hover.signal}%</span><span>LOAD {hover.load}%</span>
          </div>
          {hover.issue && <div style={{ marginTop: 6, fontSize: 10.5, color: hover.status === "critical" ? "var(--crit)" : "var(--warn)" }}>⚠ {hover.issue}</div>}
        </div>
      )}

      {!compact && (
        <>
          <div style={{ position: "absolute", top: 14, right: 14, padding: "6px 10px", background: "var(--map-overlay-bg)", border: "1px solid var(--line)", borderRadius: 6, fontFamily: "var(--mono)", fontSize: 10, color: "var(--ink-3)", letterSpacing: ".10em" }}>N ↑ · 6.50°N 3.40°E</div>
          <div style={{ position: "absolute", bottom: 14, right: 14, padding: "8px 10px", background: "var(--map-overlay-bg)", border: "1px solid var(--line)", borderRadius: 6, display: "flex", flexDirection: "column", gap: 4, fontFamily: "var(--mono)", fontSize: 10 }}>
            <Legend c="var(--accent)" t="OPTIMAL" />
            <Legend c="var(--warn)"   t="DEGRADED" />
            <Legend c="var(--crit)"   t="CRITICAL" />
          </div>
        </>
      )}
    </div>
  );
}

function Legend({ c, t }: { c: string; t: string }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 6, color: "var(--ink-2)", letterSpacing: ".10em" }}>
      <div style={{ width: 8, height: 8, background: c, transform: "rotate(45deg)", boxShadow: `0 0 8px ${c}` }} />
      <span>{t}</span>
    </div>
  );
}
