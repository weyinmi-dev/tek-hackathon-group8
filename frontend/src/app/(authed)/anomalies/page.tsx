"use client";

import { useEffect } from "react";
import { observer } from "mobx-react-lite";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill, Section } from "@/components/UI";
import { GeoBadge } from "@/components/GeoBadge";
import { PredFaultMini, SiteDieselChart } from "@/components/EnergyCharts";
import { useAuth } from "@/lib/auth";
import { isEngineer } from "@/lib/rbac";
import { useAnomaliesStore } from "@/lib/stores/StoreProvider";
import type { EnergyAnomalyDto } from "@/lib/types";

type AnomalyKind = EnergyAnomalyDto["kind"];

const KIND_META: Record<AnomalyKind, { label: string; icon: string; tone: "ok" | "warn" | "crit" | "info" }> = {
  "fuel-theft":      { label: "Fuel theft",            icon: "⛽", tone: "crit" },
  "sensor-offline":  { label: "Sensor offline",        icon: "⌁", tone: "warn" },
  "gen-overuse":     { label: "Generator overuse",     icon: "⚙", tone: "warn" },
  "battery-degrade": { label: "Battery degradation",   icon: "▥", tone: "info" },
  "predicted-fault": { label: "Predicted fault",       icon: "⌖", tone: "warn" },
};

