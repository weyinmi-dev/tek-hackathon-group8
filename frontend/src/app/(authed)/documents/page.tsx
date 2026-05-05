"use client";

import { Fragment, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill, Section } from "@/components/UI";
import { useAuth } from "@/lib/auth";
import { isAdmin, isEngineer, isManager } from "@/lib/rbac";
import { api } from "@/lib/api";
import { downloadSampleTemplates } from "@/lib/sampleTemplates";
import type {
  DocumentListItem,
  DocumentProvider,
  IndexingStatus,
  IngestionRunSummary,
  IngestionStatus,
} from "@/lib/types";

const CATEGORIES = [
  "EngineeringSop",
  "IncidentReport",
  "OutageSummary",
  "NetworkDiagnostic",
  "TowerPerformance",
  "AlertHistory",
];

const STATUS_TONE: Record<IndexingStatus, "ok" | "warn" | "crit" | "info" | "neutral"> = {
  Indexed: "ok",
  Pending: "info",
  InProgress: "warn",
  Failed: "crit",
};

// Network-log file types that route to /api/network/ingest. Anything else
// goes to /api/documents/upload (the RAG corpus).
const LOG_EXTENSIONS = [".csv", ".json", ".jsonl", ".xlsx", ".txt", ".log"];

function looksLikeNetworkLog(fileName: string): boolean {
  const lower = fileName.toLowerCase();
  return LOG_EXTENSIONS.some(ext => lower.endsWith(ext));
}

