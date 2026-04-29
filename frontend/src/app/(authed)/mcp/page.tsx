"use client";

import { useEffect, useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill, Section } from "@/components/UI";
import { RoleGate } from "@/components/RoleGate";
import { api } from "@/lib/api";
import type { McpCapability, McpInvocationResult, McpPlugin } from "@/lib/types";

export default function McpPage() {
  return (
    <RoleGate minRole="manager">
      <McpInner />
    </RoleGate>
  );
}

function McpInner() {
  const [plugins, setPlugins] = useState<McpPlugin[]>([]);
  const [sel, setSel] = useState<McpPlugin | null>(null);
  const [cap, setCap] = useState<McpCapability | null>(null);
  const [args, setArgs] = useState<Record<string, string>>({});
  const [result, setResult] = useState<McpInvocationResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    api.mcpPlugins().then(p => {
      setPlugins(p);
      setSel(p[0] ?? null);
      setCap(p[0]?.capabilities[0] ?? null);
    }).catch(e => setErr(String(e)));
  }, []);

  const onSelectPlugin = (p: McpPlugin) => {
    setSel(p);
    setCap(p.capabilities[0] ?? null);
    setArgs({});
    setResult(null);
  };
  const onSelectCap = (c: McpCapability) => {
    setCap(c);
    setArgs({});
    setResult(null);
  };

  const onInvoke = async () => {
    if (!sel || !cap) return;
    setBusy(true); setErr(null); setResult(null);
    try {
      const parsed = Object.fromEntries(
        Object.entries(args).filter(([, v]) => v !== "").map(([k, v]) => [k, v])
      );
      const r = await api.mcpInvoke({ pluginId: sel.pluginId, capability: cap.name, arguments: parsed });
      setResult(r);
    } catch (e) {
      setErr(String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <TopBar
        title="MCP Plugins"
        sub={`${plugins.length} plugin${plugins.length === 1 ? "" : "s"} registered · provider-agnostic extensibility layer`}
      />
      <div style={{ padding: 22, display: "grid", gridTemplateColumns: "260px 1fr", gap: 14 }}>
        {err && <div className="mono" style={{ color: "var(--crit)", gridColumn: "1 / -1" }}>⚠ {err}</div>}

        <Card pad={0}>
          <div className="mono uppr" style={{
            padding: "12px 14px", fontSize: 10, color: "var(--ink-3)",
            letterSpacing: ".12em", borderBottom: "1px solid var(--line)",
          }}>PLUGINS</div>
          {plugins.length === 0 && (
            <div style={{ padding: 14, color: "var(--ink-3)", fontSize: 12 }}>No plugins registered.</div>
          )}
          {plugins.map((p, i) => {
            const active = sel?.pluginId === p.pluginId;
            return (
              <button key={p.pluginId} onClick={() => onSelectPlugin(p)} style={{
                appearance: "none", width: "100%", textAlign: "left",
                padding: "12px 14px",
                borderBottom: i < plugins.length - 1 ? "1px solid var(--line)" : 0,
                background: active ? "var(--bg-2)" : "transparent",
                border: "none", borderLeft: "3px solid " + (active ? "var(--accent)" : "transparent"),
                color: "var(--ink)", cursor: "pointer",
              }}>
                <div style={{ fontWeight: 500, fontSize: 13 }}>{p.displayName}</div>
                <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 2 }}>
                  {p.pluginId} · {p.kind}
                </div>
              </button>
            );
          })}
        </Card>

        {sel && (
          <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
            <Card pad={16}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 14 }}>
                <div>
                  <div style={{ fontSize: 16, fontWeight: 600 }}>{sel.displayName}</div>
                  <div className="mono" style={{ fontSize: 11, color: "var(--ink-3)", marginTop: 2 }}>{sel.pluginId}</div>
                </div>
                <Pill tone={sel.kind === "Internal" ? "ok" : "info"}>{sel.kind}</Pill>
              </div>

              <div className="mono uppr" style={{ fontSize: 9, color: "var(--ink-3)", letterSpacing: ".12em", marginBottom: 6 }}>CAPABILITIES</div>
              <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                {sel.capabilities.map(c => (
                  <Btn key={c.name} small onClick={() => onSelectCap(c)}
                       primary={cap?.name === c.name}>{c.name}</Btn>
                ))}
              </div>
            </Card>

            {cap && (
              <Section label={`INVOKE → ${cap.name}`}>
                <Card pad={16}>
                  <div className="mono" style={{ fontSize: 11, color: "var(--ink-2)", marginBottom: 12 }}>{cap.description}</div>

                  {cap.parameters.length === 0 && (
                    <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>No parameters.</div>
                  )}
                  {cap.parameters.map(p => (
                    <label key={p.name} style={{ display: "grid", gap: 4, marginBottom: 8 }}>
                      <span className="mono uppr" style={{ fontSize: 9, color: "var(--ink-3)", letterSpacing: ".12em" }}>
                        {p.name} {p.required && <span style={{ color: "var(--crit)" }}>*</span>} · {p.type}
                      </span>
                      <input
                        style={inputStyle}
                        value={args[p.name] ?? ""}
                        onChange={e => setArgs({ ...args, [p.name]: e.target.value })}
                        placeholder={p.description}
                      />
                    </label>
                  ))}

                  <div style={{ display: "flex", gap: 8, justifyContent: "flex-end", marginTop: 8 }}>
                    <Btn primary onClick={onInvoke} disabled={busy}>{busy ? "Invoking…" : "▶ Invoke"}</Btn>
                  </div>
                </Card>
              </Section>
            )}

            {result && (
              <Section label={`RESULT · ${result.durationMs}ms`}>
                <Card pad={16}>
                  <div style={{ marginBottom: 8 }}>
                    <Pill tone={result.isSuccess ? "ok" : "crit"} dot>
                      {result.isSuccess ? "success" : "failure"}
                    </Pill>
                  </div>
                  {result.error && (
                    <div className="mono" style={{ color: "var(--crit)", fontSize: 11, marginBottom: 8 }}>⚠ {result.error}</div>
                  )}
                  <pre className="mono" style={{
                    fontSize: 11, color: "var(--ink)", background: "var(--bg-3)",
                    padding: 12, borderRadius: 6, overflow: "auto", maxHeight: 400, margin: 0,
                  }}>{JSON.stringify(result.output, null, 2)}</pre>
                </Card>
              </Section>
            )}
          </div>
        )}
      </div>
    </>
  );
}

const inputStyle: React.CSSProperties = {
  width: "100%", padding: "8px 10px", borderRadius: 5,
  border: "1px solid var(--line-2)", background: "var(--bg-2)", color: "var(--ink)",
  fontFamily: "var(--mono)", fontSize: 12,
};
