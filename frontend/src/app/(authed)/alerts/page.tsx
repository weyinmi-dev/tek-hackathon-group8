"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill, Section } from "@/components/UI";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { isManager } from "@/lib/rbac";
import type { Alert } from "@/lib/types";

export default function AlertsPage() {
  const router = useRouter();
  const [filter, setFilter] = useState<"all" | "critical" | "warn" | "info">(
    "all",
  );
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [sel, setSel] = useState<Alert | null>(null);
  const [acking, setAcking] = useState<string | null>(null);
  // Visual-only confirmations for actions that don't have a backend endpoint yet.
  // Stored per-alert so a banner can flash next to the buttons.
  const [actionToast, setActionToast] = useState<{ id: string; msg: string } | null>(null);
  const { user } = useAuth();

  async function load() {
    const r = await api.alerts({
      severity: filter === "all" ? undefined : filter,
    });
    setAlerts(r);
    setSel((prev) =>
      prev ? (r.find((a) => a.id === prev.id) ?? r[0] ?? null) : (r[0] ?? null),
    );
  }
  useEffect(() => {
    load();
  }, [filter]);

  async function ack(id: string) {
    setAcking(id);
    try {
      await api.ackAlert(id);
      await load();
    } finally {
      setAcking(null);
    }
  }

  function flashAction(id: string, msg: string): void {
    setActionToast({ id, msg });
    setTimeout(() => {
      setActionToast((cur) => (cur?.id === id ? null : cur));
    }, 2400);
  }

  // Manager+ only — backend enforces it too. We use prompt() for the team name to
  // keep this minimal; a proper inline form is the future design pass.
  async function assign(a: Alert): Promise<void> {
    const team = window.prompt(`Assign ${a.id} to which NOC team?`, a.assignedTeam ?? "field-team-3")?.trim();
    if (!team) return;
    try {
      await api.assignAlert(a.id, team);
      await load();
      flashAction(a.id, `Assigned to ${team}`);
    } catch (e) {
      flashAction(a.id, e instanceof Error ? e.message : "Assign failed");
    }
  }

  async function dispatchField(a: Alert): Promise<void> {
    const target = window.prompt(
      `Dispatch field team for ${a.id}. Target?`,
      a.dispatchTarget ?? `field-team-3 → ${a.tower}`,
    )?.trim();
    if (!target) return;
    try {
      await api.dispatchAlert(a.id, target);
      await load();
      flashAction(a.id, `Field dispatch logged: ${target}`);
    } catch (e) {
      flashAction(a.id, e instanceof Error ? e.message : "Dispatch failed");
    }
  }

  function openInCopilot(a: Alert): void {
    const q = `Diagnose incident ${a.id} on ${a.tower} in ${a.region}: ${a.title}`;
    router.push(`/copilot?q=${encodeURIComponent(q)}`);
  }

  const counts = {
    all: alerts.length,
    critical: alerts.filter((a) => a.sev === "critical").length,
    warn: alerts.filter((a) => a.sev === "warn").length,
    info: alerts.filter((a) => a.sev === "info").length,
  };

  return (
    <>
      <TopBar
        title="Smart Alerts"
        sub={`${alerts.filter((a) => a.status === "active").length} active · AI-summarized · pattern detection enabled`}
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
                onClick={() => setFilter(k)}
                style={{
                  appearance: "none",
                  border: 0,
                  padding: "5px 12px",
                  borderRadius: 5,
                  fontSize: 11,
                  fontWeight: 500,
                  background: filter === k ? "var(--bg-3)" : "transparent",
                  color: filter === k ? "var(--ink)" : "var(--ink-3)",
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
                  {counts[k]}
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
          {alerts.map((a) => (
            <button
              key={a.id}
              onClick={() => setSel(a)}
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
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
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
                  <span
                    className="mono"
                    style={{ fontSize: 10, color: "var(--ink-3)" }}
                  >
                    {a.id}
                  </span>
                  <span
                    className="mono"
                    style={{ fontSize: 10, color: "var(--ink-3)" }}
                  >
                    · {a.region}
                  </span>
                </div>
                <span
                  className="mono"
                  style={{ fontSize: 10, color: "var(--ink-3)" }}
                >
                  {a.time}
                </span>
              </div>
              <div
                style={{
                  fontSize: 14,
                  fontWeight: 500,
                  marginBottom: 6,
                  color: "var(--ink)",
                }}
              >
                {a.title}
              </div>
              <div
                style={{ fontSize: 12, color: "var(--ink-2)", lineHeight: 1.5 }}
              >
                <span
                  style={{
                    color: "var(--accent)",
                    fontFamily: "var(--mono)",
                    fontSize: 10,
                    marginRight: 6,
                  }}
                >
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
              <div
                className="mono"
                style={{ fontSize: 10.5, color: "var(--ink-3)" }}
              >
                RAISED {sel.time} · {sel.region.toUpperCase()}
              </div>
            </Card>
            <Section label="AI ROOT-CAUSE">
              <Card pad={14}>
                <div
                  style={{
                    fontSize: 12.5,
                    lineHeight: 1.6,
                    color: "var(--ink-2)",
                  }}
                >
                  {sel.cause}
                </div>
              </Card>
            </Section>
            <Section label="IMPACT">
              <Card pad={14}>
                <Row k="Subscribers affected" v={sel.users.toLocaleString()} />
                <Row k="Tower(s)" v={sel.tower} />
                <Row
                  k="Confidence"
                  v={Math.round(sel.confidence * 100) + "%"}
                />
                <Row k="Assigned" v={sel.assignedTeam ?? "—"} />
                <Row k="Field dispatch" v={sel.dispatchTarget ?? "—"} />
                <Row k="Status" v={sel.status} last />
              </Card>
            </Section>
            {user?.role !== "viewer" && (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                  <Btn
                    primary
                    onClick={() => ack(sel.id)}
                    disabled={acking === sel.id || sel.status === "acknowledged"}
                  >
                    {acking === sel.id
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
                {actionToast?.id === sel.id && (
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
                    ⌁ {actionToast.msg}
                  </div>
                )}
              </div>
            )}
          </div>
        )}
      </div>
    </>
  );
}

function Row({ k, v, last }: { k: string; v: string; last?: boolean }) {
  return (
    <div
      style={{
        display: "flex",
        justifyContent: "space-between",
        padding: "8px 0",
        borderBottom: last ? 0 : "1px solid var(--line)",
        fontSize: 12,
      }}
    >
      <span style={{ color: "var(--ink-3)" }}>{k}</span>
      <span className="mono">{v}</span>
    </div>
  );
}