export default function DocumentsPage() {
  const { user } = useAuth();
  const [docs, setDocs] = useState<DocumentListItem[]>([]);
  const [providers, setProviders] = useState<DocumentProvider[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [uploadOpen, setUploadOpen] = useState(false);
  const [linkOpen, setLinkOpen] = useState(false);
  const [ingestOpen, setIngestOpen] = useState(false);
  // Recent ingestion runs from the current session — the backend doesn't yet
  // expose GET /analytics/ingestion-runs, so this is in-memory only.
  const [runs, setRuns] = useState<RunRecord[]>([]);

  const refresh = async () => {
    try {
      const [d, p] = await Promise.all([api.documents(), api.documentProviders()]);
      setDocs(d);
      setProviders(p);
      setErr(null);
    } catch (e) {
      setErr(String(e));
    }
  };

  useEffect(() => { void refresh(); }, []);

  const indexedCount = useMemo(() => docs.filter(d => d.status === "Indexed").length, [docs]);
  const totalSize = useMemo(() => docs.reduce((s, d) => s + d.sizeBytes, 0), [docs]);

  const onReindex = async (id: string) => {
    try { await api.reindexDocument(id); await refresh(); }
    catch (e) { setErr(String(e)); }
  };
  const onDelete = async (id: string, title: string) => {
    if (!window.confirm(`Delete "${title}"? The chunks and embedding rows go with it.`)) return;
    try { await api.deleteDocument(id); await refresh(); }
    catch (e) { setErr(String(e)); }
  };

  const recordRun = (run: RunRecord) => setRuns(prev => [run, ...prev].slice(0, 8));

  return (
    <>
      <TopBar
        title="Knowledge"
        sub={`${docs.length} docs · ${indexedCount} indexed · ${formatBytes(totalSize)} stored${runs.length ? ` · ${runs.length} pipeline run${runs.length === 1 ? "" : "s"} this session` : ""}`}
        right={(
          <div style={{ display: "flex", gap: 6 }}>
            {isEngineer(user?.role) && (
              <>
                <Btn primary onClick={() => setIngestOpen(true)}>+ Ingest log</Btn>
                <Btn
                  onClick={() => downloadSampleTemplates()}
                  title="Download a CSV + TXT sample with realistic events you can drop into the ingest modal."
                >
                  ↓ Sample template
                </Btn>
              </>
            )}
            {isManager(user?.role) && (
              <>
                <Btn onClick={() => setLinkOpen(true)}>+ Link cloud</Btn>
                <Btn onClick={() => setUploadOpen(true)}>+ Upload doc</Btn>
              </>
            )}
          </div>
        )}
      />
      <div style={{ padding: 22, display: "grid", gridTemplateColumns: "1fr 320px", gap: 14 }}>
        {err && <div className="mono" style={{ color: "var(--crit)", gridColumn: "1 / -1" }}>⚠ {err}</div>}

        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          {runs.length > 0 && (
            <Section label="RECENT PIPELINE RUNS (THIS SESSION)">
              <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                {runs.map(r => <RunSummaryCard key={r.localId} run={r} />)}
              </div>
            </Section>
          )}

          <Section label="DOCUMENTS (RAG CORPUS)">
            <Card pad={0}>
              <div style={{
                padding: "12px 14px", borderBottom: "1px solid var(--line)",
                display: "grid", gridTemplateColumns: "2.4fr .9fr 1fr 1fr 1fr 1fr 100px", gap: 10,
                fontSize: 10, fontFamily: "var(--mono)", color: "var(--ink-3)",
                letterSpacing: ".12em", textTransform: "uppercase",
              }}>
                <span>DOCUMENT</span><span>STATUS</span><span>SOURCE</span><span>CATEGORY</span><span>REGION</span><span>UPLOADED</span><span>ACTIONS</span>
              </div>
              {docs.length === 0 && (
                <div style={{ padding: 20, textAlign: "center", color: "var(--ink-3)", fontSize: 12 }}>
                  No documents yet. {isManager(user?.role) ? "Upload a runbook or link a cloud-stored SOP to get started." : "Ask a manager to upload one."}
                </div>
              )}
              {docs.map((d, i) => (
                <div key={d.id} style={{
                  padding: "12px 14px",
                  borderBottom: i < docs.length - 1 ? "1px solid var(--line)" : 0,
                  display: "grid", gridTemplateColumns: "2.4fr .9fr 1fr 1fr 1fr 1fr 100px", gap: 10,
                  alignItems: "center", fontSize: 12.5, color: "var(--ink)",
                }}>
                  <div>
                    <div style={{ fontWeight: 500 }}>{d.title}</div>
                    <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 1 }}>
                      {d.fileName} · {formatBytes(d.sizeBytes)} · v{d.version}
                    </div>
                    {d.lastIndexError && (
                      <div className="mono" style={{ fontSize: 10, color: "var(--crit)", marginTop: 2 }}>{d.lastIndexError}</div>
                    )}
                  </div>
                  <Pill tone={STATUS_TONE[d.status]} dot>{d.status}</Pill>
                  <span style={{ color: "var(--ink-2)", fontSize: 11 }}>{d.source}</span>
                  <span style={{ color: "var(--ink-2)", fontSize: 11 }}>{d.category}</span>
                  <span style={{ color: "var(--ink-2)", fontSize: 11 }}>{d.region}</span>
                  <span className="mono" style={{ fontSize: 10.5, color: "var(--ink-3)" }}>
                    {new Date(d.uploadedAtUtc).toLocaleDateString()}
                  </span>
                  <div style={{ display: "flex", gap: 4, justifyContent: "flex-end" }}>
                    {isManager(user?.role) && <Btn small onClick={() => onReindex(d.id)}>↻</Btn>}
                    {isAdmin(user?.role) && <Btn small style={{ color: "var(--crit)" }} onClick={() => onDelete(d.id, d.title)}>×</Btn>}
                  </div>
                </div>
              ))}
            </Card>
          </Section>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <Section label="STORAGE PROVIDERS">
            <Card pad={14}>
              {providers.map(p => (
                <div key={p.source} style={{
                  display: "flex", justifyContent: "space-between", alignItems: "center",
                  padding: "8px 0", borderBottom: "1px solid var(--line)",
                  fontSize: 12,
                }}>
                  <span>{p.source}</span>
                  <Pill tone={p.isAvailable ? "ok" : "neutral"} dot>
                    {p.isAvailable ? "connected" : "placeholder"}
                  </Pill>
                </div>
              ))}
              <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 8 }}>
                Cloud providers list as &ldquo;placeholder&rdquo; until an SDK adapter is wired in
                Modules.Ai.Infrastructure → DocumentStorageRegistry. The OneDrive log-fetch flow
                below works today via direct-download share links.
              </div>
            </Card>
          </Section>

          <Section label="AI PIPELINE · NETWORK LOGS">
            <Card pad={14}>
              <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginBottom: 8 }}>
                Log files (.csv / .json / .xlsx / .txt) trigger the 5-stage pipeline.
              </div>
              <Step n="1" label="Parse"     sub="Detect format, extract events" />
              <Step n="2" label="Analyze"   sub="AI anomaly detection" />
              <Step n="3" label="Decide"    sub="Plan alerts + optimizations" />
              <Step n="4" label="Persist"   sub="Stage-4 dedup (create vs update)" />
              <Step n="5" label="Project"   sub="Fan out to dashboards & copilot" last />
              <div style={{ height: 1, background: "var(--line)", margin: "10px 0" }} />
              <div className="mono uppr" style={{ fontSize: 9.5, color: "var(--ink-3)", letterSpacing: ".12em", marginBottom: 6 }}>
                OPERATIONS TRIGGERED
              </div>
              <Trigger label="Network maps"           detail="towers, regions, signal" />
              <Trigger label="Alerts / anomalies"     detail="created or de-duplicated" />
              <Trigger label="Optimizations"          detail="per-tower proposals" />
              <Trigger label="Dashboard insights"     detail="ingestion analytics row" />
              <Trigger label="Copilot knowledge base" detail="event bus → KB indexer" last />
            </Card>
          </Section>

          <Section label="RAG PIPELINE · DOCUMENTS">
            <Card pad={14}>
              <Step n="1" label="Source"    sub="Local upload / Google Drive / OneDrive / SharePoint / Azure Blob" />
              <Step n="2" label="Ingestion" sub="Stream bytes from the storage provider" />
              <Step n="3" label="Extract"   sub="text/markdown today; PDF/Office adapter is pluggable" />
              <Step n="4" label="Chunk"     sub="Recursive splitter (600 chars, 80 overlap)" />
              <Step n="5" label="Embed"     sub="Azure OpenAI text-embedding-3-small (or hashing fallback)" />
              <Step n="6" label="pgvector"  sub="Indexed chunks ready for retrieval" last />
            </Card>
          </Section>
        </div>
      </div>

      {ingestOpen && (
        <IngestLogModal
          onClose={() => setIngestOpen(false)}
          onIngested={(record) => { recordRun(record); }}
        />
      )}
      {uploadOpen && <UploadModal onClose={() => setUploadOpen(false)} onUploaded={async () => { setUploadOpen(false); await refresh(); }} />}
      {linkOpen && <LinkModal providers={providers} onClose={() => setLinkOpen(false)} onLinked={async () => { setLinkOpen(false); await refresh(); }} />}
    </>
  );
}

