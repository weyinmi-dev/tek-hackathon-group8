"use client";

import Link from "next/link";
import { useState } from "react";
import { Pill } from "./UI";
import type { IngestionRunSummary, SyncAction, SyncChange } from "@/lib/types";

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
            <Stat
              label="Anomalies"
              value={run.changes.filter((c) => c.entityType === "Anomaly" && c.action === "Created").length}
              tone="crit"
            />
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

          {run.changes.length > 0 && <ChangeTable changes={run.changes} />}
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

/**
 * The itemised record of what the upload touched, collapsed by default.
 *
 * Collapsed because the counts answer the usual question ("did it work?") and this answers the
 * unusual one ("what exactly did it do to my data?"). Expanding it should feel like opening the
 * ledger, not like the page vomiting rows at you.
 */
function ChangeTable({ changes }: { changes: SyncChange[] }) {
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState<SyncAction | "all">("all");

  const counts = {
    Created: changes.filter((c) => c.action === "Created").length,
    Updated: changes.filter((c) => c.action === "Updated").length,
    Archived: changes.filter((c) => c.action === "Archived").length,
  };

  const visible = filter === "all" ? changes : changes.filter((c) => c.action === filter);

  // Grouped by entity type so the reader scans "what kind of thing", not a flat 30-row wall.
  const grouped = visible.reduce<Record<string, SyncChange[]>>((acc, c) => {
    (acc[c.entityType] ??= []).push(c);
    return acc;
  }, {});

  return (
    <div style={{ marginTop: 12 }}>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="mono uppr"
        style={{
          display: "flex",
          alignItems: "center",
          gap: 8,
          width: "100%",
          padding: "8px 10px",
          background: "var(--bg-2)",
          border: "1px solid var(--line)",
          borderRadius: 8,
          color: "var(--ink-2)",
          fontSize: 9,
          letterSpacing: ".14em",
          cursor: "pointer",
        }}
      >
        <span style={{ color: "var(--accent)", width: 10 }}>{open ? "▾" : "▸"}</span>
        <span>{changes.length} record{changes.length === 1 ? "" : "s"} changed</span>
        <span style={{ flex: 1 }} />
        <span style={{ color: "var(--ink-4)", letterSpacing: 0 }}>
          {open ? "Hide" : "View"}
        </span>
      </button>

      {open && (
        <div
          style={{
            border: "1px solid var(--line)",
            borderTop: "none",
            borderRadius: "0 0 8px 8px",
            overflow: "hidden",
          }}
        >
          {/* Filter by what happened — during an incident the only rows that matter are the
              archived ones, and hunting them out of thirty creates is a waste of the operator. */}
          <div
            style={{
              display: "flex",
              gap: 4,
              padding: "8px 10px",
              borderBottom: "1px solid var(--line)",
              background: "var(--bg-2)",
            }}
          >
            <FilterChip on={filter === "all"} onClick={() => setFilter("all")} tone="neutral">
              All {changes.length}
            </FilterChip>
            {counts.Created > 0 && (
              <FilterChip on={filter === "Created"} onClick={() => setFilter("Created")} tone="ok">
                Created {counts.Created}
              </FilterChip>
            )}
            {counts.Updated > 0 && (
              <FilterChip on={filter === "Updated"} onClick={() => setFilter("Updated")} tone="info">
                Updated {counts.Updated}
              </FilterChip>
            )}
            {counts.Archived > 0 && (
              <FilterChip on={filter === "Archived"} onClick={() => setFilter("Archived")} tone="warn">
                Archived {counts.Archived}
              </FilterChip>
            )}
          </div>

          <div style={{ maxHeight: 340, overflowY: "auto" }}>
            {Object.entries(grouped).map(([type, rows]) => (
              <div key={type}>
                <div
                  className="mono uppr"
                  style={{
                    padding: "7px 10px",
                    fontSize: 8,
                    letterSpacing: ".16em",
                    color: "var(--ink-4)",
                    background: "var(--bg-2)",
                    borderBottom: "1px solid var(--line)",
                    position: "sticky",
                    top: 0,
                  }}
                >
                  {type} ({rows.length})
                </div>

                {rows.map((c, i) => (
                  <div
                    key={`${c.entityType}-${c.entityKey}-${i}`}
                    className="mono"
                    style={{
                      display: "grid",
                      gridTemplateColumns: "80px 130px 1fr",
                      gap: 10,
                      alignItems: "baseline",
                      padding: "8px 10px",
                      fontSize: 10.5,
                      borderBottom: "1px solid var(--line)",
                      borderLeft: `2px solid ${actionColor(c.action)}`,
                    }}
                  >
                    <span
                      className="uppr"
                      style={{ fontSize: 8, letterSpacing: ".12em", color: actionColor(c.action) }}
                    >
                      {actionLabel(c.action)}
                    </span>
                    <span style={{ color: "var(--ink)", wordBreak: "break-all" }}>{c.entityKey}</span>
                    <span style={{ color: "var(--ink-3)", lineHeight: 1.5 }}>{c.detail ?? "—"}</span>
                  </div>
                ))}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function FilterChip({
  on,
  onClick,
  tone,
  children,
}: {
  on: boolean;
  onClick: () => void;
  tone: "ok" | "info" | "warn" | "neutral";
  children: React.ReactNode;
}) {
  const color = tone === "neutral" ? "var(--ink-3)" : `var(--${tone})`;
  return (
    <button
      type="button"
      onClick={onClick}
      className="mono uppr"
      style={{
        padding: "3px 8px",
        fontSize: 8,
        letterSpacing: ".12em",
        borderRadius: 5,
        cursor: "pointer",
        border: `1px solid ${on ? color : "var(--line)"}`,
        background: on ? `color-mix(in oklch, ${color} 14%, transparent)` : "transparent",
        color: on ? color : "var(--ink-4)",
      }}
    >
      {children}
    </button>
  );
}

function actionLabel(action: SyncAction): string {
  return action;
}

function actionColor(action: SyncAction): string {
  return action === "Created" ? "var(--ok)" : action === "Updated" ? "var(--info)" : "var(--warn)";
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
