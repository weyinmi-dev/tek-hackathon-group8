"use client";

import Link from "next/link";
import { Pill } from "./UI";
import type { IngestionRunSummary } from "@/lib/types";

/**
 * The synchronisation report for one upload.
 *
 * Deliberately one component used in two places — inline in the upload modal and on the sync-history
 * detail — so what an operator sees the moment a file lands is byte-for-byte what they see when they
 * come back to it a week later.
 */
export function SyncReport({ run }: { run: IngestionRunSummary }) {
  const failed = run.finalStatus === "Failed";
  const site = run.syncedSites[0];

  const changed =
    run.recordsCreated + run.recordsUpdated + run.recordsArchived > 0;

  return (
    <div style={{ display: "grid", gap: 14 }}>
      {/* ── Outcome ─────────────────────────────────────────────────────── */}
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "center" }}>
        <Pill tone={failed ? "crit" : "ok"} dot={!failed}>
          {failed ? "FAILED" : "SYNCHRONISED"}
        </Pill>

        {run.deduplicatedFromPriorRun && (
          <Pill tone="info">
            ALREADY INGESTED — NO CHANGES
          </Pill>
        )}

        {run.durationMs != null && (
          <Pill tone="neutral">{formatDuration(run.durationMs)}</Pill>
        )}

        {run.warnings.length > 0 && (
          <Pill tone="warn">
            {run.warnings.length} WARNING{run.warnings.length === 1 ? "" : "S"}
          </Pill>
        )}
      </div>

      {failed && run.failureReason && (
        <div
          className="mono"
          style={{
            fontSize: 11,
            lineHeight: 1.6,
            color: "var(--crit)",
            background: "color-mix(in oklch, var(--crit) 8%, transparent)",
            border: "1px solid color-mix(in oklch, var(--crit) 30%, transparent)",
            borderRadius: 8,
            padding: "10px 12px",
          }}
        >
          {run.failureReason}
        </div>
      )}

      {/* ── What changed ────────────────────────────────────────────────── */}
      {!failed && (
        <div>
          <Label>Records</Label>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(88px, 1fr))",
              gap: 8,
              marginTop: 8,
            }}
          >
            <Stat label="Created" value={run.recordsCreated} tone="ok" />
            <Stat label="Updated" value={run.recordsUpdated} tone="info" />
            <Stat label="Archived" value={run.recordsArchived} tone="warn" />
            <Stat label="Telemetry" value={run.telemetryRowsAppended} tone="neutral" />
            <Stat label="Alerts" value={run.alertsCreated} tone={run.alertsCreated > 0 ? "crit" : "neutral"} />
            <Stat label="Optimizations" value={run.optimizationsCreated} tone="accent" />
          </div>

          {!changed && !run.deduplicatedFromPriorRun && (
            <p
              className="mono"
              style={{ fontSize: 10, color: "var(--ink-4)", marginTop: 8, lineHeight: 1.6 }}
            >
              Nothing changed — the reported state already matches what is stored.
            </p>
          )}
        </div>
      )}

      {/* ── Warnings ────────────────────────────────────────────────────── */}
      {run.warnings.length > 0 && (
        <div>
          <Label>Warnings</Label>
          <ul
            className="mono"
            style={{
              margin: "8px 0 0",
              padding: "0 0 0 16px",
              fontSize: 11,
              lineHeight: 1.7,
              color: "var(--warn)",
            }}
          >
            {run.warnings.map((w) => (
              <li key={w}>{w}</li>
            ))}
          </ul>
        </div>
      )}

      {/* ── Provenance ──────────────────────────────────────────────────── */}
      {site && (
        <div>
          <Label>Source</Label>
          <div style={{ display: "grid", gap: 6, marginTop: 8 }}>
            <Row label="Site">
              <Link
                href={`/sites/${site.siteCode}`}
                style={{ color: "var(--accent)", textDecoration: "none" }}
              >
                {site.siteCode} — {site.siteName}
              </Link>
            </Row>
            <Row label="Provider">{site.provider}</Row>
            <Row label="Environment">{site.environment}</Row>
            <Row label="Region">{site.region}</Row>
            {site.vendor && <Row label="Vendor">{site.vendor}</Row>}
            <Row label="Technologies">{site.technologies.split(",").join(" · ")}</Row>
            <Row label="Snapshot">v{site.snapshotVersion}</Row>
            <Row label="Request ID">{site.requestId}</Row>
            {run.syncedSites.length > 1 && (
              <Row label="Also synced">
                {run.syncedSites
                  .slice(1)
                  .map((s) => s.siteCode)
                  .join(", ")}
              </Row>
            )}
          </div>
        </div>
      )}

      {/* ── Stage timings ───────────────────────────────────────────────── */}
      {run.stageTimings.length > 0 && (
        <details>
          <summary
            className="mono uppr"
            style={{
              fontSize: 9,
              letterSpacing: ".16em",
              color: "var(--ink-4)",
              cursor: "pointer",
            }}
          >
            Pipeline stages
          </summary>
          <div style={{ display: "grid", gap: 4, marginTop: 8 }}>
            {run.stageTimings.map((t) => {
              const ms =
                new Date(t.endedAt).getTime() - new Date(t.startedAt).getTime();
              return (
                <div
                  key={t.stage}
                  className="mono"
                  style={{
                    display: "grid",
                    gridTemplateColumns: "1fr auto auto",
                    gap: 10,
                    fontSize: 10,
                    color: "var(--ink-3)",
                    padding: "4px 0",
                    borderBottom: "1px solid var(--line)",
                  }}
                >
                  <span>{t.stage}</span>
                  <span style={{ color: t.succeeded ? "var(--ok)" : "var(--crit)" }}>
                    {t.succeeded ? "OK" : "FAIL"}
                  </span>
                  <span style={{ color: "var(--ink-4)" }}>{ms}ms</span>
                </div>
              );
            })}
          </div>
        </details>
      )}
    </div>
  );
}