// ── Right-rail step / trigger primitives ──────────────────────────────────

function Step({ n, label, sub, last }: { n: string; label: string; sub: string; last?: boolean }) {
  return (
    <div style={{ display: "flex", gap: 10, padding: "6px 0", borderBottom: last ? 0 : "1px dashed var(--line)" }}>
      <div className="mono" style={{
        width: 18, height: 18, borderRadius: 4, background: "var(--bg-3)",
        border: "1px solid var(--line-2)", display: "grid", placeItems: "center",
        fontSize: 10, color: "var(--accent)",
      }}>{n}</div>
      <div>
        <div style={{ fontSize: 12, fontWeight: 500 }}>{label}</div>
        <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>{sub}</div>
      </div>
    </div>
  );
}

function Trigger({ label, detail, last }: { label: string; detail: string; last?: boolean }) {
  return (
    <div style={{ display: "flex", gap: 8, padding: "5px 0", borderBottom: last ? 0 : "1px dashed var(--line)" }}>
      <span style={{ color: "var(--accent)", fontFamily: "var(--mono)", fontSize: 11 }}>↳</span>
      <div>
        <div style={{ fontSize: 12, fontWeight: 500 }}>{label}</div>
        <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>{detail}</div>
      </div>
    </div>
  );
}

// ── Run summary card ──────────────────────────────────────────────────────
// Renders one IngestionRunSummary plus the file/source it came from. The
// counts mirror the boxes from the project's "AI ingestion" diagram —
// network maps, alerts, optimizations, dashboards, copilot KB.

