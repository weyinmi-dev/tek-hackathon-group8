"use client";

import { observer } from "mobx-react-lite";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";
import { Card, Pill } from "@/components/UI";
import { SyncReport, formatDuration } from "@/components/SyncReport";
import { TopBar } from "@/components/TopBar";
import { useSyncStore } from "@/lib/stores/StoreProvider";
import type { IngestionRunSummary } from "@/lib/types";

type Tab = "runs" | "files";

/**
 * Synchronisation history.
 *
 * Two tabs over one record set, not two pages over two: an upload and the file it came from are the
 * same event seen from different angles, and splitting them into separate routes would mean two
 * stores, two fetches, and two chances to disagree about what happened.
 */
function SyncPage() {
  const sync = useSyncStore();
  const [tab, setTab] = useState<Tab>("runs");

  // Notifications deep-link here as /sync?run=<id>. Honour it so clicking "Synchronised LAG0456"
  // opens that upload's report rather than dumping the operator at the top of the list.
  const params = useSearchParams();
  const runParam = params.get("run");

  useEffect(() => {
    void sync.loadRuns();
  }, [sync]);

  // Re-fetch when an upload lands in this tab. `version` is bumped by SyncStore.recordUpload.
  useEffect(() => {
    if (sync.version > 0) void sync.loadRuns();
  }, [sync, sync.version]);

  useEffect(() => {
    if (runParam) void sync.selectById(runParam);
  }, [sync, runParam]);

  const selected = sync.selectedRun;

  return (
    <>
      <TopBar
        title="Synchronization"
        sub="Every upload, what it changed, and the file it came from"
        right={
          <Pill tone="neutral">
            {sync.total} RUN{sync.total === 1 ? "" : "S"}
          </Pill>
        }
      />

      <div style={{ padding: 20, display: "grid", gap: 16 }}>
        <Card pad={0}>
          {/* ── Tabs + search ────────────────────────────────────────────── */}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              gap: 12,
              padding: "12px 16px",
              borderBottom: "1px solid var(--line)",
              flexWrap: "wrap",
            }}
          >
            <div style={{ display: "flex", gap: 4 }}>
              <TabBtn on={tab === "runs"} onClick={() => setTab("runs")}>
                Runs
              </TabBtn>
              <TabBtn on={tab === "files"} onClick={() => setTab("files")}>
                Files
              </TabBtn>
            </div>

            <input
              value={sync.search}
              onChange={(e) => {
                sync.setSearch(e.target.value);
                void sync.loadRuns();
              }}
              placeholder="Search site, provider, file…"
              className="mono"
              style={{
                background: "var(--bg-2)",
                border: "1px solid var(--line)",
                borderRadius: 8,
                color: "var(--ink)",
                padding: "7px 10px",
                fontSize: 11,
                minWidth: 240,
                outline: "none",
              }}
            />
          </div>

          {sync.loading && sync.runs.length === 0 ? (
            <Empty>Loading…</Empty>
          ) : sync.error ? (
            <Empty tone="crit">{sync.error}</Empty>
          ) : sync.runs.length === 0 ? (
            <Empty>
              No uploads yet. Upload a Site Snapshot from the Knowledge &amp; Logs page.
            </Empty>
          ) : tab === "runs" ? (
            <RunsTable
              runs={sync.runs}
              selectedId={sync.selectedRunId}
              onSelect={(id) => sync.select(id === sync.selectedRunId ? null : id)}
            />
          ) : (
            <FilesTable runs={sync.runs} />
          )}
        </Card>

        {/* ── Detail ───────────────────────────────────────────────────── */}
        {selected && (
          <Card>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                marginBottom: 14,
              }}
            >
              <div className="mono uppr" style={{ fontSize: 9, letterSpacing: ".16em", color: "var(--ink-4)" }}>Sync report — {selected.fileName ?? "upload"}</div>
              <button
                type="button"
                onClick={() => sync.select(null)}
                className="mono uppr"
                style={{
                  background: "none",
                  border: "1px solid var(--line)",
                  borderRadius: 6,
                  color: "var(--ink-3)",
                  padding: "4px 9px",
                  fontSize: 9,
                  letterSpacing: ".12em",
                  cursor: "pointer",
                }}
              >
                Close
              </button>
            </div>
            <SyncReport run={selected} />
          </Card>
        )}
      </div>
    </>
  );
}

function RunsTable({
  runs,
  selectedId,
  onSelect,
}: {
  runs: IngestionRunSummary[];
  selectedId: string | null;
  onSelect: (id: string) => void;
}) {
  const cols = "150px 1fr 110px 90px 90px 1fr";

  return (
    <div>
      <HeaderRow cols={cols}>
        <span>Uploaded</span>
        <span>Site / Provider</span>
        <span>Status</span>
        <span>Duration</span>
        <span>Version</span>
        <span>Changes</span>
      </HeaderRow>

      {runs.map((run) => {
        const site = run.syncedSites[0];
        const failed = run.finalStatus === "Failed";
        const on = run.ingestionRunId === selectedId;

        return (
          <button
            key={run.ingestionRunId}
            type="button"
            onClick={() => onSelect(run.ingestionRunId)}
            className="mono"
            style={{
              display: "grid",
              gridTemplateColumns: cols,
              gap: 10,
              width: "100%",
              textAlign: "left",
              alignItems: "center",
              padding: "11px 16px",
              fontSize: 11,
              color: "var(--ink-2)",
              background: on ? "var(--accent-dim)" : "transparent",
              border: "none",
              borderBottom: "1px solid var(--line)",
              borderLeft: `2px solid ${on ? "var(--accent)" : "transparent"}`,
              cursor: "pointer",
            }}
          >
            <span style={{ color: "var(--ink-3)" }}>{formatWhen(run.startedAt)}</span>

            <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
              {site ? (
                <>
                  <span style={{ color: "var(--ink)" }}>{site.siteCode}</span>
                  <span style={{ color: "var(--ink-4)" }}> · {site.provider}</span>
                </>
              ) : (
                <span style={{ color: "var(--ink-4)" }}>{run.fileName ?? "log upload"}</span>
              )}
            </span>

            <span>
              <Pill tone={failed ? "crit" : run.warnings.length > 0 ? "warn" : "ok"}>
                {failed ? "FAILED" : run.warnings.length > 0 ? "WARNINGS" : "OK"}
              </Pill>
            </span>

            <span style={{ color: "var(--ink-3)" }}>
              {run.durationMs != null ? formatDuration(run.durationMs) : "—"}
            </span>

            <span style={{ color: "var(--ink-4)" }}>
              {site ? `v${site.snapshotVersion}` : "—"}
            </span>

            <Changes run={run} />
          </button>
        );
      })}
    </div>
  );
}

