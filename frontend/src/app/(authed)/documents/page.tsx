"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill, Section } from "@/components/UI";
import { useAuth } from "@/lib/auth";
import { isAdmin, isManager } from "@/lib/rbac";
import { api } from "@/lib/api";
import type { DocumentListItem, DocumentProvider, IndexingStatus } from "@/lib/types";

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

export default function DocumentsPage() {
  const { user } = useAuth();
  const [docs, setDocs] = useState<DocumentListItem[]>([]);
  const [providers, setProviders] = useState<DocumentProvider[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [uploadOpen, setUploadOpen] = useState(false);
  const [linkOpen, setLinkOpen] = useState(false);

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

  return (
    <>
      <TopBar
        title="Knowledge"
        sub={`${docs.length} documents · ${indexedCount} indexed · ${formatBytes(totalSize)} stored`}
        right={isManager(user?.role) ? (
          <div style={{ display: "flex", gap: 6 }}>
            <Btn onClick={() => setLinkOpen(true)}>+ Link cloud</Btn>
            <Btn primary onClick={() => setUploadOpen(true)}>+ Upload</Btn>
          </div>
        ) : undefined}
      />
      <div style={{ padding: 22, display: "grid", gridTemplateColumns: "1fr 320px", gap: 14 }}>
        {err && <div className="mono" style={{ color: "var(--crit)", gridColumn: "1 / -1" }}>⚠ {err}</div>}

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
                Modules.Ai.Infrastructure → DocumentStorageRegistry.
              </div>
            </Card>
          </Section>

          <Section label="PIPELINE">
            <Card pad={14}>
              <Step n="1" label="Source" sub="Local upload / Google Drive / OneDrive / SharePoint / Azure Blob" />
              <Step n="2" label="Ingestion" sub="Stream bytes from the storage provider" />
              <Step n="3" label="Extract" sub="text/markdown today; PDF/Office adapter is pluggable" />
              <Step n="4" label="Chunk" sub="Recursive splitter (600 chars, 80 overlap)" />
              <Step n="5" label="Embed" sub="Azure OpenAI text-embedding-3-small (or hashing fallback)" />
              <Step n="6" label="pgvector" sub="Indexed chunks ready for retrieval" last />
            </Card>
          </Section>
        </div>
      </div>

      {uploadOpen && <UploadModal onClose={() => setUploadOpen(false)} onUploaded={async () => { setUploadOpen(false); await refresh(); }} />}
      {linkOpen && <LinkModal providers={providers} onClose={() => setLinkOpen(false)} onLinked={async () => { setLinkOpen(false); await refresh(); }} />}
    </>
  );
}

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

function Modal({ title, children, onClose }: { title: string; children: React.ReactNode; onClose: () => void }) {
  return (
    <div onClick={onClose} style={{
      position: "fixed", inset: 0, background: "rgba(0,0,0,.55)",
      display: "grid", placeItems: "center", zIndex: 50,
    }}>
      <div onClick={e => e.stopPropagation()} style={{
        background: "var(--bg-1)", border: "1px solid var(--line-2)", borderRadius: 10,
        width: 460, padding: 20, boxShadow: "0 20px 60px rgba(0,0,0,.45)",
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

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