type RunRecord = {
  localId: string;
  fileName: string;
  source: "local" | "onedrive";
  summary: IngestionRunSummary;
};

function RunSummaryCard({ run }: { run: RunRecord }) {
  const { summary } = run;
  const dedup = summary.deduplicatedFromPriorRun;
  const failed = summary.finalStatus === "Failed";
  const tone = failed ? "crit" : dedup ? "info" : "ok";
  const headline = failed
    ? "Pipeline failed"
    : dedup
      ? "Duplicate file — returned prior run"
      : "Pipeline completed";

  return (
    <Card pad={14}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 10 }}>
        <div style={{ minWidth: 0 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
            <div style={{ fontSize: 13, fontWeight: 600 }}>{run.fileName}</div>
            <Pill tone={tone} dot>{headline}</Pill>
            {dedup && <Pill tone="warn">deduplicated</Pill>}
            <Pill tone="neutral">{run.source === "onedrive" ? "OneDrive" : "Local upload"}</Pill>
          </div>
          <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 3 }}>
            run {summary.ingestionRunId.slice(0, 8)}… · sha {summary.contentHash.slice(0, 10)}… · status {summary.finalStatus}
          </div>
          {summary.failureReason && (
            <div className="mono" style={{ fontSize: 10, color: "var(--crit)", marginTop: 4 }}>
              ⚠ {summary.failureReason}
            </div>
          )}
        </div>
      </div>

      {!failed && (
        <div style={{
          display: "grid", gridTemplateColumns: "repeat(5, 1fr)", gap: 10, marginTop: 12,
        }}>
          <Stat label="Events parsed"    value={summary.eventsParsed} />
          <Stat label="Anomalies"        value={summary.anomaliesDetected} accent />
          <Stat label="Alerts created"   value={summary.alertsCreated}  href="/alerts" />
          <Stat label="Alerts updated"   value={summary.alertsUpdated}  href="/alerts" />
          <Stat label="Optimizations"    value={summary.optimizationsCreated} href="/optimize" />
        </div>
      )}

      {!failed && (
        <div style={{ display: "flex", gap: 8, marginTop: 12, flexWrap: "wrap" }}>
          <Pill tone={summary.topologyChanged ? "ok" : "neutral"} dot>
            {summary.topologyChanged ? "topology updated" : "topology unchanged"}
          </Pill>
          <Link href="/map" style={{ textDecoration: "none" }}>
            <Pill tone="info">view network map →</Pill>
          </Link>
          <Link href="/insights" style={{ textDecoration: "none" }}>
            <Pill tone="info">dashboard insights →</Pill>
          </Link>
          <Link href="/copilot" style={{ textDecoration: "none" }}>
            <Pill tone="info">copilot KB →</Pill>
          </Link>
        </div>
      )}

      {summary.stageTimings.length > 0 && (
        <details style={{ marginTop: 10 }}>
          <summary className="mono uppr" style={{
            cursor: "pointer", fontSize: 9.5, color: "var(--ink-3)", letterSpacing: ".12em",
          }}>STAGE TIMINGS</summary>
          <div style={{
            marginTop: 6, display: "grid", gridTemplateColumns: "1fr auto auto", gap: 4,
            fontSize: 11, fontFamily: "var(--mono)",
          }}>
            {summary.stageTimings.map((t, i) => {
              const ms = Math.max(0, new Date(t.endedAt).getTime() - new Date(t.startedAt).getTime());
              return (
                <Fragment key={i}>
                  <span style={{ color: t.succeeded ? "var(--ink-2)" : "var(--crit)" }}>{stageLabel(t.stage)}</span>
                  <span style={{ color: "var(--ink-3)" }}>{ms} ms</span>
                  <span style={{ color: t.succeeded ? "var(--ok)" : "var(--crit)" }}>{t.succeeded ? "ok" : "fail"}</span>
                </Fragment>
              );
            })}
          </div>
        </details>
      )}
    </Card>
  );
}