const AnomaliesPage = observer(function AnomaliesPage() {
  const store = useAnomaliesStore();
  const { user } = useAuth();
  const canMutate = isEngineer(user?.role);

  useEffect(() => {
    void store.load();
    const id = setInterval(() => void store.load(), 30_000);
    return () => clearInterval(id);
  }, [store]);

  const sel = store.selected;
  const anomalies = store.anomalies;

  return (
    <>
      <TopBar
        title="Anomaly Detection"
        sub="Isolation Forest + statistical models · fuel theft · battery degradation · gen misuse"
        right={
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <Pill tone="crit" dot>{anomalies.filter((a) => a.sev === "critical").length} CRITICAL</Pill>
            <Pill tone="warn" dot>{anomalies.filter((a) => a.sev === "warn").length} WARN</Pill>
            <Pill tone="info">{anomalies.filter(a => a.acknowledged).length} ACKED</Pill>
            <button
              onClick={() => store.toggleShowAcknowledged()}
              style={{
                appearance: "none", border: "1px solid var(--line)", borderRadius: 5,
                padding: "5px 10px", fontSize: 11, cursor: "pointer",
                background: store.showAcknowledged ? "var(--bg-3)" : "transparent",
                color: store.showAcknowledged ? "var(--ink)" : "var(--ink-3)",
              }}
            >
              {store.showAcknowledged ? "Hide acked" : "Show acked"}
            </button>
          </div>
        }
      />
      <div style={{
        padding: 22, display: "grid", gridTemplateColumns: "1fr 380px",
        gap: 14, height: "calc(100vh - 67px)",
      }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 10, overflowY: "auto", paddingRight: 4 }}>
          {store.visible.map((a) => {
            const meta = KIND_META[a.kind];
            const active = sel?.id === a.id;
            return (
              <button key={a.id} onClick={() => store.setSelected(a.id)} style={{
                appearance: "none", textAlign: "left", cursor: "pointer",
                background: active ? "var(--bg-2)" : "var(--bg-1)",
                borderTop: "1px solid " + (active ? "var(--accent-line)" : "var(--line)"),
                borderRight: "1px solid " + (active ? "var(--accent-line)" : "var(--line)"),
                borderBottom: "1px solid " + (active ? "var(--accent-line)" : "var(--line)"),
                borderRadius: 8, padding: 14,
                borderLeft: `3px solid ${a.sev === "critical" ? "var(--crit)" : a.sev === "warn" ? "var(--warn)" : "var(--info)"}`,
                opacity: a.acknowledged ? 0.55 : 1,
              }}>
                <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8, flexWrap: "wrap" }}>
                  <span style={{ fontSize: 16 }}>{meta.icon}</span>
                  <Pill tone={meta.tone} dot>{meta.label}</Pill>
                  <span className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>{a.id.slice(0, 8)}</span>
                  {a.acknowledged && <Pill tone="ok">ACKED</Pill>}
                  {/* Compact OSM context — region pill + fuel distance. The directive's
                      example flow ("fuel drop at remote site → high theft probability")
                      becomes visually obvious when the badge is right next to the kind. */}
                  <GeoBadge geo={a.geo} compact />
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
          {/* Three explicit states — without an error banner, an API 500 (e.g. OSM
              cold-cache timing out at the proxy) was indistinguishable from a real
              "no anomalies" empty state, which made the symptom undiagnosable. */}
          {store.error && (
            <div
              className="mono"
              style={{
                margin: "0 4px 4px 0",
                padding: "10px 12px",
                background: "color-mix(in oklch, var(--crit) 10%, transparent)",
                border: "1px solid color-mix(in oklch, var(--crit) 35%, transparent)",
                borderRadius: 6,
                color: "var(--crit)",
                fontSize: 11.5,
                lineHeight: 1.5,
              }}
            >
              ⚠ Failed to load anomalies: {store.error}
              <div style={{ color: "var(--ink-3)", marginTop: 4, fontSize: 10.5 }}>
                Check the browser console + the Web.Api logs. If this is the first
                request after a backend restart, OSM cold-cache lookups can take a
                moment — try again in a few seconds.
              </div>
            </div>
          )}
          {!store.error && store.loading && store.anomalies.length === 0 && (
            <div className="mono" style={{ color: "var(--ink-3)", padding: 14, fontSize: 11.5, textAlign: "center" }}>
              ⌁ Loading anomalies…
            </div>
          )}
          {!store.error && !store.loading && store.visible.length === 0 && (
            <div className="mono" style={{ color: "var(--ink-3)", padding: 20, textAlign: "center" }}>
              ⌁ no anomalies {store.showAcknowledged ? "" : "open"} — fleet healthy
            </div>
          )}
        </div>

        {sel && (
          <div style={{ display: "flex", flexDirection: "column", gap: 14, overflowY: "auto" }}>
            <Card pad={16}>
              <div className="mono" style={{ fontSize: 10, color: "var(--accent)", marginBottom: 4 }}>{sel.id.slice(0, 8)}</div>
              <div style={{ fontSize: 15, fontWeight: 600, marginBottom: 10 }}>{KIND_META[sel.kind].label}</div>
              {sel.geo && (
                <div style={{ marginBottom: 10 }}>
                  <GeoBadge geo={sel.geo} />
                </div>
              )}
              <Row k="Site" v={sel.site} />
              <Row k="Detected" v={`${sel.t} WAT`} />
              <Row k="Severity" v={sel.sev} />
              <Row k="Confidence" v={`${Math.round(sel.conf * 100)}%`} />
              <Row k="Status" v={sel.acknowledged ? "acknowledged" : "open"} last />
            </Card>

            {sel.geo && (
              <Section label="OSM GEO CONTEXT">
                <Card pad={14}>
                  <Row k="Region type" v={sel.geo.regionType} />
                  <Row k="Accessibility" v={`${Math.round(sel.geo.accessibilityScore)} / 100`} />
                  <Row
                    k="Nearest fuel"
                    v={
                      sel.geo.nearestFuelStationMetres != null
                        ? `${(sel.geo.nearestFuelStationMetres / 1000).toFixed(1)} km${sel.geo.nearestFuelStationName ? ` · ${sel.geo.nearestFuelStationName}` : ""}`
                        : "—"
                    }
                    last
                  />
                </Card>
              </Section>
            )}

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
                <Btn primary onClick={() => void store.acknowledge(sel.id)} disabled={store.busy === sel.id}>
                  {store.busy === sel.id ? "Acknowledging…" : "Acknowledge"}
                </Btn>
              </div>
            )}
            {store.toast && (
              <div className="mono" style={{
                fontSize: 10.5, color: "var(--accent)",
                padding: "6px 10px", background: "var(--accent-dim)",
                border: "1px solid var(--accent-line)", borderRadius: 5,
                letterSpacing: ".06em",
              }}>⌁ {store.toast}</div>
            )}
          </div>
        )}
      </div>
    </>
  );
});

export default AnomaliesPage;

function Row({ k, v, last }: { k: string; v: string; last?: boolean }) {
  return (
    <div style={{
      display: "flex", justifyContent: "space-between",
      padding: "8px 0", borderBottom: last ? 0 : "1px solid var(--line)", fontSize: 12,
      gap: 12,
    }}>
      <span style={{ color: "var(--ink-3)" }}>{k}</span>
      <span className="mono" style={{ textTransform: "capitalize", textAlign: "right" }}>{v}</span>
    </div>
  );
}
