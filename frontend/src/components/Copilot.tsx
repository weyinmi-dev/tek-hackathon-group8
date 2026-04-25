"use client";

import { useEffect, useRef, useState } from "react";
import { api } from "@/lib/api";
import { Btn } from "@/components/UI";
import type { CopilotAnswer, SkillTraceEntry } from "@/lib/types";

const SUGGESTED = [
  "Why is Lagos West slow?",
  "Show all outages in the last 2 hours",
  "Which tower is causing packet loss in Ikeja?",
  "Predict the next likely failure",
  "Compare Lekki vs Victoria Island latency",
];

type Msg =
  | { role: "system"; content: string }
  | { role: "user"; content: string }
  | { role: "assistant"; answer: CopilotAnswer; query: string };

export function Copilot({ embedded = false }: { embedded?: boolean }) {
  const [messages, setMessages] = useState<Msg[]>([
    { role: "system", content: "TelcoPilot · v1.4 · Powered by Azure OpenAI + Semantic Kernel · Context: Lagos metro NOC" },
  ]);
  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const [trace, setTrace] = useState<SkillTraceEntry[]>([]);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (scrollRef.current) scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
  }, [messages, trace, busy]);

  async function ask(q: string) {
    if (!q.trim() || busy) return;
    setInput(""); setBusy(true);
    setMessages(m => [...m, { role: "user", content: q }]);

    // Animated skill trace — pre-populate so the agent panel renders immediately
    const stages: SkillTraceEntry[] = [
      { skill: "IntentParser", function: "parseQuery", durationMs: 0, status: "running" },
    ];
    setTrace(stages);

    try {
      const a = await api.chat(q);
      // Replace synthetic trace with the backend's real trace
      setTrace(a.skillTrace);
      // Brief pause so users see the trace before the answer renders
      await new Promise(r => setTimeout(r, 400));
      setMessages(m => [...m, { role: "assistant", answer: a, query: q }]);
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Copilot is unavailable.";
      setMessages(m => [...m, { role: "assistant", query: q, answer: {
        answer: `ROOT CAUSE\nCopilot service unavailable.\n\nAFFECTED\n• Backend chat endpoint returned an error.\n\nRECOMMENDED ACTIONS\n1. Verify the backend container is healthy (\`docker compose ps\`)\n2. Check Ai:* config — Mock provider works without Azure OpenAI keys\n3. Inspect logs: \`docker compose logs backend\`\n\nCONFIDENCE\n10 % — ${msg}`,
        confidence: 0.1, skillTrace: [], attachments: [], provider: "error"
      } }]);
    } finally {
      setBusy(false); setTrace([]);
    }
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%", minHeight: 0 }}>
      <div ref={scrollRef} style={{
        flex: 1, overflowY: "auto",
        padding: embedded ? 14 : "22px 22px 14px",
        display: "flex", flexDirection: "column", gap: 16,
      }}>
        {messages.map((m, i) => <Message key={i} m={m} />)}
        {busy && <SkillTrace steps={trace} />}
      </div>

      {messages.length <= 1 && !busy && (
        <div style={{ padding: embedded ? "0 14px 10px" : "0 22px 10px", display: "flex", flexWrap: "wrap", gap: 6 }}>
          {SUGGESTED.map(s => (
            <button key={s} onClick={() => ask(s)} style={{
              appearance: "none", border: "1px solid var(--line-2)", background: "var(--bg-1)",
              color: "var(--ink-2)", padding: "6px 10px", borderRadius: 14, fontSize: 11.5,
              fontFamily: "var(--mono)", cursor: "pointer",
            }}>→ {s}</button>
          ))}
        </div>
      )}

      <div style={{ padding: embedded ? 14 : "14px 22px 22px", borderTop: "1px solid var(--line)", background: "var(--bg-1)" }}>
        <div style={{
          display: "flex", alignItems: "center", gap: 10,
          padding: "10px 12px", background: "var(--bg-2)",
          border: "1px solid var(--line-2)", borderRadius: 8,
        }}>
          <span className="mono" style={{ color: "var(--accent)", fontSize: 13, fontWeight: 600 }}>›</span>
          <input
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={e => { if (e.key === "Enter") ask(input); }}
            placeholder='Ask: "why is Lagos West slow?"'
            disabled={busy}
            style={{
              flex: 1, border: 0, background: "transparent", outline: "none",
              fontSize: 13.5, color: "var(--ink)", fontFamily: "var(--sans)",
            }}
          />
          <span className="mono" style={{ fontSize: 10, color: "var(--ink-3)" }}>⏎</span>
          <Btn primary small onClick={() => ask(input)} disabled={busy}>{busy ? "Thinking…" : "Ask"}</Btn>
        </div>
        <div className="mono" style={{ fontSize: 9.5, color: "var(--ink-3)", marginTop: 8, letterSpacing: ".08em" }}>
          QUERIES LOGGED · MODEL: AZURE OPENAI (or MOCK fallback)
        </div>
      </div>
    </div>
  );
}

