"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { observer } from "mobx-react-lite";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill, Section } from "@/components/UI";
import { GeoBadge } from "@/components/GeoBadge";
import { useAuth } from "@/lib/auth";
import { isManager } from "@/lib/rbac";
import { useAlertsStore } from "@/lib/stores/StoreProvider";
import type { Alert } from "@/lib/types";

const AlertsPage = observer(function AlertsPage() {
  const router = useRouter();
  const store = useAlertsStore();
  const { user } = useAuth();

  // Re-fetch whenever the persisted filter changes. The store decides which alert
  // to keep selected (preserves prior selection when still present, falls back to
  // the first one) so the detail panel never goes blank between filter flips.
  useEffect(() => { void store.load(); }, [store, store.filter]);

  async function assign(a: Alert): Promise<void> {
    const team = window.prompt(`Assign ${a.id} to which NOC team?`, a.assignedTeam ?? "field-team-3")?.trim();
    if (!team) return;
    try { await store.assign(a.id, team); }
    catch (e) { store.flashAction(a.id, e instanceof Error ? e.message : "Assign failed"); }
  }

  async function dispatchField(a: Alert): Promise<void> {
    const target = window.prompt(
      `Dispatch field team for ${a.id}. Target?`,
      a.dispatchTarget ?? `field-team-3 → ${a.tower}`,
    )?.trim();
    if (!target) return;
    try { await store.dispatch(a.id, target); }
    catch (e) { store.flashAction(a.id, e instanceof Error ? e.message : "Dispatch failed"); }
  }

  function openInCopilot(a: Alert): void {
    const q = `Diagnose incident ${a.id} on ${a.tower} in ${a.region}: ${a.title}`;
    router.push(`/copilot?q=${encodeURIComponent(q)}`);
  }

  const sel = store.selected;

  return (
    <>
      <TopBar
        title="Smart Alerts"
        sub={`${store.alerts.filter((a) => a.status === "active").length} active · AI-summarized · pattern detection enabled`}
        right={
          <div
            style={{
              display: "flex",
              gap: 6,
              padding: 3,
              background: "var(--bg-1)",
              border: "1px solid var(--line)",
              borderRadius: 7,
            }}
          >
            {(["all", "critical", "warn", "info"] as const).map((k) => (
              <button
                key={k}
                onClick={() => store.setFilter(k)}
                style={{
                  appearance: "none",
                  border: 0,
                  padding: "5px 12px",
                  borderRadius: 5,
                  fontSize: 11,
                  fontWeight: 500,
                  background: store.filter === k ? "var(--bg-3)" : "transparent",
                  color: store.filter === k ? "var(--ink)" : "var(--ink-3)",
                  cursor: "pointer",
                  display: "flex",
                  alignItems: "center",
                  gap: 6,
                }}
              >
                {k.charAt(0).toUpperCase() + k.slice(1)}
                <span
                  className="mono"
                  style={{ fontSize: 9.5, color: "var(--ink-3)" }}
                >
                  {store.counts[k]}
                </span>
              </button>
            ))}
          </div>
        }
      />
      <div
        style={{
          padding: 22,
          display: "grid",
          gridTemplateColumns: "1fr 380px",
          gap: 14,
          height: "calc(100vh - 67px)",
        }}
      >
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: 10,
            overflowY: "auto",
            paddingRight: 4,
          }}
        >
          {/* Three explicit states — without these, fetch failure and "empty fleet"
              both render as a blank scroll area and are indistinguishable. */}
          {store.error && (
            <div
              className="mono"
              style={{
                padding: "10px 12px",
                background: "color-mix(in oklch, var(--crit) 10%, transparent)",
                border: "1px solid color-mix(in oklch, var(--crit) 35%, transparent)",
                borderRadius: 6,
                color: "var(--crit)",
                fontSize: 11.5,
                lineHeight: 1.5,
              }}
            >
              ⚠ Failed to load alerts: {store.error}
              <div style={{ color: "var(--ink-3)", marginTop: 4, fontSize: 10.5 }}>
                Check the browser console + the Web.Api logs. If you just changed the
                backend, make sure the Web.Api process has been restarted.
              </div>
            </div>
          )}
          {!store.error && store.loading && store.alerts.length === 0 && (
            <div className="mono" style={{ color: "var(--ink-3)", padding: 14, fontSize: 11.5 }}>
              ⌁ Loading alerts…
            </div>
          )}
          {!store.error && !store.loading && store.alerts.length === 0 && (
            <div className="mono" style={{ color: "var(--ink-3)", padding: 14, fontSize: 11.5 }}>
              ⌁ No alerts match the current filter ({store.filter}).
            </div>
          )}
          {store.alerts.map((a) => (
            <button
              key={a.id}
              onClick={() => store.setSelected(a.id)}
              style={{
                appearance: "none",
                textAlign: "left",
                cursor: "pointer",
                background: sel?.id === a.id ? "var(--bg-2)" : "var(--bg-1)",
                border:
                  "1px solid " +
                  (sel?.id === a.id ? "var(--accent-line)" : "var(--line)"),
                borderRadius: 8,
                padding: 14,
                position: "relative",
                borderLeft: `3px solid ${a.sev === "critical" ? "var(--crit)" : a.sev === "warn" ? "var(--warn)" : "var(--info)"}`,
              }}
            >
              <div
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  marginBottom: 8,
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                  <Pill
                    tone={
                      a.sev === "critical"
                        ? "crit"
                        : a.sev === "warn"
                          ? "warn"
                          : "info"
                    }
                    dot
                  >
                    {a.sev}
                  </Pill>
                  <span className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>{a.id}</span>
                  <span className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>· {a.region}</span>
                  {/* OSM-derived geo context: region type + accessibility + nearest fuel station.
                      Compact (region pill + fuel) keeps the list row dense; the detail panel
                      shows the full breakdown including the accessibility score. */}
                  <GeoBadge geo={a.geo} compact />
                </div>
                <span className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>{a.time}</span>
              </div>
              <div style={{ fontSize: 14, fontWeight: 500, marginBottom: 6, color: "var(--ink)" }}>
                {a.title}
              </div>
              <div style={{ fontSize: 12, color: "var(--ink-2)", lineHeight: 1.5 }}>
                <span style={{ color: "var(--accent)", fontFamily: "var(--mono)", fontSize: 10, marginRight: 6 }}>
                  AI
                </span>
                {a.cause}
              </div>
              <div
                style={{
                  display: "flex",
                  gap: 14,
                  marginTop: 10,
                  fontSize: 10.5,
                  color: "var(--ink-3)",
                  fontFamily: "var(--mono)",
                }}
              >
                <span>
                  👥{" "}
                  {a.users > 0
                    ? `${a.users.toLocaleString()} affected`
                    : "no users impacted"}
                </span>
                <span>· {a.tower}</span>
                <span style={{ marginLeft: "auto", color: "var(--ink-2)" }}>
                  conf {Math.round(a.confidence * 100)}%
                </span>
              </div>
            </button>
          ))}
        </div>
        {sel && (
          <div
            style={{
              display: "flex",
              flexDirection: "column",
              gap: 14,
              overflowY: "auto",
            }}
          >
            <Card pad={16}>
              <div style={{ marginBottom: 10 }}>
                <Pill
                  tone={
                    sel.sev === "critical"
                      ? "crit"
                      : sel.sev === "warn"
                        ? "warn"
                        : "info"
                  }
                  dot
                >
                  {sel.id}
                </Pill>
              </div>
              <div style={{ fontSize: 16, fontWeight: 600, marginBottom: 6 }}>
                {sel.title}
              </div>
              <div className="mono" style={{ fontSize: 10.5, color: "var(--ink-3)" }}>
                RAISED {sel.time} · {sel.region.toUpperCase()}
              </div>
              {sel.geo && (
                <div style={{ marginTop: 10 }}>
                  <GeoBadge geo={sel.geo} />
                </div>
              )}
            </Card>
            <Section label="AI ROOT-CAUSE">
              <Card pad={14}>
                <div style={{ fontSize: 12.5, lineHeight: 1.6, color: "var(--ink-2)" }}>
                  {sel.cause}
                </div>
              </Card>
            </Section>
            <Section label="IMPACT">
              <Card pad={14}>
                <Row k="Subscribers affected" v={sel.users.toLocaleString()} />
                <Row k="Tower(s)" v={sel.tower} />
                <Row k="Confidence" v={Math.round(sel.confidence * 100) + "%"} />
                <Row k="Assigned" v={sel.assignedTeam ?? "—"} />
                <Row k="Field dispatch" v={sel.dispatchTarget ?? "—"} />
                <Row k="Status" v={sel.status} last />
              </Card>
            </Section>
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
                  />
                  <Row
                    k="Coordinates"
                    v={`${sel.geo.latitude.toFixed(4)}, ${sel.geo.longitude.toFixed(4)}`}
                    last={!sel.geo.address}
                  />
                  {sel.geo.address && <Row k="Address" v={sel.geo.address} last />}
                </Card>
              </Section>
            )}
            {user?.role !== "viewer" && (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                  <Btn
                    primary
                    onClick={() => store.ack(sel.id)}
                    disabled={store.acking === sel.id || sel.status === "acknowledged"}
                  >
                    {store.acking === sel.id
                      ? "Acknowledging…"
                      : sel.status === "acknowledged"
                        ? "Acknowledged"
                        : "Acknowledge"}
                  </Btn>
                  {isManager(user?.role) && (
                    <Btn onClick={() => assign(sel)}>
                      {sel.assignedTeam ? "Reassign" : "Assign"}
                    </Btn>
                  )}
                  <Btn onClick={() => dispatchField(sel)}>
                    {sel.dispatchTarget ? "Re-dispatch" : "Dispatch field"}
                  </Btn>
                  <Btn ghost onClick={() => openInCopilot(sel)}>
                    Open in Copilot →
                  </Btn>
                </div>
                {store.actionToast?.id === sel.id && (
                  <div
                    className="mono"
                    style={{
                      fontSize: 10.5,
                      color: "var(--accent)",
                      padding: "6px 10px",
                      background: "var(--accent-dim)",
                      border: "1px solid var(--accent-line)",
                      borderRadius: 5,
                      letterSpacing: ".06em",
                    }}
                  >
                    ⌁ {store.actionToast.msg}
                  </div>
                )}
              </div>
            )}
          </div>
        )}
      </div>
    </>
  );
});

export default AlertsPage;

function Row({ k, v, last }: { k: string; v: string; last?: boolean }) {
  return (
    <div
      style={{
        display: "flex",
        justifyContent: "space-between",
        padding: "8px 0",
        borderBottom: last ? 0 : "1px solid var(--line)",
        fontSize: 12,
        gap: 12,
      }}
    >
      <span style={{ color: "var(--ink-3)" }}>{k}</span>
      <span className="mono" style={{ textAlign: "right", overflow: "hidden", textOverflow: "ellipsis" }}>{v}</span>
    </div>
  );
}
