"use client";

import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";

type NavItem = { id: string; label: string; icon: string; section: string; badge?: string; adminOnly?: boolean };

const NAV: NavItem[] = [
  { id: "/dashboard", label: "Command Center", icon: "◉", section: "OPS" },
  { id: "/copilot",   label: "Copilot",        icon: "✦", section: "OPS" },
  { id: "/map",       label: "Network Map",    icon: "◎", section: "OPS" },
  { id: "/alerts",    label: "Alerts",         icon: "△", section: "OPS", badge: "14" },
  { id: "/insights",  label: "Dashboard",      icon: "▤", section: "INSIGHTS" },
  { id: "/users",     label: "Users & Roles",  icon: "◆", section: "ADMIN" },
  { id: "/audit",     label: "Audit Log",      icon: "≡", section: "ADMIN" },
];
const SECTIONS = ["OPS", "INSIGHTS", "ADMIN"];

export function Sidebar() {
  const router = useRouter();
  const pathname = usePathname();
  const { user, logout } = useAuth();

  const initials = (user?.fullName ?? "  ").split(" ").map(s => s[0]).join("").slice(0, 2).toUpperCase();

  return (
    <aside style={{
      borderRight: "1px solid var(--line)", background: "var(--bg-1)",
      display: "flex", flexDirection: "column", position: "sticky", top: 0, height: "100vh",
    }}>
      {/* Brand */}
      <div style={{ padding: "18px 18px 14px", borderBottom: "1px solid var(--line)", display: "flex", alignItems: "center", gap: 10 }}>
        <div style={{
          width: 28, height: 28, borderRadius: 6,
          background: "var(--accent)", color: "#001a10",
          display: "grid", placeItems: "center", fontWeight: 700,
          fontFamily: "var(--mono)", fontSize: 14, letterSpacing: "-.02em",
          boxShadow: "0 0 24px var(--accent-dim)",
        }}>◉</div>
        <div style={{ display: "flex", flexDirection: "column", lineHeight: 1.1 }}>
          <div style={{ fontWeight: 600, fontSize: 14, letterSpacing: "-.01em" }}>TelcoPilot</div>
          <div className="mono uppr" style={{ fontSize: 9, color: "var(--ink-3)", letterSpacing: ".14em", marginTop: 2 }}>NOC · LAGOS</div>
        </div>
      </div>

      {/* Nav */}
      <nav style={{ flex: 1, overflowY: "auto", padding: "10px 10px 14px" }}>
        {SECTIONS.map(section => (
          <div key={section} style={{ marginTop: 14 }}>
            <div className="mono uppr" style={{ fontSize: 9.5, color: "var(--ink-3)", padding: "4px 10px 6px", letterSpacing: ".14em" }}>{section}</div>
            {NAV.filter(n => n.section === section).map(n => {
              const active = pathname === n.id || (n.id === "/dashboard" && pathname === "/");
              return (
                <button key={n.id} onClick={() => router.push(n.id)} style={{
                  width: "100%", textAlign: "left",
                  display: "flex", alignItems: "center", gap: 10,
                  padding: "8px 10px", borderRadius: 6,
                  background: active ? "var(--bg-3)" : "transparent",
                  border: "1px solid " + (active ? "var(--line-2)" : "transparent"),
                  color: active ? "var(--ink)" : "var(--ink-2)",
                  cursor: "pointer", fontSize: 13, fontWeight: active ? 500 : 400,
                  position: "relative",
                }}>
                  <span style={{
                    width: 18, height: 18, display: "grid", placeItems: "center",
                    color: active ? "var(--accent)" : "var(--ink-3)",
                    fontFamily: "var(--mono)", fontSize: 12,
                  }}>{n.icon}</span>
                  <span style={{ flex: 1 }}>{n.label}</span>
                  {n.badge && (
                    <span className="mono" style={{
                      fontSize: 9.5, padding: "2px 6px", borderRadius: 3,
                      background: "var(--crit)", color: "#fff", fontWeight: 600,
                    }}>{n.badge}</span>
                  )}
                  {active && <span style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 2, background: "var(--accent)", borderRadius: 2 }} />}
                </button>
              );
            })}
          </div>
        ))}
      </nav>

      {/* User */}
      <div style={{ padding: 12, borderTop: "1px solid var(--line)", display: "flex", gap: 10, alignItems: "center" }}>
        <div style={{
          width: 32, height: 32, borderRadius: 6,
          background: "linear-gradient(135deg,var(--bg-3),var(--bg-2))",
          border: "1px solid var(--line-2)",
          display: "grid", placeItems: "center",
          fontFamily: "var(--mono)", fontSize: 11, fontWeight: 600, color: "var(--ink)",
        }}>{initials || "··"}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 12, fontWeight: 500, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
            {user?.fullName ?? "—"}
          </div>
          <div className="mono uppr" style={{ fontSize: 9, color: "var(--accent)", letterSpacing: ".12em", marginTop: 1 }}>
            ● {user?.role ?? "—"}
          </div>
        </div>
        <button onClick={() => logout()} title="Sign out" style={{
          appearance: "none", background: "transparent", border: 0, color: "var(--ink-3)",
          cursor: "pointer", fontFamily: "var(--mono)", fontSize: 14,
        }}>⏻</button>
      </div>
    </aside>
  );
}
