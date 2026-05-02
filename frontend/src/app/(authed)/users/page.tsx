"use client";

import { useEffect, useMemo, useState } from "react";
import { TopBar } from "@/components/TopBar";
import { Btn, Card, Pill, Section } from "@/components/UI";
import { RoleGate } from "@/components/RoleGate";
import { useAuth } from "@/lib/auth";
import { canManageTarget, isAdmin, isManager } from "@/lib/rbac";
import { api } from "@/lib/api";
import type { UserListItem } from "@/lib/types";

const ROLE_CAPS: Record<string, string[]> = {
  engineer: [
    "copilot.read",
    "copilot.write",
    "tower.diagnose",
    "alerts.read",
    "alerts.ack",
    "map.read",
  ],
  manager: [
    "copilot.read",
    "copilot.write",
    "alerts.read",
    "alerts.assign",
    "users.create",
    "users.update",
    "reports.export",
    "map.read",
    "dashboard.read",
  ],
  admin: ["*.admin", "users.delete", "roles.assign"],
  viewer: ["dashboard.read", "alerts.read", "map.read"],
};

const ROLES = ["engineer", "manager", "admin", "viewer"];

export default function UsersPage() {
  return (
    <RoleGate minRole="manager">
      <UsersInner />
    </RoleGate>
  );
}

