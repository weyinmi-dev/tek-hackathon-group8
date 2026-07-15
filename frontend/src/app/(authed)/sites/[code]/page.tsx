"use client";

import { observer } from "mobx-react-lite";
import { use, useCallback, useEffect, useState } from "react";
import { Card, Pill } from "@/components/UI";
import { TopBar } from "@/components/TopBar";
import { RangePicker, TrendChart } from "@/components/TrendChart";
import { api } from "@/lib/api";
import { useSyncStore } from "@/lib/stores/StoreProvider";
import type { SiteDetail, SiteTelemetry } from "@/lib/types";

/**
 * Site Details — the latest synchronised state of one site, plus its reported history.
 *
 * Always renders the most recent snapshot: it re-fetches whenever the sync store's `version` bumps,
 * so an upload performed anywhere in the app leaves this page correct without a manual reload.
 */
function SiteDetailPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = use(params);
  const sync = useSyncStore();

  const [site, setSite] = useState<SiteDetail | null>(null);
  const [telemetry, setTelemetry] = useState<SiteTelemetry | null>(null);
  const [hours, setHours] = useState(24);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setError(null);
    // allSettled, not all: a telemetry failure must not blank the site's current state, which is
    // the half an operator most needs during an incident.
    const [detail, series] = await Promise.allSettled([
      api.network.siteDetail(code),
      api.network.siteTelemetry(code, hours),
    ]);

    if (detail.status === "fulfilled") setSite(detail.value);
    else setError(detail.reason instanceof Error ? detail.reason.message : String(detail.reason));

    if (series.status === "fulfilled") setTelemetry(series.value);

    setLoading(false);
  }, [code, hours]);

  useEffect(() => {
    void load();
  }, [load]);

  // An upload landed — this site's state may be stale. Re-fetch.
  useEffect(() => {
    if (sync.version > 0) void load();
  }, [sync.version, load]);

  if (loading) {
    return (
      <>
        <TopBar title={code} sub="Site details" />
        <Empty>Loading…</Empty>
      </>
    );
  }

  if (error || !site) {
    return (
      <>
        <TopBar title={code} sub="Site details" />
        <Empty tone="crit">{error ?? "Site not found."}</Empty>
      </>
    );
  }

  const env = site.environmental;
  const perf = site.performance;
  const tone = statusTone(site.statusWire);

  const points = telemetry?.points ?? [];
  const timestamps = points.map((p) => p.at);

  return (
    <>
      <TopBar
        title={site.siteCode}
        sub={`${site.name} · ${site.region}`}
        right={
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <Pill tone={tone} dot={tone !== "ok"}>
              {site.statusWire}
            </Pill>
            {site.healthScore != null && (
              <Pill tone={healthTone(site.healthScore)}>HEALTH {site.healthScore}</Pill>
            )}
          </div>
        }
      />

      <div style={{ padding: 20, display: "grid", gap: 16 }}>
        {/* ── Provenance ─────────────────────────────────────────────────── */}
        <Card>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))",
              gap: 14,
            }}
          >
            <Fact label="Provider" value={site.provider ?? "—"} />
            <Fact label="Vendor" value={site.vendor ?? "—"} />
            <Fact
              label="Technologies"
              value={site.technologies.length > 0 ? site.technologies.join(" · ") : "—"}
            />
            <Fact label="Last synchronised" value={formatWhen(site.lastSynchronisedAt)} />
            <Fact label="Last heartbeat" value={formatWhen(site.lastHeartbeat)} />
            <Fact label="Environment" value={site.environment ?? "—"} />
          </div>

          {!site.provider && (
            <p
              className="mono"
              style={{ fontSize: 10, color: "var(--ink-4)", marginTop: 12, lineHeight: 1.6 }}
            >
              This site has never received an OSS snapshot — the fields above populate on the first
              Site Snapshot upload.
            </p>
          )}
        </Card>

        {/* ── Live state ─────────────────────────────────────────────────── */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
            gap: 16,
          }}
        >
          <Card>
            <Label>Radio</Label>
            <div style={{ display: "grid", gap: 8, marginTop: 10 }}>
              <Metric label="Signal" value={`${site.signalPct}%`} />
              <Metric label="Load" value={`${site.loadPct}%`} />
              {perf?.latencyMs != null && <Metric label="Latency" value={`${perf.latencyMs} ms`} />}
              {perf?.availabilityPercent != null && (
                <Metric label="Availability" value={`${perf.availabilityPercent}%`} />
              )}
              {perf?.connectedUsers != null && (
                <Metric label="Connected users" value={String(perf.connectedUsers)} />
              )}
              {perf?.packetLossPercent != null && (
                <Metric label="Packet loss" value={`${perf.packetLossPercent}%`} />
              )}
            </div>
          </Card>

          <Card>
            <Label>Environment</Label>
            <div style={{ display: "grid", gap: 8, marginTop: 10 }}>
              {env?.temperature != null && (
                <Metric
                  label="Temperature"
                  value={`${env.temperature}°C`}
                  tone={env.temperature > 35 ? "warn" : undefined}
                />
              )}
              {env?.humidity != null && <Metric label="Humidity" value={`${env.humidity}%`} />}
              {env?.batteryVoltage != null && (
                <Metric label="Battery" value={`${env.batteryVoltage} V`} />
              )}
              {env?.generatorFuelPercent != null && (
                <Metric
                  label="Generator fuel"
                  value={`${env.generatorFuelPercent}%`}
                  tone={env.generatorFuelPercent < 25 ? "crit" : undefined}
                />
              )}
              {env?.mainPowerAvailable != null && (
                <Metric
                  label="Grid power"
                  value={env.mainPowerAvailable ? "Available" : "DOWN"}
                  tone={env.mainPowerAvailable ? undefined : "crit"}
                />
              )}
              {env?.generatorRunning != null && (
                <Metric label="Generator" value={env.generatorRunning ? "Running" : "Idle"} />
              )}
              {!env && <Muted>No environmental readings reported.</Muted>}
            </div>
          </Card>

          <Card>
            <Label>Traffic</Label>
            <div style={{ display: "grid", gap: 8, marginTop: 10 }}>
              {perf?.downlinkTrafficGb != null && (
                <Metric label="Downlink" value={`${perf.downlinkTrafficGb} GB`} />
              )}
              {perf?.uplinkTrafficGb != null && (
                <Metric label="Uplink" value={`${perf.uplinkTrafficGb} GB`} />
              )}
              {perf?.callDropRate != null && (
                <Metric label="Call drop rate" value={`${perf.callDropRate}%`} />
              )}
              {perf?.handoverSuccessRate != null && (
                <Metric label="Handover success" value={`${perf.handoverSuccessRate}%`} />
              )}
              {perf?.kpis.map((k) => (
                <Metric key={k.name} label={k.name} value={`${k.value}${k.unit ? ` ${k.unit}` : ""}`} />
              ))}
              {!perf && <Muted>No performance metrics reported.</Muted>}
            </div>
          </Card>
        </div>

        {/* ── Active alarms ──────────────────────────────────────────────── */}
        <Card>
          <Label>Active alarms</Label>
          {site.activeAlarms.length === 0 ? (
            <Muted style={{ marginTop: 10 }}>No alarms reported. The site is clear.</Muted>
          ) : (
            <div style={{ display: "grid", gap: 8, marginTop: 10 }}>
              {site.activeAlarms.map((a) => (
                <div
                  key={a.alarmId}
                  style={{
                    display: "grid",
                    gridTemplateColumns: "auto 1fr auto",
                    gap: 12,
                    alignItems: "center",
                    padding: "10px 12px",
                    borderRadius: 8,
                    border: "1px solid var(--line)",
                    background: "var(--bg-2)",
                  }}
                >
                  <Pill tone={severityTone(a.severity)} dot>
                    {a.severity}
                  </Pill>
                  <div>
                    <div className="mono" style={{ fontSize: 12, color: "var(--ink)" }}>
                      {a.type ?? a.category ?? a.alarmId}
                    </div>
                    {a.description && (
                      <div
                        className="mono"
                        style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 3 }}
                      >
                        {a.description}
                      </div>
                    )}
                  </div>
                  <div
                    className="mono"
                    style={{ fontSize: 10, color: "var(--ink-4)", textAlign: "right" }}
                  >
                    {a.alarmId}
                    <br />
                    {formatWhen(a.raisedAt)}
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>

        {/* ── Trends ─────────────────────────────────────────────────────── */}
        <Card>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: 14,
            }}
          >
            <Label>Historical telemetry</Label>
            <RangePicker hours={hours} onChange={setHours} />
          </div>

          {points.length < 2 ? (
            <Muted>
              Not enough history yet — trends appear once this site has reported more than one
              snapshot in the selected range.
            </Muted>
          ) : (
            <div style={{ display: "grid", gap: 22 }}>
              <TrendGroup title="Health & signal">
                <TrendChart
                  timestamps={timestamps}
                  domain={[0, 100]}
                  series={[
                    { key: "health", label: "Health score", color: "var(--ok)", values: points.map((p) => p.healthScore) },
                    { key: "signal", label: "Signal %", color: "var(--info)", values: points.map((p) => p.signalPct) },
                    { key: "load", label: "Load %", color: "var(--accent)", values: points.map((p) => p.loadPct) },
                  ]}
                />
              </TrendGroup>

              <TrendGroup title="Energy">
                <TrendChart
                  timestamps={timestamps}
                  domain={[0, 100]}
                  series={[
                    { key: "batt", label: "Battery %", color: "var(--ok)", values: points.map((p) => p.batteryPct) },
                    { key: "diesel", label: "Generator fuel %", color: "var(--warn)", values: points.map((p) => p.dieselPct) },
                  ]}
                />
              </TrendGroup>

              <TrendGroup title="Temperature">
                <TrendChart
                  timestamps={timestamps}
                  series={[
                    { key: "temp", label: "Temperature °C", color: "var(--crit)", values: points.map((p) => p.temperatureC) },
                  ]}
                />
              </TrendGroup>

              <TrendGroup title="Traffic & latency">
                <TrendChart
                  timestamps={timestamps}
                  series={[
                    { key: "dl", label: "Downlink GB", color: "var(--info)", values: points.map((p) => p.downlinkTrafficGb) },
                    { key: "users", label: "Connected users", color: "var(--accent)", values: points.map((p) => p.connectedUsers) },
                    { key: "lat", label: "Latency ms", color: "var(--warn)", values: points.map((p) => p.latencyMs) },
                  ]}
                />
              </TrendGroup>

              <TrendGroup title="KPIs">
                <TrendChart
                  timestamps={timestamps}
                  series={[
                    { key: "rsrp", label: "RSRP dBm", color: "var(--info)", values: points.map((p) => p.rsrp) },
                    { key: "sinr", label: "SINR dB", color: "var(--ok)", values: points.map((p) => p.sinr) },
                    { key: "prb", label: "PRB util %", color: "var(--accent)", values: points.map((p) => p.prbUtilization) },
                  ]}
                />
              </TrendGroup>
            </div>
          )}
        </Card>

        {/* ── Equipment + maintenance ────────────────────────────────────── */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))",
            gap: 16,
          }}
        >
          <Card>
            <Label>Equipment</Label>
            {site.equipment.length === 0 ? (
              <Muted style={{ marginTop: 10 }}>No equipment reported.</Muted>
            ) : (
              <div style={{ display: "grid", gap: 6, marginTop: 10 }}>
                {site.equipment.map((e) => (
                  <div
                    key={e.equipmentId}
                    className="mono"
                    style={{
                      display: "grid",
                      gridTemplateColumns: "1fr auto",
                      gap: 10,
                      alignItems: "center",
                      padding: "8px 0",
                      borderBottom: "1px solid var(--line)",
                      fontSize: 11,
                      opacity: e.isActive ? 1 : 0.5,
                    }}
                  >
                    <span>
                      <span style={{ color: "var(--ink)" }}>{e.equipmentId}</span>
                      <span style={{ color: "var(--ink-4)" }}> · {e.type}</span>
                      {e.model && <span style={{ color: "var(--ink-4)" }}> · {e.model}</span>}
                    </span>
                    <Pill tone={e.isActive ? "ok" : "neutral"}>
                      {e.isActive ? (e.status ?? "ACTIVE") : "RETIRED"}
                    </Pill>
                  </div>
                ))}
              </div>
            )}
          </Card>

          <Card>
            <Label>Maintenance</Label>
            <div style={{ display: "grid", gap: 8, marginTop: 10 }}>
              <Metric label="Last service" value={site.lastMaintenanceDate ?? "—"} />
              <Metric label="Next scheduled" value={site.nextScheduledMaintenance ?? "—"} />
            </div>

            {site.tickets.length > 0 && (
              <div style={{ display: "grid", gap: 6, marginTop: 14 }}>
                {site.tickets.map((t) => (
                  <div
                    key={t.ticketId}
                    className="mono"
                    style={{
                      display: "grid",
                      gridTemplateColumns: "1fr auto",
                      gap: 10,
                      alignItems: "center",
                      padding: "8px 0",
                      borderBottom: "1px solid var(--line)",
                      fontSize: 11,
                    }}
                  >
                    <span>
                      <span style={{ color: "var(--ink)" }}>{t.ticketId}</span>
                      {t.issue && <span style={{ color: "var(--ink-4)" }}> · {t.issue}</span>}
                      {t.engineerName && (
                        <span style={{ color: "var(--ink-4)" }}> · {t.engineerName}</span>
                      )}
                    </span>
                    <Pill tone={ticketTone(t.status)}>{t.status.toUpperCase()}</Pill>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </div>
      </div>
    </>
  );
}

function TrendGroup({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <div
        className="mono uppr"
        style={{ fontSize: 9, letterSpacing: ".14em", color: "var(--ink-3)", marginBottom: 10 }}
      >
        {title}
      </div>
      {children}
    </div>
  );
}

function Label({ children }: { children: React.ReactNode }) {
  return (
    <div className="mono uppr" style={{ fontSize: 9, letterSpacing: ".16em", color: "var(--ink-4)" }}>
      {children}
    </div>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <Label>{label}</Label>
      <div className="mono" style={{ fontSize: 12, color: "var(--ink)", marginTop: 5 }}>
        {value}
      </div>
    </div>
  );
}

function Metric({
  label,
  value,
  tone,
}: {
  label: string;
  value: string;
  tone?: "warn" | "crit";
}) {
  return (
    <div
      className="mono"
      style={{
        display: "flex",
        justifyContent: "space-between",
        fontSize: 11,
        color: "var(--ink-3)",
      }}
    >
      <span>{label}</span>
      <span style={{ color: tone ? `var(--${tone})` : "var(--ink)" }}>{value}</span>
    </div>
  );
}

function Muted({
  children,
  style,
}: {
  children: React.ReactNode;
  style?: React.CSSProperties;
}) {
  return (
    <p className="mono" style={{ fontSize: 10, color: "var(--ink-4)", lineHeight: 1.6, ...style }}>
      {children}
    </p>
  );
}

function Empty({ children, tone }: { children: React.ReactNode; tone?: "crit" }) {
  return (
    <div
      className="mono"
      style={{
        padding: 60,
        textAlign: "center",
        fontSize: 12,
        color: tone === "crit" ? "var(--crit)" : "var(--ink-4)",
      }}
    >
      {children}
    </div>
  );
}

function statusTone(status: string): "ok" | "warn" | "crit" {
  const s = status.toUpperCase();
  if (s === "CRITICAL") return "crit";
  if (s === "WARN") return "warn";
  return "ok";
}

function healthTone(score: number): "ok" | "warn" | "crit" {
  if (score < 50) return "crit";
  if (score < 80) return "warn";
  return "ok";
}

function severityTone(severity: string): "ok" | "warn" | "crit" | "info" {
  const s = severity.toUpperCase();
  if (s === "CRITICAL") return "crit";
  if (s === "MAJOR" || s === "MINOR" || s === "WARNING") return "warn";
  return "info";
}

function ticketTone(status: string): "ok" | "warn" | "neutral" {
  if (status === "Open") return "warn";
  if (status === "Completed") return "ok";
  return "neutral";
}

function formatWhen(iso: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export default observer(SiteDetailPage);
