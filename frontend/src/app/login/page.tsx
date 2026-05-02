"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";

const inp: React.CSSProperties = {
  width: "100%", padding: "10px 12px", background: "var(--bg-1)",
  border: "1px solid var(--line-2)", borderRadius: 7,
  color: "var(--ink)", fontSize: 13.5, outline: "none",
  fontFamily: "var(--mono)",
};

export default function LoginPage() {
  const router = useRouter();
  const { login } = useAuth();
  const [email, setEmail] = useState("oluwaseun.a@telco.lag");
  const [pw, setPw] = useState("Telco!2025");
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  async function submit(e?: React.FormEvent) {
    e?.preventDefault();
    setBusy(true); setErr(null);
    try {
      await login(email, pw);
      router.push("/dashboard");
    } catch (ex) {
      setErr(ex instanceof Error ? ex.message : "Sign-in failed");
    } finally { setBusy(false); }
  }

  return (
    <div style={{ minHeight: "100vh", display: "grid", gridTemplateColumns: "1fr 1fr", background: "var(--bg)" }}>
      {/* Left — atmospheric panel */}
      <div style={{
        position: "relative", overflow: "hidden",
        background: "var(--bg-hero)",
        borderRight: "1px solid var(--line)",
        padding: "42px 48px", display: "flex", flexDirection: "column", justifyContent: "space-between",
      }}>
        <svg viewBox="0 0 100 100" preserveAspectRatio="none" style={{ position: "absolute", inset: 0, width: "100%", height: "100%", opacity: .5 }}>
          <defs>
            <pattern id="lg" width="4" height="4" patternUnits="userSpaceOnUse">
              <path d="M 4 0 L 0 0 0 4" fill="none" stroke="var(--grid-stroke)" strokeWidth=".15" />
            </pattern>
          </defs>
          <rect width="100" height="100" fill="url(#lg)" />
        </svg>
        <svg viewBox="0 0 100 100" preserveAspectRatio="none" style={{ position: "absolute", inset: 0, width: "100%", height: "100%" }}>
          {[[20, 30], [35, 55], [60, 40], [78, 68], [45, 80], [72, 22]].map(([x, y], i) => (
            <g key={i}>
              <circle cx={x} cy={y} r="6" fill="rgba(0,229,160,.10)" />
              <circle cx={x} cy={y} r="1" fill="#00e5a0">
                <animate attributeName="r" values="1;3;1" dur={`${2 + i * 0.3}s`} repeatCount="indefinite" />
              </circle>
            </g>
          ))}
          <line x1="20" y1="30" x2="35" y2="55" stroke="rgba(0,229,160,.18)" strokeWidth=".15" strokeDasharray="1 1" />
          <line x1="35" y1="55" x2="60" y2="40" stroke="rgba(0,229,160,.18)" strokeWidth=".15" strokeDasharray="1 1" />
          <line x1="60" y1="40" x2="78" y2="68" stroke="rgba(0,229,160,.18)" strokeWidth=".15" strokeDasharray="1 1" />
          <line x1="60" y1="40" x2="72" y2="22" stroke="rgba(0,229,160,.18)" strokeWidth=".15" strokeDasharray="1 1" />
          <line x1="78" y1="68" x2="45" y2="80" stroke="rgba(0,229,160,.18)" strokeWidth=".15" strokeDasharray="1 1" />
        </svg>

        <div style={{ position: "relative", display: "flex", alignItems: "center", gap: 12 }}>
          <div style={{
            width: 36, height: 36, borderRadius: 8,
            background: "var(--accent)", color: "#001a10",
            display: "grid", placeItems: "center", fontWeight: 700,
            fontFamily: "var(--mono)", fontSize: 18,
            boxShadow: "0 0 32px var(--accent-dim)",
          }}>◉</div>
          <div>
            <div style={{ fontWeight: 600, fontSize: 18, letterSpacing: "-.01em" }}>TelcoPilot</div>
            <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".14em", marginTop: 2 }}>NETWORK INTELLIGENCE · v1.4</div>
          </div>
        </div>

        <div style={{ position: "relative", maxWidth: 480 }}>
          <div className="mono uppr" style={{ fontSize: 10.5, color: "var(--accent)", letterSpacing: ".18em", marginBottom: 14 }}>AI-NATIVE TELCO OPERATIONS</div>
          <div style={{ fontSize: 34, fontWeight: 600, letterSpacing: "-.02em", lineHeight: 1.15, marginBottom: 14 }}>
            Stop digging through logs.<br />
            <span style={{ color: "var(--accent)" }}>Ask the network.</span>
          </div>
          <div style={{ fontSize: 14, color: "var(--ink-2)", lineHeight: 1.6, maxWidth: 420 }}>
            Natural-language diagnostics, predictive failure detection, and a live signal map for the Lagos metro NOC.
          </div>
        </div>

        <div style={{ position: "relative", display: "flex", gap: 24, fontFamily: "var(--mono)", fontSize: 10.5, color: "var(--ink-3)", letterSpacing: ".10em" }}>
          <span><span style={{ color: "var(--ok)" }}>●</span> SOC2</span>
          <span><span style={{ color: "var(--ok)" }}>●</span> RBAC</span>
          <span><span style={{ color: "var(--ok)" }}>●</span> JWT</span>
          <span><span style={{ color: "var(--ok)" }}>●</span> 1,284 TOWERS LIVE</span>
        </div>
      </div>

      {/* Right — form */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "center", padding: 42 }}>
        <form onSubmit={submit} style={{ width: 380, display: "flex", flexDirection: "column", gap: 18 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 600, letterSpacing: "-.01em", marginBottom: 6 }}>Sign in</div>
            <div style={{ fontSize: 13, color: "var(--ink-3)" }}>Use your corporate credentials. SSO recommended.</div>
          </div>

          <button
            type="button"
            onClick={() => setErr("Azure AD federation not configured in this environment — use password.")}
            style={{
              appearance: "none", cursor: "pointer",
              padding: "11px 14px", background: "var(--bg-1)", border: "1px solid var(--line-2)",
              borderRadius: 7, color: "var(--ink)", fontSize: 13, fontWeight: 500,
              display: "flex", alignItems: "center", justifyContent: "center", gap: 10,
            }}
          >
            <span style={{ width: 14, height: 14, background: "#0078d4", borderRadius: 2, display: "inline-block" }} />
            Continue with Azure AD
          </button>

          <div style={{ display: "flex", alignItems: "center", gap: 10, color: "var(--ink-3)", fontSize: 11 }}>
            <span style={{ flex: 1, height: 1, background: "var(--line)" }} />
            <span className="mono uppr" style={{ letterSpacing: ".12em" }}>OR PASSWORD</span>
            <span style={{ flex: 1, height: 1, background: "var(--line)" }} />
          </div>

          <Field label="Email">
            <input value={email} onChange={e => setEmail(e.target.value)} style={inp} autoComplete="username" />
          </Field>
          <Field label="Password" right={<span style={{ fontSize: 11, color: "var(--ink-3)" }}>Demo: Telco!2025</span>}>
            <input type="password" value={pw} onChange={e => setPw(e.target.value)} style={inp} autoComplete="current-password" />
          </Field>

          <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 12, color: "var(--ink-2)", cursor: "pointer" }}>
            <input type="checkbox" defaultChecked style={{ accentColor: "var(--accent)" }} />
            Require MFA (TOTP) on next prompt
          </label>

          {err && <div className="mono" style={{ fontSize: 11, color: "var(--crit)" }}>⚠ {err}</div>}

          <button type="submit" disabled={busy} style={{
            appearance: "none", padding: 12, cursor: busy ? "wait" : "pointer",
            background: "var(--accent)", color: "#001a10",
            border: 0, borderRadius: 7, fontWeight: 600, fontSize: 13.5,
            display: "flex", alignItems: "center", justifyContent: "center", gap: 8,
          }}>{busy ? "Authenticating…" : "Sign in →"}</button>

          <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", textAlign: "center", marginTop: 6, letterSpacing: ".04em" }}>
            ALL SIGN-IN ATTEMPTS LOGGED · TLS 1.3 · JWT
          </div>
        </form>
      </div>
    </div>
  );
}

function Field({ label, right, children }: { label: string; right?: React.ReactNode; children: React.ReactNode }) {
  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 6, fontSize: 11.5, color: "var(--ink-2)" }}>
        <span>{label}</span>{right}
      </div>
      {children}
    </div>
  );
}
