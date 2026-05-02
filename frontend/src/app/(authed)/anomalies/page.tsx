"use client";

import { useEffect, useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill, Section } from "@/components/UI";
import { PredFaultMini, SiteDieselChart } from "@/components/EnergyCharts";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { isEngineer } from "@/lib/rbac";
import type { EnergyAnomalyDto } from "@/lib/types";

type AnomalyKind = EnergyAnomalyDto["kind"];

const KIND_META: Record<AnomalyKind, { label: string; icon: string; tone: "ok" | "warn" | "crit" | "info" }> = {
  "fuel-theft":      { label: "Fuel theft",            icon: "⛽", tone: "crit" },
  "sensor-offline":  { label: "Sensor offline",        icon: "⌁", tone: "warn" },
  "gen-overuse":     { label: "Generator overuse",     icon: "⚙", tone: "warn" },
  "battery-degrade": { label: "Battery degradation",   icon: "▥", tone: "info" },
  "predicted-fault": { label: "Predicted fault",       icon: "⌖", tone: "warn" },
};

export default function AnomaliesPage() {
  const [anomalies, setAnomalies] = useState<EnergyAnomalyDto[]>([]);
  const [sel, setSel] = useState<EnergyAnomalyDto | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [toast, setToast] = useState<{ id: string; msg: string } | null>(null);
  const { user } = useAuth();
  const canMutate = isEngineer(user?.role);

  async function refresh() {
    const r = await api.energy.anomalies(50);
    setAnomalies(r.anomalies);
    setSel((prev) => prev
      ? (r.anomalies.find((a) => a.id === prev.id) ?? r.anomalies[0] ?? null)
      : (r.anomalies[0] ?? null));
  }
  useEffect(() => {
    void refresh();
    const id = setInterval(() => void refresh(), 30_000);
    return () => clearInterval(id);
  }, []);

  function flash(id: string, msg: string) {
    setToast({ id, msg });
    setTimeout(() => setToast((cur) => cur?.id === id ? null : cur), 2400);
  }

  async function ack() {
    if (!sel) return;
    setBusy(sel.id);
    try {
      await api.energy.ackAnomaly(sel.id);
      await refresh();
      flash(sel.id, "Anomaly acknowledged");
    } catch (e) {
      flash(sel.id, e instanceof Error ? e.message : "Acknowledge failed");
    } finally {
      setBusy(null);
    }
  }

  return (
    <>
      <TopBar
        title="Anomaly Detection"
        sub="Isolation Forest + statistical models · fuel theft · battery degradation · gen misuse"
        right={
          <div style={{ display: "flex", gap: 8 }}>
            <Pill tone="crit" dot>{anomalies.filter((a) => a.sev === "critical").length} CRITICAL</Pill>
            <Pill tone="warn" dot>{anomalies.filter((a) => a.sev === "warn").length} WARN</Pill>
            <Pill tone="info">{anomalies.filter(a => a.acknowledged).length} ACKED</Pill>
          </div>
        }
      />
      <div style={{
        padding: 22, display: "grid", gridTemplateColumns: "1fr 380px",
        gap: 14, height: "calc(100vh - 67px)",
      }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 10, overflowY: "auto", paddingRight: 4 }}>
          {anomalies.map((a) => {
            const meta = KIND_META[a.kind];
            const active = sel?.id === a.id;
            return (
              <button key={a.id} onClick={() => setSel(a)} style={{
                appearance: "none", textAlign: "left", cursor: "pointer",
                background: active ? "var(--bg-2)" : "var(--bg-1)",
                border: "1px solid " + (active ? "var(--accent-line)" : "var(--line)"),
                borderRadius: 8, padding: 14,
                borderLeft: `3px solid ${a.sev === "critical" ? "var(--crit)" : a.sev === "warn" ? "var(--warn)" : "var(--info)"}`,
                opacity: a.acknowledged ? 0.55 : 1,
              }}>
                <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8 }}>
                  <span style={{ fontSize: 16 }}>{meta.icon}</span>
                  <Pill tone={meta.tone} dot>{meta.label}</Pill>
                  <span className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>{a.id.slice(0, 8)}</span>
                  {a.acknowledged && <Pill tone="ok">ACKED</Pill>}
                  <span style={{ flex: 1 }} />
                  <span className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>{a.t}</span>
                </div>
                <div style={{ fontSize: 13, color: "var(--ink)", lineHeight: 1.5, marginBottom: 6 }}>
                  <span className="mono" style={{ color: "var(--accent)", fontSize: 11, marginRight: 8 }}>{a.site}</span>
                  {a.detail}
                </div>
                <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", display: "flex", gap: 14, marginTop: 8 }}>
                  <span>conf {Math.round(a.conf * 100)}%</span>
                  <span>· model: {a.model}</span>
                </div>
              </button>
            );
          })}
          {anomalies.length === 0 && (
            <div className="mono" style={{ color: "var(--ink-3)", padding: 20, textAlign: "center" }}>
              ⌁ no anomalies — fleet healthy
            </div>
          )}
        </div>

        {sel && (
          <div style={{ display: "flex", flexDirection: "column", gap: 14, overflowY: "auto" }}>
            <Card pad={16}>
              <div className="mono" style={{ fontSize: 10, color: "var(--accent)", marginBottom: 4 }}>{sel.id.slice(0, 8)}</div>
              <div style={{ fontSize: 15, fontWeight: 600, marginBottom: 10 }}>{KIND_META[sel.kind].label}</div>
              <Row k="Site" v={sel.site} />
              <Row k="Detected" v={`${sel.t} WAT`} />
              <Row k="Severity" v={sel.sev} />
              <Row k="Confidence" v={`${Math.round(sel.conf * 100)}%`} />
              <Row k="Status" v={sel.acknowledged ? "acknowledged" : "open"} last />
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
                      {sel.detail}
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

            {canMutate && !sel.acknowledged && (
              <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                <Btn primary onClick={ack} disabled={busy === sel.id}>
                  {busy === sel.id ? "Acknowledging…" : "Acknowledge"}
                </Btn>
              </div>
            )}
            {toast?.id === sel.id && (
              <div className="mono" style={{
                fontSize: 10.5, color: "var(--accent)",
                padding: "6px 10px", background: "var(--accent-dim)",
                border: "1px solid var(--accent-line)", borderRadius: 5,
                letterSpacing: ".06em",
              }}>⌁ {toast.msg}</div>
            )}
          </div>
        )}
      </div>
    </>
  );
}

function Row({ k, v, last }: { k: string; v: string; last?: boolean }) {
  return (
    <div style={{
      display: "flex", justifyContent: "space-between",
      padding: "8px 0", borderBottom: last ? 0 : "1px solid var(--line)", fontSize: 12,
    }}>
      <span style={{ color: "var(--ink-3)" }}>{k}</span>
      <span className="mono" style={{ textTransform: "capitalize" }}>{v}</span>
    </div>
  );
}