function Stat({ label, value, accent, href }: { label: string; value: number; accent?: boolean; href?: string }) {
  const body = (
    <div style={{
      padding: "8px 10px", border: "1px solid var(--line)", borderRadius: 6,
      background: "var(--bg-2)", cursor: href ? "pointer" : "default",
    }}>
      <div className="mono uppr" style={{ fontSize: 9, color: "var(--ink-3)", letterSpacing: ".12em" }}>{label}</div>
      <div className="mono" style={{
        fontSize: 22, fontWeight: 600, marginTop: 4,
        color: accent ? "var(--accent)" : "var(--ink)",
      }}>{value}</div>
    </div>
  );
  if (href) return <Link href={href} style={{ textDecoration: "none" }}>{body}</Link>;
  return body;
}

function stageLabel(s: IngestionStatus): string {
  return s;
}

// ── Modals ────────────────────────────────────────────────────────────────

function IngestLogModal({
  onClose,
  onIngested,
}: {
  onClose: () => void;
  onIngested: (record: RunRecord) => void;
}) {
  const [tab, setTab] = useState<"file" | "url">("file");
  const fileRef = useRef<HTMLInputElement>(null);
  const [staged, setStaged] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);

  const [url, setUrl] = useState("");
  const [urlFileName, setUrlFileName] = useState("");

  const [busy, setBusy] = useState(false);
  const [stage, setStage] = useState<string>("");
  const [err, setErr] = useState<string | null>(null);
  const [last, setLast] = useState<IngestionRunSummary | null>(null);
  const [lastSource, setLastSource] = useState<"local" | "onedrive">("local");
  const [lastFileName, setLastFileName] = useState("");

  const onPickFile = (f: File | null | undefined) => {
    if (!f) return;
    setErr(null);
    if (!looksLikeNetworkLog(f.name)) {
      setErr(`"${f.name}" doesn't look like a network log (.csv / .json / .xlsx / .txt). It will still be sent — the backend will reject it if the format isn't recognised.`);
    }
    setStaged(f);
  };

  const submitFile = async () => {
    const file = staged ?? fileRef.current?.files?.[0] ?? null;
    if (!file) { setErr("Pick a file first."); return; }
    setBusy(true); setErr(null); setLast(null);
    setStage("Uploading bytes…");
    try {
      const fd = new FormData();
      fd.append("file", file);
      setStage("Running 5-stage pipeline…");
      const summary = await api.network.ingest(fd);
      setLast(summary); setLastSource("local"); setLastFileName(file.name);
      onIngested({ localId: crypto.randomUUID(), fileName: file.name, source: "local", summary });
    } catch (e) {
      setErr(stringifyError(e));
    } finally {
      setBusy(false); setStage("");
    }
  };

  const submitUrl = async () => {
    const trimmed = url.trim();
    if (!trimmed) { setErr("Paste a URL first."); return; }
    setBusy(true); setErr(null); setLast(null);
    const resolvedUrl = normaliseOneDriveShareUrl(trimmed);
    const fileName = urlFileName.trim() || guessFileNameFromUrl(resolvedUrl) || "onedrive-log";
    setStage("Fetching from URL…");
    try {
      const summary = await api.network.ingestFromUrl(resolvedUrl, fileName);
      setLast(summary); setLastSource("onedrive"); setLastFileName(fileName);
      onIngested({ localId: crypto.randomUUID(), fileName, source: "onedrive", summary });
    } catch (e) {
      setErr(stringifyError(e));
    } finally {
      setBusy(false); setStage("");
    }
  };

  return (
    <Modal title="Ingest network log" onClose={onClose} width={560}>
      <div style={{ display: "flex", gap: 4, marginBottom: 12 }}>
        <TabBtn active={tab === "file"} onClick={() => setTab("file")}>Upload file</TabBtn>
        <TabBtn active={tab === "url"} onClick={() => setTab("url")}>Fetch from OneDrive URL</TabBtn>
      </div>

      <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginBottom: 10 }}>
        Triggers <strong style={{ color: "var(--ink-2)" }}>POST /api/network/ingest</strong> →
        Parse · Analyze · Decide · Persist · Project. Re-uploading the same bytes returns the
        prior run (deduplicated).
      </div>

      {tab === "file" && (
        <div style={{ display: "grid", gap: 10 }}>
          <div
            onDragOver={e => { e.preventDefault(); setDragOver(true); }}
            onDragLeave={() => setDragOver(false)}
            onDrop={e => {
              e.preventDefault(); setDragOver(false);
              const f = e.dataTransfer.files?.[0]; if (f) onPickFile(f);
            }}
            onClick={() => fileRef.current?.click()}
            style={{
              border: `1px dashed ${dragOver ? "var(--accent)" : "var(--line-2)"}`,
              background: dragOver ? "var(--accent-dim)" : "var(--bg-2)",
              borderRadius: 8, padding: 22, textAlign: "center", cursor: "pointer",
              transition: "all .15s",
            }}
          >
            <div style={{ fontSize: 13, fontWeight: 500 }}>
              {staged ? staged.name : "Drag & drop a log file here"}
            </div>
            <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 4 }}>
              {staged
                ? `${formatBytes(staged.size)} · ${staged.type || "unknown type"}`
                : "or click to browse · .csv .json .xlsx .txt"}
            </div>
            <input
              ref={fileRef} type="file" hidden
              accept=".csv,.json,.jsonl,.xlsx,.txt,.log,text/csv,application/json,text/plain"
              onChange={e => onPickFile(e.target.files?.[0] ?? null)}
            />
          </div>
        </div>
      )}

      {tab === "url" && (
        <div style={{ display: "grid", gap: 10 }}>
          <Field label="OneDrive direct-download URL (or any HTTPS URL)">
            <input
              style={inputStyle}
              placeholder="https://onedrive.live.com/...&download=1"
              value={url}
              onChange={e => setUrl(e.target.value)}
            />
          </Field>
          <Field label="File name (optional — defaults to a guess from the URL)">
            <input
              style={inputStyle}
              placeholder="region-east.csv"
              value={urlFileName}
              onChange={e => setUrlFileName(e.target.value)}
            />
          </Field>
          <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>
            Tip: for a OneDrive personal share link, append <code>?download=1</code> (or
            <code> &amp;download=1</code>) to force a direct file response. The browser
            fetches the bytes; the backend never sees the URL. If the fetch is blocked
            by CORS, download the file locally and use the &ldquo;Upload file&rdquo; tab.
          </div>
        </div>
      )}

      {busy && (
        <div className="mono" style={{ fontSize: 11, color: "var(--accent)", marginTop: 12 }}>
          ◐ {stage || "Working…"}
        </div>
      )}

      {err && (
        <div className="mono" style={{ fontSize: 11, color: "var(--crit)", marginTop: 12 }}>
          ⚠ {err}
        </div>
      )}

      {last && (
        <div style={{ marginTop: 14 }}>
          <RunSummaryCard run={{ localId: "modal", fileName: lastFileName, source: lastSource, summary: last }} />
        </div>
      )}

      <div style={{ display: "flex", gap: 8, justifyContent: "flex-end", marginTop: 14 }}>
        <Btn type="button" onClick={onClose}>{last ? "Done" : "Cancel"}</Btn>
        {tab === "file" && !last && (
          <Btn type="button" primary disabled={busy} onClick={submitFile}>
            {busy ? "Ingesting…" : "Ingest + run pipeline"}
          </Btn>
        )}
        {tab === "url" && !last && (
          <Btn type="button" primary disabled={busy} onClick={submitUrl}>
            {busy ? "Fetching…" : "Fetch + ingest"}
          </Btn>
        )}
      </div>
    </Modal>
  );
}

