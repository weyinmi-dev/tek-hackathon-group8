"use client";

import { useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill, Section } from "@/components/UI";
import { PredFaultMini, SiteDieselChart } from "@/components/EnergyCharts";
import { useAuth } from "@/lib/auth";
import { ANOMALY_EVENTS, type AnomalyEvent, type AnomalyKind } from "@/lib/energy-data";

const KIND_META: Record<AnomalyKind, { label: string; icon: string; tone: "ok" | "warn" | "crit" | "info" }> = {
  "fuel-theft":      { label: "Fuel theft",            icon: "⛽", tone: "crit" },
  "sensor-offline":  { label: "Sensor offline",        icon: "⌁", tone: "warn" },
  "gen-overuse":     { label: "Generator overuse",     icon: "⚙", tone: "warn" },
  "battery-degrade": { label: "Battery degradation",   icon: "▥", tone: "info" },
  "predicted-fault": { label: "Predicted fault",       icon: "⌖", tone: "warn" },
};

const MODEL_NAME: Record<AnomalyKind, string> = {
  "fuel-theft":      "IsolationForest-v3",
  "predicted-fault": "Prophet+RuleHybrid",
  "sensor-offline":  "StatThreshold",
  "gen-overuse":     "StatThreshold",
  "battery-degrade": "StatThreshold",
};

export default function AnomaliesPage() {
  const [sel, setSel] = useState<AnomalyEvent>(ANOMALY_EVENTS[0]);
  const { user } = useAuth();

  return (
    <>
      <TopBar
        title="Anomaly Detection"
        sub="Isolation Forest + statistical models · fuel theft · battery degradation · gen misuse"
        right={
          <div style={{ display: "flex", gap: 8 }}>
            <Pill tone="crit" dot>{ANOMALY_EVENTS.filter((a) => a.sev === "critical").length} CRITICAL</Pill>
            <Pill tone="warn" dot>{ANOMALY_EVENTS.filter((a) => a.sev === "warn").length} WARN</Pill>
            <Pill tone="info">7D AVG ↓2</Pill>
          </div>
        }
      />
      <div style={{
        padding: 22,
        display: "grid",
        gridTemplateColumns: "1fr 380px",
        gap: 14,
        height: "calc(100vh - 67px)",
      }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 10, overflowY: "auto", paddingRight: 4 }}>
          {ANOMALY_EVENTS.map((a) => {
            const meta = KIND_META[a.kind];
            const active = sel.id === a.id;
            return (
              <button
                key={a.id}
                onClick={() => setSel(a)}
                style={{
                  appearance: "none", textAlign: "left", cursor: "pointer",
                  background: active ? "var(--bg-2)" : "var(--bg-1)",
                  border: "1px solid " + (active ? "var(--accent-line)" : "var(--line)"),
                  borderRadius: 8, padding: 14,
                  borderLeft: `3px solid ${a.sev === "critical" ? "var(--crit)" : a.sev === "warn" ? "var(--warn)" : "var(--info)"}`,
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8 }}>
                  <span style={{ fontSize: 16 }}>{meta.icon}</span>
                  <Pill tone={meta.tone} dot>{meta.label}</Pill>
                  <span className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>{a.id}</span>
                  <span style={{ flex: 1 }} />
                  <span className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>{a.t}</span>
                </div>
                <div style={{ fontSize: 13, color: "var(--ink)", lineHeight: 1.5, marginBottom: 6 }}>
                  <span className="mono" style={{ color: "var(--accent)", fontSize: 11, marginRight: 8 }}>{a.site}</span>
                  {a.detail}
                </div>
                <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", display: "flex", gap: 14, marginTop: 8 }}>
                  <span>conf {Math.round(a.conf * 100)}%</span>
                  <span>· model: {MODEL_NAME[a.kind]}</span>
                </div>
              </button>
            );
          })}
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 14, overflowY: "auto" }}>
          <Card pad={16}>
            <div className="mono" style={{ fontSize: 10, color: "var(--accent)", marginBottom: 4 }}>{sel.id}</div>
            <div style={{ fontSize: 15, fontWeight: 600, marginBottom: 10 }}>{KIND_META[sel.kind].label}</div>
            <Row k="Site" v={sel.site} />
            <Row k="Detected" v={`${sel.t} WAT`} />
            <Row k="Severity" v={sel.sev} />
            <Row k="Confidence" v={`${Math.round(sel.conf * 100)}%`} last />
          </Card>

          <Section label="SIGNATURE">
            <Card pad={14}>
              {sel.kind === "fuel-theft" && (
                <>
                  <div className="mono uppr" style={{ fontSize: 9.5, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 8 }}>
                    FUEL LEVEL · 24H
                  </div>
                  <SiteDieselChart pct={60} health="critical" />
                  <div style={{
                    marginTop: 10, padding: 10, background: "var(--bg-3)", borderRadius: 5,
                    fontSize: 11.5, lineHeight: 1.5, color: "var(--ink-2)",
                  }}>
                    Detected −18L drop in 6 minutes at 04:18, outside the scheduled refill window
                    (06:00–08:00). No work order open. Pattern matches 11 prior theft incidents in this region.
                  </div>
                </>
              )}
              {sel.kind === "predicted-fault" && (
                <>
                  <div className="mono uppr" style={{ fontSize: 9.5, color: "var(--ink-3)", letterSpacing: ".14em", marginBottom: 8 }}>
                    FAULT PROBABILITY · NEXT 4H
                  </div>
                  <PredFaultMini />
                </>
              )}
              {(sel.kind === "gen-overuse" || sel.kind === "battery-degrade" || sel.kind === "sensor-offline") && (
                <div style={{
                  padding: 10, background: "var(--bg-3)", borderRadius: 5,
                  fontSize: 11.5, lineHeight: 1.5, color: "var(--ink-2)",
                }}>
                  {sel.detail}
                </div>
              )}
            </Card>
          </Section>

          {user?.role !== "viewer" && (
            <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
              <Btn primary>Acknowledge</Btn>
              {sel.kind === "fuel-theft" && <Btn>Dispatch Security</Btn>}
              <Btn>Create Work Order</Btn>
              <Btn ghost>Suppress 24h</Btn>
            </div>
          )}
        </div>
      </div>
    </>
  );
}

function Row({ k, v, last }: { k: string; v: string; last?: boolean }) {
  return (
    <div style={{
      display: "flex", justifyContent: "space-between",
      padding: "8px 0",
      borderBottom: last ? 0 : "1px solid var(--line)",
      fontSize: 12,
    }}>
      <span style={{ color: "var(--ink-3)" }}>{k}</span>
      <span className="mono" style={{ textTransform: "capitalize" }}>{v}</span>
    </div>
  );
}
