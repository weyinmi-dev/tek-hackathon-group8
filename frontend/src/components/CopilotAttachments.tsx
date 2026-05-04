"use client";

import { useEffect, useState } from "react";
import { Pill } from "@/components/UI";
import { NetworkMap } from "@/components/NetworkMap";
import { api } from "@/lib/api";
import type { Alert, MapResponse } from "@/lib/types";

// Renders the chart cards and mini-map that follow the textual answer.
// Attachment string conventions match the handoff prototype + the strings the
// backend's RecommendationSkill emits in CopilotAnswer.attachments:
//   lagosWestChart · lekkiChart · ikejaChart · predictChart · outageTable
//   miniMap-<region>   (region = lagosWest, lekki, ikeja, …)

type Tone = "accent" | "warn" | "crit";

const REGION_CHARTS: Record<string, { title: string; data: number[]; unit: string; threshold: number; tone: Tone }> = {
  lagosWestChart: {
    title: "LATENCY · LAGOS WEST · LAST 2H",
    data: [34,38,42,48,55,62,72,84,96,110,124,138,142,140,138,135],
    unit: "ms", threshold: 80, tone: "accent",
  },
  lekkiChart: {
    title: "PACKET LOSS · TWR-LEK-003",
    data: [2,2,3,2,4,8,18,32,48,58,60,60,58,55,52,50],
    unit: "%", threshold: 20, tone: "crit",
  },
  ikejaChart: {
    title: "JITTER · TWR-IKJ-019",
    data: [8,9,10,12,15,18,22,28,32,30,28,25,22,20,18,16],
    unit: "ms", threshold: 20, tone: "warn",
  },
};

// Module-level caches so multiple assistant turns on the same page don't
// re-hit /api/map and /api/alerts. Cleared on full reload.
let mapCache: MapResponse | null = null;
let mapInflight: Promise<MapResponse> | null = null;
let alertsCache: Alert[] | null = null;
let alertsInflight: Promise<Alert[]> | null = null;

function getMap(): Promise<MapResponse> {
  if (mapCache) return Promise.resolve(mapCache);
  mapInflight ??= api.map().then((m) => { mapCache = m; mapInflight = null; return m; });
  return mapInflight;
}
function getAlerts(): Promise<Alert[]> {
  if (alertsCache) return Promise.resolve(alertsCache);
  alertsInflight ??= api.alerts().then((a) => { alertsCache = a; alertsInflight = null; return a; });
  return alertsInflight;
}

export function CopilotAttachments({ attachments }: { attachments: string[] }) {
  if (!attachments || attachments.length === 0) return null;

  const miniMapAttachment = attachments.find((a) => a.startsWith("miniMap"));
  const focus = miniMapAttachment?.split("-")[1] ?? "";

  return (
    <div style={{ marginTop: 14, display: "flex", flexDirection: "column", gap: 10 }}>
      {Object.entries(REGION_CHARTS).map(([key, cfg]) =>
        attachments.includes(key) ? <ChartCard key={key} {...cfg} /> : null,
      )}
      {attachments.includes("predictChart") && <PredictChart />}
      {attachments.includes("outageTable") && <OutageTable />}
      {miniMapAttachment && <MiniMap focus={focus} />}
    </div>
  );
}

function ChartCard({ title, data, unit, threshold, tone }: {
  title: string; data: number[]; unit: string; threshold: number; tone: Tone;
}) {
  const max = Math.max(...data, threshold) * 1.1;
  const c = tone === "crit" ? "var(--crit)" : tone === "warn" ? "var(--warn)" : "var(--accent)";
  return (
    <div style={{ background: "var(--bg-2)", border: "1px solid var(--line)", borderRadius: 6, padding: 12 }}>
      <div className="mono uppr" style={{
        fontSize: 9.5, color: "var(--ink-3)", letterSpacing: ".14em",
        marginBottom: 8, display: "flex", justifyContent: "space-between",
      }}>
        <span>{title}</span>
        <span style={{ color: c }}>{data[data.length - 1]}{unit}</span>
      </div>
      <svg viewBox="0 0 200 60" style={{ width: "100%", height: 60, display: "block" }}>
        <line x1="0" y1={60 - (threshold / max) * 60} x2="200" y2={60 - (threshold / max) * 60}
          stroke="var(--line-2)" strokeWidth=".5" strokeDasharray="2 2" />
        <polyline
          points={data.map((v, i) => `${(i / (data.length - 1)) * 200},${60 - (v / max) * 60}`).join(" ")}
          fill="none" stroke={c} strokeWidth="1.5"
        />
        {data.map((v, i) =>
          v > threshold ? (
            <circle key={i} cx={(i / (data.length - 1)) * 200} cy={60 - (v / max) * 60} r="1.5" fill={c} />
          ) : null,
        )}
      </svg>
      <div className="mono" style={{ fontSize: 9.5, color: "var(--ink-3)", display: "flex", justifyContent: "space-between", marginTop: 4 }}>
        <span>−2h</span><span>−1h</span><span>now</span>
      </div>
    </div>
  );
}