function UsersInner() {
  const { user: me } = useAuth();
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [sel, setSel] = useState<UserListItem | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);

  const refresh = async () => {
    try {
      const r = await api.users();
      setUsers(r);
      setSel((prev) =>
        prev
          ? (r.find((u) => u.id === prev.id) ?? r[0] ?? null)
          : (r[0] ?? null),
      );
      setErr(null);
    } catch (e) {
      setErr(String(e));
    }
  };

  useEffect(() => {
    void refresh();
  }, []);

  const caps = sel ? (ROLE_CAPS[sel.role] ?? []) : [];
  const canEditSel =
    !!sel && !!me && canManageTarget(me.role, sel.role) && sel.id !== me.id;
  const canDeleteSel = !!sel && !!me && isAdmin(me.role) && sel.id !== me.id;

  const onChangeRole = async (role: string) => {
    if (!sel) return;
    setBusy(true);
    try {
      await api.changeUserRole(sel.id, role);
      await refresh();
    } catch (e) {
      setErr(String(e));
    } finally {
      setBusy(false);
    }
  };

  const onToggleActive = async () => {
    if (!sel) return;
    setBusy(true);
    try {
      await api.setUserActive(sel.id, !sel.isActive);
      await refresh();
    } catch (e) {
      setErr(String(e));
    } finally {
      setBusy(false);
    }
  };

  const onDelete = async () => {
    if (!sel) return;
    if (!window.confirm(`Delete ${sel.fullName}? This cannot be undone.`))
      return;
    setBusy(true);
    try {
      await api.deleteUser(sel.id);
      setSel(null);
      await refresh();
    } catch (e) {
      setErr(String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <TopBar
        title="Users & Roles"
        sub={`${users.length} accounts · 4 roles · JWT issued by TelcoPilot Identity`}
        right={
          isManager(me?.role) ? (
            <Btn primary onClick={() => setCreateOpen(true)}>
              + Create user
            </Btn>
          ) : undefined
        }
      />
      <div
        style={{
          padding: 22,
          display: "grid",
          gridTemplateColumns: "1fr 380px",
          gap: 14,
        }}
      >
        {err && (
          <div
            className="mono"
            style={{ color: "var(--crit)", gridColumn: "1 / -1" }}
          >
            ⚠ {err}
          </div>
        )}
        <Card pad={0}>
          <div
            style={{
              padding: "12px 14px",
              borderBottom: "1px solid var(--line)",
              display: "grid",
              gridTemplateColumns: "2fr 1fr 1fr 1fr 1fr 1fr",
              gap: 10,
              fontSize: 10,
              fontFamily: "var(--mono)",
              color: "var(--ink-3)",
              letterSpacing: ".12em",
              textTransform: "uppercase",
            }}
          >
            <span>USER</span>
            <span>ROLE</span>
            <span>STATE</span>
            <span>TEAM</span>
            <span>REGION</span>
            <span>LAST ACTIVE</span>
          </div>
          {users.map((u, i) => {
            const active = sel?.id === u.id;
            return (
              <button
                key={u.id}
                onClick={() => setSel(u)}
                style={{
                  appearance: "none",
                  width: "100%",
                  textAlign: "left",
                  padding: "12px 14px",
                  borderBottom:
                    i < users.length - 1 ? "1px solid var(--line)" : 0,
                  display: "grid",
                  gridTemplateColumns: "2fr 1fr 1fr 1fr 1fr 1fr",
                  gap: 10,
                  background: active ? "var(--bg-2)" : "transparent",
                  borderTop: "none",
                  borderRight: "none",
                  borderLeft:
                    "3px solid " + (active ? "var(--accent)" : "transparent"),
                  color: "var(--ink)",
                  cursor: "pointer",
                  alignItems: "center",
                  fontSize: 12.5,
                  opacity: u.isActive ? 1 : 0.55,
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                  <div
                    style={{
                      width: 28,
                      height: 28,
                      borderRadius: 5,
                      background: "var(--bg-3)",
                      border: "1px solid var(--line-2)",
                      display: "grid",
                      placeItems: "center",
                      fontFamily: "var(--mono)",
                      fontSize: 10,
                      fontWeight: 600,
                    }}
                  >
                    {u.fullName
                      .split(" ")
                      .map((s) => s[0])
                      .join("")
                      .slice(0, 2)
                      .toUpperCase()}
                  </div>
                  <div>
                    <div style={{ fontWeight: 500 }}>{u.fullName}</div>
                    <div
                      className="mono"
                      style={{
                        fontSize: 10,
                        color: "var(--ink-3)",
                        marginTop: 1,
                      }}
                    >
                      {u.handle}
                    </div>
                  </div>
                </div>
                <Pill
                  tone={
                    u.role === "admin"
                      ? "crit"
                      : u.role === "manager"
                        ? "warn"
                        : u.role === "engineer"
                          ? "accent"
                          : "neutral"
                  }
                >
                  {u.role}
                </Pill>
                <Pill tone={u.isActive ? "ok" : "neutral"} dot>
                  {u.isActive ? "active" : "inactive"}
                </Pill>
                <span style={{ color: "var(--ink-2)" }}>{u.team}</span>
                <span style={{ color: "var(--ink-2)" }}>{u.region}</span>
                <span
                  className="mono"
                  style={{ fontSize: 10.5, color: "var(--ink-3)" }}
                >
                  {u.lastLoginAtUtc
                    ? new Date(u.lastLoginAtUtc).toLocaleString()
                    : "—"}
                </span>
              </button>
            );
          })}
        </Card>

        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          {sel && (
            <>
              <Card pad={16}>
                <div
                  style={{
                    display: "flex",
                    gap: 12,
                    alignItems: "center",
                    marginBottom: 14,
                  }}
                >
                  <div
                    style={{
                      width: 48,
                      height: 48,
                      borderRadius: 8,
                      background:
                        "linear-gradient(135deg,var(--bg-3),var(--bg-2))",
                      border: "1px solid var(--line-2)",
                      display: "grid",
                      placeItems: "center",
                      fontFamily: "var(--mono)",
                      fontSize: 16,
                      fontWeight: 600,
                    }}
                  >
                    {sel.fullName
                      .split(" ")
                      .map((s) => s[0])
                      .join("")
                      .slice(0, 2)
                      .toUpperCase()}
                  </div>
                  <div>
                    <div style={{ fontSize: 14, fontWeight: 600 }}>
                      {sel.fullName}
                    </div>
                    <div
                      className="mono"
                      style={{
                        fontSize: 10.5,
                        color: "var(--ink-3)",
                        marginTop: 2,
                      }}
                    >
                      {sel.email}
                    </div>
                  </div>
                </div>
                <Row k="Role" v={sel.role} />
                <Row k="Status" v={sel.isActive ? "active" : "inactive"} />
                <Row k="Team" v={sel.team} />
                <Row k="Region scope" v={sel.region} />
                <Row
                  k="Created"
                  v={new Date(sel.createdAtUtc).toLocaleString()}
                />
                <Row
                  k="Last login"
                  v={
                    sel.lastLoginAtUtc
                      ? new Date(sel.lastLoginAtUtc).toLocaleString()
                      : "—"
                  }
                  last
                />
              </Card>

              {canEditSel && (
                <Section label="ACTIONS">
                  <Card
                    pad={14}
                    style={{
                      display: "flex",
                      flexDirection: "column",
                      gap: 10,
                    }}
                  >
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                      <Btn
                        small
                        onClick={() => setEditOpen(true)}
                        disabled={busy}
                      >
                        Edit profile
                      </Btn>
                      <Btn small onClick={onToggleActive} disabled={busy}>
                        {sel.isActive ? "Deactivate" : "Activate"}
                      </Btn>
                      {canDeleteSel && (
                        <Btn
                          small
                          onClick={onDelete}
                          disabled={busy}
                          style={{ color: "var(--crit)" }}
                        >
                          Delete
                        </Btn>
                      )}
                    </div>
                    <div>
                      <div
                        className="mono uppr"
                        style={{
                          fontSize: 9,
                          color: "var(--ink-3)",
                          letterSpacing: ".12em",
                          marginBottom: 6,
                        }}
                      >
                        Change role
                      </div>
                      <div
                        style={{ display: "flex", flexWrap: "wrap", gap: 6 }}
                      >
                        {ROLES.map((r) => {
                          const disabled =
                            busy ||
                            (isManager(me?.role) &&
                              !isAdmin(me?.role) &&
                              r === "admin");
                          return (
                            <Btn
                              key={r}
                              small
                              disabled={disabled || sel.role === r}
                              onClick={() => onChangeRole(r)}
                            >
                              {r}
                            </Btn>
                          );
                        })}
                      </div>
                    </div>
                  </Card>
                </Section>
              )}

              <Section label="CAPABILITIES (RBAC)">
                <Card pad={14}>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                    {caps.map((c) => (
                      <span
                        key={c}
                        className="mono"
                        style={{
                          fontSize: 10.5,
                          padding: "3px 8px",
                          borderRadius: 3,
                          background: "var(--accent-dim)",
                          color: "var(--accent)",
                          border: "1px solid var(--accent-line)",
                        }}
                      >
                        {c}
                      </span>
                    ))}
                  </div>
                </Card>
              </Section>
            </>
          )}
        </div>
      </div>

      {createOpen && (
        <CreateUserModal
          actorRole={me?.role}
          onClose={() => setCreateOpen(false)}
          onCreated={async () => {
            setCreateOpen(false);
            await refresh();
          }}
        />
      )}

      {editOpen && sel && (
        <EditUserModal
          user={sel}
          onClose={() => setEditOpen(false)}
          onSaved={async () => {
            setEditOpen(false);
            await refresh();
          }}
        />
      )}
    </>
  );
}

function Row({ k, v, last }: { k: string; v: string; last?: boolean }) {
  return (
    <div
      style={{
        display: "flex",
        justifyContent: "space-between",
        padding: "8px 0",
        borderBottom: last ? 0 : "1px solid var(--line)",
        fontSize: 12,
      }}
    >
      <span style={{ color: "var(--ink-3)" }}>{k}</span>
      <span className="mono" style={{ textTransform: "capitalize" }}>
        {v}
      </span>
    </div>
  );
}

function CreateUserModal({
  actorRole,
  onClose,
  onCreated,
}: {
  actorRole?: string;
  onClose: () => void;
  onCreated: () => void;
}) {
  const [form, setForm] = useState({
    email: "",
    password: "",
    fullName: "",
    handle: "",
    role: "engineer",
    team: "",
    region: "All regions",
  });
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const allowedRoles = useMemo(
    () => ROLES.filter((r) => isAdmin(actorRole) || r !== "admin"),
    [actorRole],
  );

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      await api.createUser(form);
      onCreated();
    } catch (e) {
      setErr(String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal title="Create user" onClose={onClose}>
      <form onSubmit={submit} style={{ display: "grid", gap: 10 }}>
        <Field label="Full name">
          <input
            style={inputStyle}
            required
            value={form.fullName}
            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
          />
        </Field>
        <Field label="Handle">
          <input
            style={inputStyle}
            required
            value={form.handle}
            onChange={(e) => setForm({ ...form, handle: e.target.value })}
          />
        </Field>
        <Field label="Email">
          <input
            style={inputStyle}
            type="email"
            required
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
          />
        </Field>
        <Field label="Temp password (≥8 chars)">
          <input
            style={inputStyle}
            type="password"
            required
            minLength={8}
            value={form.password}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
          />
        </Field>
        <Field label="Role">
          <select
            style={inputStyle}
            value={form.role}
            onChange={(e) => setForm({ ...form, role: e.target.value })}
          >
            {allowedRoles.map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Team">
          <input
            style={inputStyle}
            value={form.team}
            onChange={(e) => setForm({ ...form, team: e.target.value })}
          />
        </Field>
        <Field label="Region">
          <input
            style={inputStyle}
            value={form.region}
            onChange={(e) => setForm({ ...form, region: e.target.value })}
          />
        </Field>
        {err && (
          <div className="mono" style={{ color: "var(--crit)", fontSize: 11 }}>
            ⚠ {err}
          </div>
        )}
        <div
          style={{
            display: "flex",
            gap: 8,
            justifyContent: "flex-end",
            marginTop: 6,
          }}
        >
          <Btn type="button" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="submit" primary disabled={busy}>
            {busy ? "Creating…" : "Create"}
          </Btn>
        </div>
      </form>
    </Modal>
  );
}

function EditUserModal({
  user,
  onClose,
  onSaved,
}: {
  user: UserListItem;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [form, setForm] = useState({
    fullName: user.fullName,
    handle: user.handle,
    team: user.team,
    region: user.region,
  });
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      await api.updateUser(user.id, form);
      onSaved();
    } catch (e) {
      setErr(String(e));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Modal title={`Edit ${user.handle}`} onClose={onClose}>
      <form onSubmit={submit} style={{ display: "grid", gap: 10 }}>
        <Field label="Full name">
          <input
            style={inputStyle}
            required
            value={form.fullName}
            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
          />
        </Field>
        <Field label="Handle">
          <input
            style={inputStyle}
            required
            value={form.handle}
            onChange={(e) => setForm({ ...form, handle: e.target.value })}
          />
        </Field>
        <Field label="Team">
          <input
            style={inputStyle}
            value={form.team}
            onChange={(e) => setForm({ ...form, team: e.target.value })}
          />
        </Field>
        <Field label="Region">
          <input
            style={inputStyle}
            value={form.region}
            onChange={(e) => setForm({ ...form, region: e.target.value })}
          />
        </Field>
        {err && (
          <div className="mono" style={{ color: "var(--crit)", fontSize: 11 }}>
            ⚠ {err}
          </div>
        )}
        <div
          style={{
            display: "flex",
            gap: 8,
            justifyContent: "flex-end",
            marginTop: 6,
          }}
        >
          <Btn type="button" onClick={onClose}>
            Cancel
          </Btn>
          <Btn type="submit" primary disabled={busy}>
            {busy ? "Saving…" : "Save"}
          </Btn>
        </div>
      </form>
    </Modal>
  );
}

const inputStyle: React.CSSProperties = {
  width: "100%",
  padding: "8px 10px",
  borderRadius: 5,
  border: "1px solid var(--line-2)",
  background: "var(--bg-2)",
  color: "var(--ink)",
  fontFamily: "var(--mono)",
  fontSize: 12,
};

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <label style={{ display: "grid", gap: 4 }}>
      <span
        className="mono uppr"
        style={{ fontSize: 9, color: "var(--ink-3)", letterSpacing: ".12em" }}
      >
        {label}
      </span>
      {children}
    </label>
  );
}

function Modal({
  title,
  children,
  onClose,
}: {
  title: string;
  children: React.ReactNode;
  onClose: () => void;
}) {
  return (
    <div
      onClick={onClose}
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(0,0,0,.55)",
        display: "grid",
        placeItems: "center",
        zIndex: 50,
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          background: "var(--bg-1)",
          border: "1px solid var(--line-2)",
          borderRadius: 10,
          width: 420,
          padding: 20,
          boxShadow: "0 20px 60px rgba(0,0,0,.45)",
        }}
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 14,
          }}
        >
          <div style={{ fontSize: 14, fontWeight: 600 }}>{title}</div>
          <button
            onClick={onClose}
            style={{
              appearance: "none",
              background: "transparent",
              border: 0,
              color: "var(--ink-3)",
              cursor: "pointer",
              fontSize: 16,
            }}
          >
            ×
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}
