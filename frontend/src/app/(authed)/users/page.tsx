"use client";

import { useEffect, useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill, Section } from "@/components/UI";
import { api } from "@/lib/api";
import type { UserListItem } from "@/lib/types";

const ROLE_CAPS: Record<string, string[]> = {
  engineer: ["copilot.read", "copilot.write", "tower.diagnose", "alerts.read", "alerts.ack", "map.read"],
  manager:  ["copilot.read", "copilot.write", "alerts.read", "alerts.assign", "reports.export", "users.read", "map.read", "dashboard.read"],
  admin:    ["*.admin"],
  viewer:   ["dashboard.read", "alerts.read", "map.read"],
};

export default function UsersPage() {
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [sel, setSel] = useState<UserListItem | null>(null);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    api.users().then(r => { setUsers(r); setSel(r[0] ?? null); }).catch(e => setErr(String(e)));
  }, []);

  const caps = sel ? ROLE_CAPS[sel.role] ?? [] : [];

  return (
    <>
      <TopBar
        title="Users & Roles"
        sub={`${users.length} accounts · 4 roles · JWT issued by TelcoPilot Identity`}
        right={<Btn primary>+ Invite user</Btn>}
      />
      <div style={{ padding: 22, display: "grid", gridTemplateColumns: "1fr 360px", gap: 14 }}>
        {err && <div className="mono" style={{ color: "var(--crit)" }}>⚠ {err} (manager+ required)</div>}
        <Card pad={0}>
          <div style={{
            padding: "12px 14px", borderBottom: "1px solid var(--line)",
            display: "grid", gridTemplateColumns: "2fr 1fr 1.2fr 1.2fr 1fr", gap: 10,
            fontSize: 10, fontFamily: "var(--mono)", color: "var(--ink-3)",
            letterSpacing: ".12em", textTransform: "uppercase",
          }}>
            <span>USER</span><span>ROLE</span><span>TEAM</span><span>REGION</span><span>LAST ACTIVE</span>
          </div>
          {users.map((u, i) => {
            const active = sel?.id === u.id;
            return (
              <button key={u.id} onClick={() => setSel(u)} style={{
                appearance: "none", width: "100%", textAlign: "left",
                padding: "12px 14px",
                borderBottom: i < users.length - 1 ? "1px solid var(--line)" : 0,
                display: "grid", gridTemplateColumns: "2fr 1fr 1.2fr 1.2fr 1fr", gap: 10,
                background: active ? "var(--bg-2)" : "transparent",
                border: "none",
                borderLeft: "3px solid " + (active ? "var(--accent)" : "transparent"),
                color: "var(--ink)", cursor: "pointer", alignItems: "center", fontSize: 12.5,
              }}>
                <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                  <div style={{
                    width: 28, height: 28, borderRadius: 5,
                    background: "var(--bg-3)", border: "1px solid var(--line-2)",
                    display: "grid", placeItems: "center",
                    fontFamily: "var(--mono)", fontSize: 10, fontWeight: 600,
                  }}>{u.fullName.split(" ").map(s => s[0]).join("").slice(0, 2).toUpperCase()}</div>
                  <div>
                    <div style={{ fontWeight: 500 }}>{u.fullName}</div>
                    <div className="mono" style={{ fontSize: 10, color: "var(--ink-3)", marginTop: 1 }}>{u.handle}</div>
                  </div>
                </div>
                <Pill tone={u.role === "admin" ? "crit" : u.role === "manager" ? "warn" : u.role === "engineer" ? "accent" : "neutral"}>{u.role}</Pill>
                <span style={{ color: "var(--ink-2)" }}>{u.team}</span>
                <span style={{ color: "var(--ink-2)" }}>{u.region}</span>
                <span className="mono" style={{ fontSize: 10.5, color: "var(--ink-3)" }}>
                  {u.lastLoginAtUtc ? new Date(u.lastLoginAtUtc).toLocaleString() : "—"}
                </span>
              </button>
            );
          })}
        </Card>

        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          {sel && (
            <>
              <Card pad={16}>
                <div style={{ display: "flex", gap: 12, alignItems: "center", marginBottom: 14 }}>
                  <div style={{
                    width: 48, height: 48, borderRadius: 8,
                    background: "linear-gradient(135deg,var(--bg-3),var(--bg-2))",
                    border: "1px solid var(--line-2)",
                    display: "grid", placeItems: "center",
                    fontFamily: "var(--mono)", fontSize: 16, fontWeight: 600,
                  }}>{sel.fullName.split(" ").map(s => s[0]).join("").slice(0, 2).toUpperCase()}</div>
                  <div>
                    <div style={{ fontSize: 14, fontWeight: 600 }}>{sel.fullName}</div>
                    <div className="mono" style={{ fontSize: 10.5, color: "var(--ink-3)", marginTop: 2 }}>{sel.email}</div>
                  </div>
                </div>
                <Row k="Role" v={sel.role} />
                <Row k="Team" v={sel.team} />
                <Row k="Region scope" v={sel.region} />
                <Row k="Last login" v={sel.lastLoginAtUtc ? new Date(sel.lastLoginAtUtc).toLocaleString() : "—"} last />
              </Card>
              <Section label="CAPABILITIES (RBAC)">
                <Card pad={14}>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                    {caps.map(c => (
                      <span key={c} className="mono" style={{
                        fontSize: 10.5, padding: "3px 8px", borderRadius: 3,
                        background: "var(--accent-dim)", color: "var(--accent)",
                        border: "1px solid var(--accent-line)",
                      }}>{c}</span>
                    ))}
                  </div>
                </Card>
              </Section>
            </>
          )}
        </div>
      </div>
    </>
  );
}

function Row({ k, v, last }: { k: string; v: string; last?: boolean }) {
  return (
    <div style={{ display: "flex", justifyContent: "space-between", padding: "8px 0", borderBottom: last ? 0 : "1px solid var(--line)", fontSize: 12 }}>
      <span style={{ color: "var(--ink-3)" }}>{k}</span>
      <span className="mono" style={{ textTransform: "capitalize" }}>{v}</span>
    </div>
  );
}