function Stat({
  label,
  value,
  tone,
}: {
  label: string;
  value: number;
  tone: "ok" | "warn" | "crit" | "info" | "neutral" | "accent";
}) {
  const color =
    tone === "neutral" ? "var(--ink-2)" : `var(--${tone === "accent" ? "accent" : tone})`;

  return (
    <div
      style={{
        border: "1px solid var(--line)",
        borderRadius: 8,
        padding: "10px 12px",
        background: "var(--bg-2)",
      }}
    >
      <div
        style={{
          fontSize: 20,
          fontWeight: 600,
          color: value > 0 ? color : "var(--ink-4)",
          lineHeight: 1.1,
        }}
      >
        {value}
      </div>
      <div
        className="mono uppr"
        style={{ fontSize: 9, letterSpacing: ".12em", color: "var(--ink-4)", marginTop: 4 }}
      >
        {label}
      </div>
    </div>
  );
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div
      className="mono"
      style={{
        display: "grid",
        gridTemplateColumns: "120px 1fr",
        gap: 10,
        fontSize: 11,
        color: "var(--ink-2)",
      }}
    >
      <span className="uppr" style={{ fontSize: 9, letterSpacing: ".12em", color: "var(--ink-4)" }}>
        {label}
      </span>
      <span style={{ wordBreak: "break-word" }}>{children}</span>
    </div>
  );
}

export function formatDuration(ms: number): string {
  if (ms < 1000) return `${Math.round(ms)}ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)}s`;
  return `${Math.floor(ms / 60_000)}m ${Math.round((ms % 60_000) / 1000)}s`;
}

/** The mono uppercase micro-label the rest of the app uses for section headings. */
function Label({ children }: { children: React.ReactNode }) {
  return (
    <div
      className="mono uppr"
      style={{ fontSize: 9, letterSpacing: ".16em", color: "var(--ink-4)" }}
    >
      {children}
    </div>
  );
}