function TabBtn({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      style={{
        appearance: "none",
        padding: "6px 12px",
        background: active ? "var(--bg-3)" : "transparent",
        border: "1px solid " + (active ? "var(--line-2)" : "transparent"),
        color: active ? "var(--ink)" : "var(--ink-3)",
        borderRadius: 6, cursor: "pointer",
        fontSize: 12, fontWeight: active ? 600 : 400,
      }}
    >{children}</button>
  );
}

function UploadModal({ onClose, onUploaded }: { onClose: () => void; onUploaded: () => void }) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [form, setForm] = useState({ title: "", category: "EngineeringSop", region: "All regions", tags: "" });
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    const file = fileRef.current?.files?.[0];
    if (!file) { setErr("Pick a file first."); return; }
    setBusy(true); setErr(null);
    const fd = new FormData();
    fd.append("file", file);
    fd.append("title", form.title || file.name);
    fd.append("category", form.category);
    fd.append("region", form.region);
    fd.append("tags", form.tags);
    try { await api.uploadDocument(fd); onUploaded(); }
    catch (e) { setErr(String(e)); }
    finally { setBusy(false); }
  };

  return (
    <Modal title="Upload document" onClose={onClose}>
      <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginBottom: 10 }}>
        For SOPs / runbooks / incident reports. Indexed into the RAG corpus the Copilot
        retrieves against. For network logs, use <strong style={{ color: "var(--ink-2)" }}>+ Ingest log</strong> instead.
      </div>
      <form onSubmit={submit} style={{ display: "grid", gap: 10 }}>
        <Field label="File (text / markdown today)"><input ref={fileRef} type="file" required style={inputStyle} /></Field>
        <Field label="Title (optional)"><input style={inputStyle} value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} /></Field>
        <Field label="Category">
          <select style={inputStyle} value={form.category} onChange={e => setForm({ ...form, category: e.target.value })}>
            {CATEGORIES.map(c => <option key={c} value={c}>{c}</option>)}
          </select>
        </Field>
        <Field label="Region"><input style={inputStyle} value={form.region} onChange={e => setForm({ ...form, region: e.target.value })} /></Field>
        <Field label="Tags (comma separated)"><input style={inputStyle} value={form.tags} onChange={e => setForm({ ...form, tags: e.target.value })} /></Field>
        {err && <div className="mono" style={{ color: "var(--crit)", fontSize: 11 }}>⚠ {err}</div>}
        <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
          <Btn type="button" onClick={onClose}>Cancel</Btn>
          <Btn type="submit" primary disabled={busy}>{busy ? "Uploading…" : "Upload + Index"}</Btn>
        </div>
      </form>
    </Modal>
  );
}