function PredictChart() {
  return (
    <div style={{ background: "var(--bg-2)", border: "1px solid var(--line)", borderRadius: 6, padding: 12 }}>
      <div className="mono uppr" style={{
        fontSize: 9.5, color: "var(--ink-3)", letterSpacing: ".14em",
        marginBottom: 8, display: "flex", justifyContent: "space-between",
      }}>
        <span>FAILURE PROBABILITY · TWR-LAG-W-031 · NEXT 4H</span>
        <span style={{ color: "var(--warn)" }}>87% by 18:42</span>
      </div>
      <svg viewBox="0 0 200 60" style={{ width: "100%", height: 60, display: "block" }}>
        <line x1="100" y1="0" x2="100" y2="60" stroke="var(--line-2)" strokeDasharray="2 2" />
        <text x="102" y="10" fill="var(--ink-3)" fontSize="6" fontFamily="var(--mono)">NOW</text>
        <polyline points="0,55 30,52 60,48 100,40" fill="none" stroke="var(--ink-2)" strokeWidth="1.5" />
        <polyline points="100,40 130,28 160,15 200,8" fill="none" stroke="var(--warn)" strokeWidth="1.5" strokeDasharray="3 2" />
        <polygon points="100,40 130,28 160,15 200,8 200,60 100,60" fill="rgba(255,181,71,.10)" />
      </svg>
      <div className="mono" style={{ fontSize: 9.5, color: "var(--ink-3)", display: "flex", justifyContent: "space-between", marginTop: 4 }}>
        <span>−2h</span><span>NOW</span><span>+2h</span><span>+4h</span>
      </div>
    </div>
  );
}

function OutageTable() {
  const [alerts, setAlerts] = useState<Alert[]>([]);
  useEffect(() => {
    let alive = true;
    getAlerts().then((a) => { if (alive) setAlerts(a); }).catch(() => { /* keep empty */ });
    return () => { alive = false; };
  }, []);
  const rows = alerts
    .filter((a) => a.status === "active" || a.status === "investigating")
    .slice(0, 4);
  if (rows.length === 0) return null;
  return (
    <div style={{ background: "var(--bg-2)", border: "1px solid var(--line)", borderRadius: 6, overflow: "hidden" }}>
      <div className="mono uppr" style={{
        fontSize: 9.5, color: "var(--ink-3)", letterSpacing: ".14em",
        padding: "10px 12px", borderBottom: "1px solid var(--line)",
      }}>
        ACTIVE INCIDENTS · CITED
      </div>
      {rows.map((a, i) => (
        <div key={a.id} style={{
          padding: "10px 12px", display: "flex", gap: 10, alignItems: "center",
          borderBottom: i < rows.length - 1 ? "1px solid var(--line)" : 0, fontSize: 11.5,
        }}>
          <Pill tone={a.sev === "critical" ? "crit" : a.sev === "warn" ? "warn" : "info"} dot>{a.id}</Pill>
          <span style={{ flex: 1 }}>{a.title}</span>
          <span className="mono" style={{ color: "var(--ink-3)", fontSize: 10.5 }}>{a.tower}</span>
          <span className="mono" style={{ color: "var(--ink-3)", fontSize: 10.5 }}>{a.time}</span>
        </div>
      ))}
    </div>
  );
}

function MiniMap({ focus }: { focus: string }) {
  const [map, setMap] = useState<MapResponse | null>(mapCache);
  useEffect(() => {
    if (mapCache) { setMap(mapCache); return; }
    let alive = true;
    getMap().then((m) => { if (alive) setMap(m); }).catch(() => { /* leave null */ });
    return () => { alive = false; };
  }, []);
  return (
    <div style={{ background: "var(--bg-2)", border: "1px solid var(--line)", borderRadius: 6, padding: 12 }}>
      <div className="mono uppr" style={{
        fontSize: 9.5, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 8,
      }}>
        MAP CONTEXT · {focus.toUpperCase()}
      </div>
      <div style={{ height: 220, position: "relative" }}>
        {map && <NetworkMap towers={map.towers} compact />}
      </div>
    </div>
  );
}
