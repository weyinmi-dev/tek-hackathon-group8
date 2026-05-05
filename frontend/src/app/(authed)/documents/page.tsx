"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill, Section } from "@/components/UI";
import { useAuth } from "@/lib/auth";
import { isAdmin, isManager, isEngineer } from "@/lib/rbac";
import { api } from "@/lib/api";
import type { DocumentListItem, DocumentProvider, IndexingStatus } from "@/lib/types";

const CATEGORIES = [
  "EngineeringSop",
  "IncidentReport",
  "OutageSummary",
  "NetworkDiagnostic",
  "TowerPerformance",
  "AlertHistory",
  "EnergySiteSnapshot",
  "EnergyAnomaly",
];

const STATUS_TONE: Record<IndexingStatus, "ok" | "warn" | "crit" | "info" | "neutral"> = {
  Indexed: "ok",
  Pending: "info",
  InProgress: "warn",
  Failed: "crit",
  Rejected: "neutral",
};

export default function DocumentsPage() {
  const { user } = useAuth();
  const [docs, setDocs] = useState<DocumentListItem[]>([]);
  const [providers, setProviders] = useState<DocumentProvider[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [uploadOpen, setUploadOpen] = useState(false);
  const [linkOpen, setLinkOpen] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [deleteDoc, setDeleteDoc] = useState<DocumentListItem | null>(null);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");

  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedSearch(searchTerm);
      if (page !== 1) setPage(1);
    }, 300);
    return () => clearTimeout(handler);
  }, [searchTerm]);

  const refresh = async () => {
    try {
      const [d, p] = await Promise.all([api.documents(page, 10, debouncedSearch), api.documentProviders()]);
      setDocs(d.items);
      setTotal(d.totalCount);
      setProviders(p);
      setErr(null);
    } catch (e) {
      setErr(String(e));
    }
  };

  useEffect(() => { void refresh(); }, [page, debouncedSearch]);

  const indexedCount = useMemo(() => docs.filter(d => d.status === "Indexed").length, [docs]);
  const totalSize = useMemo(() => docs.reduce((s, d) => s + d.sizeBytes, 0), [docs]);

  const onReindex = async (id: string) => {
    try { await api.reindexDocument(id); await refresh(); }
    catch (e) { setErr(String(e)); }
  };
  const onDeleteClick = (d: DocumentListItem) => {
    setDeleteDoc(d);
  };
  const onSync = async () => {
    setSyncing(true);
    try { await api.syncDocuments(); await refresh(); }
    catch (e) { setErr(String(e)); }
    finally { setSyncing(false); }
  };

  return (
    <>
      <TopBar
        title="Knowledge"
        sub={`${total} documents · ${indexedCount} indexed · ${formatBytes(totalSize)} stored (current page)`}
        right={isManager(user?.role) ? (
          <div style={{ display: "flex", gap: 6 }}>
            {isAdmin(user?.role) && <Btn onClick={() => onSync()} disabled={syncing}>{syncing ? "Syncing…" : "↻ Sync All"}</Btn>}
            <Btn onClick={() => setLinkOpen(true)}>+ Link cloud</Btn>
            <Btn primary onClick={() => setUploadOpen(true)}>+ Upload (AI Verified)</Btn>
          </div>
        ) : undefined}
      />
      <div style={{ padding: 22, display: "grid", gridTemplateColumns: "1fr 320px", gap: 14 }}>
        {err && <div className="mono" style={{ color: "var(--crit)", gridColumn: "1 / -1" }}>⚠ {err}</div>}

        <Card pad={0}>
          <div style={{ padding: "12px 14px", borderBottom: "1px solid var(--line)", background: "var(--bg-2)", borderTopLeftRadius: 6, borderTopRightRadius: 6 }}>
            <input 
              style={{ width: "100%", maxWidth: 300, padding: "6px 10px", borderRadius: 4, border: "1px solid var(--line-2)", background: "var(--bg-1)", color: "var(--ink)", fontSize: 12, fontFamily: "var(--mono)" }} 
              placeholder="Search by title or filename..." 
              value={searchTerm} 
              onChange={e => setSearchTerm(e.target.value)} 
            />
          </div>
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
                {d.rejectionReason && (
                  <div className="mono" style={{ fontSize: 10, color: "var(--accent)", marginTop: 2, borderLeft: "2px solid var(--accent)", paddingLeft: 6 }}>
                    <b>REJECTED:</b> {d.rejectionReason}
                  </div>
                )}
              </div>
              <Pill tone={STATUS_TONE[d.status]} dot>{d.status}</Pill>
              <span style={{ color: "var(--ink-2)", fontSize: 11 }}>{d.source}</span>
              <span style={{ color: "var(--ink-2)", fontSize: 11 }}>{d.category}</span>
              <span style={{ color: "var(--ink-2)", fontSize: 11 }}>{d.region}</span>
              <span className="mono" style={{ fontSize: 10.5, color: "var(--ink-3)" }}>
                {new Date(d.uploadedAtUtc).toLocaleDateString()}
              </span>
              <div style={{ display: "flex", gap: 6, justifyContent: "flex-end" }}>
                <a href={`/api/documents/${d.id}/download`} download={d.fileName} title="Download Original">
                  <Btn small>📥</Btn>
                </a>
                {d.externalReference && (
                  <a href={d.externalReference} target="_blank" rel="noreferrer" title="View Source">
                    <Btn small>👁</Btn>
                  </a>
                )}
                {isManager(user?.role) && <Btn small onClick={() => onReindex(d.id)} title="Reindex">↻</Btn>}
                {isEngineer(user?.role) && <Btn small style={{ color: "var(--crit)" }} onClick={() => onDeleteClick(d)} title="Delete Document">🗑</Btn>}
              </div>
            </div>
          ))}
          {total > 10 && (
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "12px 14px", borderTop: "1px solid var(--line)" }}>
              <span className="mono" style={{ fontSize: 11, color: "var(--ink-3)" }}>
                Showing {((page - 1) * 10) + 1} to {Math.min(page * 10, total)} of {total} documents
              </span>
              <div style={{ display: "flex", gap: 8 }}>
                <Btn small disabled={page === 1} onClick={() => setPage(page - 1)}>Previous</Btn>
                <Btn small disabled={page * 10 >= total} onClick={() => setPage(page + 1)}>Next</Btn>
              </div>
            </div>
          )}
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
                    {p.isAvailable ? "connected" : "not connected"}
                  </Pill>
                </div>
              ))}
              <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 8 }}>
                Cloud providers list as &ldquo;not connected&rdquo; until an SDK adapter is wired in
                Modules.Ai.Infrastructure &rarr; DocumentStorageRegistry.
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
      {deleteDoc && <DeleteModal doc={deleteDoc} onClose={() => setDeleteDoc(null)} onDeleted={async () => { setDeleteDoc(null); await refresh(); }} />}
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
  const [selectedCount, setSelectedCount] = useState(0);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    const files = fileRef.current?.files;
    if (!files || files.length === 0) { setErr("Pick a file first."); return; }
    setBusy(true); setErr(null);
    
    try {
      await Promise.all(Array.from(files).map(async (file) => {
        const fd = new FormData();
        fd.append("file", file);
        fd.append("title", files.length > 1 ? file.name : (form.title || file.name));
        fd.append("category", form.category);
        fd.append("region", form.region);
        fd.append("tags", form.tags);
        return api.uploadDocument(fd);
      }));
      onUploaded();
    } catch (e) { setErr(String(e)); }
    finally { setBusy(false); }
  };

  return (
    <Modal title="Upload document" onClose={onClose}>
      <form onSubmit={submit} style={{ display: "grid", gap: 10 }}>
        <Field label="Files (PDF, CSV, JSON, Excel, Text)">
          <input 
            ref={fileRef} type="file" multiple accept=".pdf,.csv,.xlsx,.json,text/*" 
            required style={inputStyle} 
            onChange={(e) => setSelectedCount(e.target.files?.length || 0)} 
          />
        </Field>
        <Field label="Title (optional)">
          <input 
            style={inputStyle} value={form.title} 
            onChange={e => setForm({ ...form, title: e.target.value })} 
            disabled={selectedCount > 1} 
            placeholder={selectedCount > 1 ? "Auto-generated from filenames" : ""} 
          />
        </Field>
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
          Linking registers the document in the index. Ingestion calls the provider on demand &mdash;
          providers shown as &ldquo;not connected&rdquo; will fail until an SDK adapter is wired up.
        </div>
        <Field label="Source">
          <select style={inputStyle} value={form.source} onChange={e => setForm({ ...form, source: e.target.value as typeof form.source })}>
            {cloudOptions.map(p => (
              <option key={p.source} value={p.source}>
                {p.source} {p.isAvailable ? "" : "(not connected)"}
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

function DeleteModal({ doc, onClose, onDeleted }: { doc: DocumentListItem; onClose: () => void; onDeleted: () => void }) {
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const performDelete = async () => {
    setBusy(true); setErr(null);
    try { await api.deleteDocument(doc.id); onDeleted(); }
    catch (e) { setErr(String(e)); }
    finally { setBusy(false); }
  };

  return (
    <Modal title="Confirm Deletion" onClose={onClose}>
      <div style={{ display: "grid", gap: 16 }}>
        <div style={{ fontSize: 13, color: "var(--ink)" }}>
          Are you sure you want to delete <b style={{ color: "var(--crit)" }}>{doc.title}</b>?
        </div>
        <div className="mono" style={{ fontSize: 11, color: "var(--ink-3)", background: "var(--bg-2)", padding: 12, borderRadius: 6, borderLeft: "3px solid var(--crit)" }}>
          This will permanently remove:
          <ul style={{ marginTop: 6, paddingLeft: 18 }}>
            <li>The original source file ({doc.fileName})</li>
            <li>All extracted text chunks</li>
            <li>AI vector embeddings for search</li>
          </ul>
        </div>
        {err && <div className="mono" style={{ color: "var(--crit)", fontSize: 11 }}>⚠ {err}</div>}
        <div style={{ display: "flex", gap: 8, justifyContent: "flex-end", marginTop: 10 }}>
          <Btn type="button" onClick={onClose} disabled={busy}>Cancel</Btn>
          <Btn type="button" onClick={performDelete} disabled={busy} style={{ background: "var(--crit)", color: "white", border: "none" }}>
            {busy ? "Deleting…" : "Delete Forever"}
          </Btn>
        </div>
      </div>
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