function LinkModal({ providers, onClose, onLinked }: { providers: DocumentProvider[]; onClose: () => void; onLinked: () => void }) {
  const cloudOptions = providers.filter(p => p.source !== "LocalUpload");
  const [form, setForm] = useState({
    title: "", fileName: "", contentType: "text/plain", sizeBytes: 0,
    region: "All regions", tags: "", category: "EngineeringSop",
    source: cloudOptions[0]?.source ?? "GoogleDrive",
    storageKey: "", externalReference: "",
  });
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true); setErr(null);
    try { await api.linkDocument(form); onLinked(); }
    catch (e) { setErr(String(e)); }
    finally { setBusy(false); }
  };
  return (
    <Modal title="Link cloud document" onClose={onClose}>
      <form onSubmit={submit} style={{ display: "grid", gap: 10 }}>
        <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>
          Linking registers the document in the index. Ingestion calls the provider on demand —
          providers shown as &ldquo;placeholder&rdquo; will fail until an SDK adapter is wired up.
        </div>
        <Field label="Source">
          <select style={inputStyle} value={form.source} onChange={e => setForm({ ...form, source: e.target.value as typeof form.source })}>
            {cloudOptions.map(p => (
              <option key={p.source} value={p.source}>
                {p.source} {p.isAvailable ? "" : "(placeholder)"}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Title"><input style={inputStyle} required value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} /></Field>
        <Field label="File name"><input style={inputStyle} required value={form.fileName} onChange={e => setForm({ ...form, fileName: e.target.value })} /></Field>
        <Field label="Storage key (Drive file ID / blob name / SharePoint ID)"><input style={inputStyle} required value={form.storageKey} onChange={e => setForm({ ...form, storageKey: e.target.value })} /></Field>
        <Field label="External URL (optional)"><input style={inputStyle} value={form.externalReference} onChange={e => setForm({ ...form, externalReference: e.target.value })} /></Field>
        <Field label="Category">
          <select style={inputStyle} value={form.category} onChange={e => setForm({ ...form, category: e.target.value })}>
            {CATEGORIES.map(c => <option key={c} value={c}>{c}</option>)}
          </select>
        </Field>
        <Field label="Region"><input style={inputStyle} value={form.region} onChange={e => setForm({ ...form, region: e.target.value })} /></Field>
        <Field label="Tags (comma separated)"><input style={inputStyle} value={form.tags} onChange={e => setForm({ ...form, tags: e.target.value })} /></Field>
        {err && <div className="mono" style={{ color: "var(--crit)", fontSize: 11 }}>⚠ {err}</div>}
        <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
          <Btn type="button" onClick={onClose}>Cancel</Btn>
          <Btn type="submit" primary disabled={busy}>{busy ? "Linking…" : "Link + Index"}</Btn>
        </div>
      </form>
    </Modal>
  );
}

const inputStyle: React.CSSProperties = {
  width: "100%", padding: "8px 10px", borderRadius: 5,
  border: "1px solid var(--line-2)", background: "var(--bg-2)", color: "var(--ink)",
  fontFamily: "var(--mono)", fontSize: 12,
};

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label style={{ display: "grid", gap: 4 }}>
      <span className="mono uppr" style={{ fontSize: 9, color: "var(--ink-3)", letterSpacing: ".12em" }}>{label}</span>
      {children}
    </label>
  );
}