function Message({ m }: { m: Msg }) {
  if (m.role === "system") {
    return <div className="mono" style={{ fontSize: 10.5, color: "var(--ink-3)", padding: "0 4px", letterSpacing: ".04em", animation: "fadein .3s" }}>⌁ {m.content}</div>;
  }
  if (m.role === "user") {
    return (
      <div style={{ alignSelf: "flex-end", maxWidth: "72%", animation: "fadein .25s" }}>
        <div style={{
          padding: "10px 14px", borderRadius: "10px 10px 2px 10px",
          background: "var(--bg-3)", border: "1px solid var(--line-2)",
          fontSize: 13.5, lineHeight: 1.5,
        }}>{m.content}</div>
        <div className="mono uppr" style={{ fontSize: 9, color: "var(--ink-3)", marginTop: 4, textAlign: "right", letterSpacing: ".10em" }}>
          YOU · {new Date().toTimeString().slice(0, 5)}
        </div>
      </div>
    );
  }
  return (
    <div style={{ alignSelf: "flex-start", maxWidth: "92%", width: "100%", animation: "fadein .35s" }}>
      <div className="mono uppr" style={{
        fontSize: 9, color: "var(--accent)", marginBottom: 6, letterSpacing: ".14em",
        display: "flex", alignItems: "center", gap: 6,
      }}>
        <span style={{ width: 6, height: 6, borderRadius: "50%", background: "var(--accent)", boxShadow: "0 0 8px var(--accent)" }} />
        TELCOPILOT · ANSWER · {m.answer.provider.toUpperCase()}
      </div>
      <div style={{
        padding: "14px 16px", borderRadius: "2px 10px 10px 10px",
        background: "var(--bg-1)", border: "1px solid var(--line-2)",
        borderLeft: "2px solid var(--accent)",
        fontSize: 13.5, lineHeight: 1.6, whiteSpace: "pre-wrap",
      }}>
        <FormattedAnswer text={m.answer.answer} />
      </div>
    </div>
  );
}

function FormattedAnswer({ text }: { text: string }) {
  const lines = text.split("\n").map((line, i) => {
    if (/^(ROOT CAUSE|AFFECTED|RECOMMENDED ACTIONS|CONFIDENCE)$/i.test(line.trim())) {
      return <div key={i} className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginTop: i > 0 ? 12 : 0, marginBottom: 4 }}>{line.trim()}</div>;
    }
    const segs = line.split(/(TWR-[A-Z]+-[A-Z0-9]*-?\d+|TWR-[A-Z]+-\d+|INC-\d+|\d+\s*%)/g);
    return (
      <div key={i} style={{ minHeight: line.trim() ? "auto" : 4 }}>
        {segs.map((s, j) => {
          if (/^TWR-/.test(s) || /^INC-/.test(s))
            return <span key={j} className="mono" style={{ color: "var(--accent)", background: "var(--accent-dim)", padding: "1px 5px", borderRadius: 3, fontSize: 11.5, whiteSpace: "nowrap" }}>{s}</span>;
          if (/\d+\s*%/.test(s))
            return <span key={j} className="mono" style={{ color: "var(--warn)", fontWeight: 600 }}>{s}</span>;
          return <span key={j}>{s}</span>;
        })}
      </div>
    );
  });
  return <div>{lines}</div>;
}

function SkillTrace({ steps }: { steps: SkillTraceEntry[] }) {
  return (
    <div style={{
      padding: 14, background: "var(--bg-1)", border: "1px dashed var(--accent-line)",
      borderRadius: 8, display: "flex", flexDirection: "column", gap: 8, animation: "fadein .2s",
    }}>
      <div className="mono uppr" style={{ fontSize: 9.5, color: "var(--accent)", letterSpacing: ".14em", display: "flex", alignItems: "center", gap: 8 }}>
        <span style={{ display: "inline-block", width: 10, height: 10, border: "1.5px solid var(--accent)", borderTopColor: "transparent", borderRadius: "50%", animation: "spin .8s linear infinite" }} />
        SEMANTIC KERNEL · EXECUTING
      </div>
      {steps.map((s, i) => (
        <div key={i} className="mono" style={{ display: "flex", alignItems: "center", gap: 10, fontSize: 11.5, color: s.status === "done" ? "var(--ink-2)" : "var(--ink)" }}>
          <span style={{
            width: 14, height: 14, borderRadius: 3,
            background: s.status === "done" ? "var(--accent-dim)" : "var(--bg-3)",
            color: s.status === "done" ? "var(--accent)" : "var(--ink-3)",
            display: "grid", placeItems: "center", fontSize: 9, fontWeight: 700,
            border: "1px solid " + (s.status === "done" ? "var(--accent-line)" : "var(--line-2)"),
          }}>{s.status === "done" ? "✓" : i + 1}</span>
          <span style={{ color: "var(--ink-3)" }}>{s.skill}.</span>
          <span>{s.function}()</span>
          <span style={{ flex: 1, height: 1, background: "var(--line)" }} />
          <span style={{ color: s.status === "done" ? "var(--ok)" : "var(--ink-3)", fontSize: 10 }}>
            {s.status === "done" ? `${s.durationMs}ms` : "…"}
          </span>
        </div>
      ))}
    </div>
  );
}