function Changes({ run }: { run: IngestionRunSummary }) {
  if (run.finalStatus === "Failed") {
    return <span style={{ color: "var(--crit)" }}>—</span>;
  }

  if (run.deduplicatedFromPriorRun) {
    return <span style={{ color: "var(--ink-4)" }}>duplicate — no changes</span>;
  }

  const bits: string[] = [];
  if (run.recordsCreated) bits.push(`+${run.recordsCreated}`);
  if (run.recordsUpdated) bits.push(`~${run.recordsUpdated}`);
  if (run.recordsArchived) bits.push(`−${run.recordsArchived}`);

  if (bits.length === 0) {
    return <span style={{ color: "var(--ink-4)" }}>no changes</span>;
  }

  return (
    <span style={{ display: "flex", gap: 10 }}>
      {run.recordsCreated > 0 && (
        <span style={{ color: "var(--ok)" }}>+{run.recordsCreated} created</span>
      )}
      {run.recordsUpdated > 0 && (
        <span style={{ color: "var(--info)" }}>~{run.recordsUpdated} updated</span>
      )}
      {run.recordsArchived > 0 && (
        <span style={{ color: "var(--warn)" }}>−{run.recordsArchived} archived</span>
      )}
    </span>
  );
}

function FilesTable({ runs }: { runs: IngestionRunSummary[] }) {
  const cols = "1fr 120px 130px 120px 150px 90px";

  return (
    <div>
      <HeaderRow cols={cols}>
        <span>File</span>
        <span>Site</span>
        <span>Provider</span>
        <span>Uploaded by</span>
        <span>Uploaded</span>
        <span>Type</span>
      </HeaderRow>

      {runs.map((run) => {
        const site = run.syncedSites[0];
        return (
          <div
            key={run.ingestionRunId}
            className="mono"
            style={{
              display: "grid",
              gridTemplateColumns: cols,
              gap: 10,
              alignItems: "center",
              padding: "11px 16px",
              fontSize: 11,
              color: "var(--ink-2)",
              borderBottom: "1px solid var(--line)",
            }}
          >
            <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
              {run.fileName ?? "—"}
            </span>

            <span>
              {site ? (
                <Link
                  href={`/sites/${site.siteCode}`}
                  style={{ color: "var(--accent)", textDecoration: "none" }}
                >
                  {site.siteCode}
                </Link>
              ) : (
                <span style={{ color: "var(--ink-4)" }}>—</span>
              )}
            </span>

            <span style={{ color: "var(--ink-3)" }}>{site?.provider ?? "—"}</span>
            <span style={{ color: "var(--ink-3)" }}>{run.submittedBy ?? "—"}</span>
            <span style={{ color: "var(--ink-4)" }}>{formatWhen(run.startedAt)}</span>

            <span>
              <Pill tone={site ? "accent" : "neutral"}>
                {site ? "SNAPSHOT" : "LOG"}
              </Pill>
            </span>
          </div>
        );
      })}
    </div>
  );
}

function HeaderRow({ cols, children }: { cols: string; children: React.ReactNode }) {
  return (
    <div
      className="mono uppr"
      style={{
        display: "grid",
        gridTemplateColumns: cols,
        gap: 10,
        padding: "9px 16px",
        fontSize: 9,
        letterSpacing: ".14em",
        color: "var(--ink-4)",
        borderBottom: "1px solid var(--line)",
        background: "var(--bg-2)",
      }}
    >
      {children}
    </div>
  );
}

function TabBtn({
  on,
  onClick,
  children,
}: {
  on: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="mono uppr"
      style={{
        padding: "6px 12px",
        fontSize: 10,
        letterSpacing: ".14em",
        borderRadius: 7,
        cursor: "pointer",
        border: `1px solid ${on ? "var(--accent-line)" : "var(--line)"}`,
        background: on ? "var(--accent-dim)" : "transparent",
        color: on ? "var(--accent)" : "var(--ink-3)",
      }}
    >
      {children}
    </button>
  );
}

function Empty({
  children,
  tone = "neutral",
}: {
  children: React.ReactNode;
  tone?: "neutral" | "crit";
}) {
  return (
    <div
      className="mono"
      style={{
        padding: 40,
        textAlign: "center",
        fontSize: 11,
        color: tone === "crit" ? "var(--crit)" : "var(--ink-4)",
      }}
    >
      {children}
    </div>
  );
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

const ObservedSyncPage = observer(SyncPage);

/**
 * useSearchParams() opts a route into client-side rendering, which Next requires be wrapped in a
 * Suspense boundary — without it the build fails rather than degrading.
 */
export default function SyncPageRoute() {
  return (
    <Suspense fallback={<Empty>Loading…</Empty>}>
      <ObservedSyncPage />
    </Suspense>
  );
}