function Modal({ title, children, onClose, width = 460 }: { title: string; children: React.ReactNode; onClose: () => void; width?: number }) {
  return (
    <div onClick={onClose} style={{
      position: "fixed", inset: 0, background: "rgba(0,0,0,.55)",
      display: "grid", placeItems: "center", zIndex: 50,
    }}>
      <div onClick={e => e.stopPropagation()} style={{
        background: "var(--bg-1)", border: "1px solid var(--line-2)", borderRadius: 10,
        width, maxHeight: "90vh", overflowY: "auto",
        padding: 20, boxShadow: "0 20px 60px rgba(0,0,0,.45)",
      }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 14 }}>
          <div style={{ fontSize: 14, fontWeight: 600 }}>{title}</div>
          <button onClick={onClose} style={{ appearance: "none", background: "transparent", border: 0, color: "var(--ink-3)", cursor: "pointer", fontSize: 16 }}>×</button>
        </div>
        {children}
      </div>
    </div>
  );
}

// ── helpers ───────────────────────────────────────────────────────────────

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function stringifyError(e: unknown): string {
  if (e instanceof Error) return e.message;
  return String(e);
}

function guessFileNameFromUrl(url: string): string | null {
  try {
    const u = new URL(url);
    const last = u.pathname.split("/").filter(Boolean).pop();
    if (!last) return null;
    return decodeURIComponent(last);
  } catch {
    return null;
  }
}

// OneDrive personal share links (https://1drv.ms/...) and consumer shares
// (https://onedrive.live.com/...) need a tweak to return raw bytes instead of
// the share-page HTML. The simplest universal trick is appending `download=1`.
// We don't change URLs that already have it, and we leave non-OneDrive URLs alone.
function normaliseOneDriveShareUrl(url: string): string {
  try {
    const u = new URL(url);
    const isOneDrive =
      u.hostname.endsWith("1drv.ms") ||
      u.hostname.endsWith("onedrive.live.com") ||
      u.hostname.endsWith("sharepoint.com");
    if (!isOneDrive) return url;
    if (u.searchParams.has("download")) return url;
    u.searchParams.set("download", "1");
    return u.toString();
  } catch {
    return url;
  }
}

