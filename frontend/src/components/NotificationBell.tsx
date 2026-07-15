"use client";

import { observer } from "mobx-react-lite";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { useSyncStore } from "@/lib/stores/StoreProvider";

/**
 * The operator notification feed, in the TopBar so it is present on every page.
 *
 * The poll lives in StoreProvider rather than here: the bell unmounts and remounts on every
 * navigation, and if it owned the timer, an operator moving between pages would silently reset the
 * interval and could miss a critical alarm.
 */
export const NotificationBell = observer(function NotificationBell() {
  const sync = useSyncStore();
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  // Dismiss on outside click. Without this the panel traps the page — it sits over the content and
  // there is nothing else to click that closes it.
  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, [open]);

  const unread = sync.unreadCount;

  return (
    <div ref={ref} style={{ position: "relative" }}>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-label={`Notifications${unread > 0 ? ` (${unread} unread)` : ""}`}
        style={{
          position: "relative",
          display: "grid",
          placeItems: "center",
          width: 32,
          height: 32,
          borderRadius: 6,
          cursor: "pointer",
          background: open ? "var(--accent-dim)" : "var(--bg-1)",
          border: `1px solid ${open ? "var(--accent-line)" : "var(--line)"}`,
          color: unread > 0 ? "var(--accent)" : "var(--ink-3)",
          fontSize: 13,
        }}
      >
        ◔
        {unread > 0 && (
          <span
            className="mono"
            style={{
              position: "absolute",
              top: -5,
              right: -5,
              minWidth: 15,
              height: 15,
              padding: "0 3px",
              borderRadius: 8,
              background: "var(--crit)",
              color: "#fff",
              fontSize: 9,
              fontWeight: 600,
              display: "grid",
              placeItems: "center",
              boxShadow: "0 0 10px var(--crit)",
            }}
          >
            {unread > 9 ? "9+" : unread}
          </span>
        )}
      </button>

      {open && (
        <div
          style={{
            position: "absolute",
            top: 40,
            right: 0,
            width: 340,
            maxHeight: 420,
            overflowY: "auto",
            background: "var(--bg-1)",
            border: "1px solid var(--line)",
            borderRadius: 10,
            boxShadow: "0 12px 40px rgba(0,0,0,.35)",
            zIndex: 20,
          }}
        >
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              padding: "10px 12px",
              borderBottom: "1px solid var(--line)",
            }}
          >
            <span
              className="mono uppr"
              style={{ fontSize: 9, letterSpacing: ".16em", color: "var(--ink-4)" }}
            >
              Notifications
            </span>
            {unread > 0 && (
              <button
                type="button"
                onClick={() => void sync.markAllRead()}
                className="mono uppr"
                style={{
                  background: "none",
                  border: "none",
                  color: "var(--accent)",
                  fontSize: 9,
                  letterSpacing: ".12em",
                  cursor: "pointer",
                  padding: 0,
                }}
              >
                Mark all read
              </button>
            )}
          </div>

          {sync.notifications.length === 0 ? (
            <div
              className="mono"
              style={{ padding: 28, textAlign: "center", fontSize: 11, color: "var(--ink-4)" }}
            >
              Nothing to report.
            </div>
          ) : (
            sync.notifications.map((n) => (
              <button
                key={n.id}
                type="button"
                onClick={() => {
                  void sync.markRead(n.id);
                  if (n.link) {
                    setOpen(false);
                    router.push(n.link);
                  }
                }}
                style={{
                  display: "block",
                  width: "100%",
                  textAlign: "left",
                  padding: "11px 12px",
                  border: "none",
                  borderBottom: "1px solid var(--line)",
                  borderLeft: `2px solid ${n.isRead ? "transparent" : sevColor(n.severity)}`,
                  background: n.isRead ? "transparent" : "var(--bg-2)",
                  cursor: n.link ? "pointer" : "default",
                }}
              >
                <div
                  className="mono"
                  style={{
                    fontSize: 11,
                    color: n.isRead ? "var(--ink-3)" : "var(--ink)",
                    lineHeight: 1.4,
                  }}
                >
                  {n.title}
                </div>
                <div
                  className="mono"
                  style={{ fontSize: 10, color: "var(--ink-4)", marginTop: 4, lineHeight: 1.5 }}
                >
                  {n.body}
                </div>
                <div
                  className="mono uppr"
                  style={{
                    fontSize: 8,
                    letterSpacing: ".12em",
                    color: "var(--ink-4)",
                    marginTop: 5,
                  }}
                >
                  {formatWhen(n.raisedAtUtc)}
                </div>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
});

function sevColor(severity: string): string {
  if (severity === "critical") return "var(--crit)";
  if (severity === "warn") return "var(--warn)";
  return "var(--info)";
}

function formatWhen(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";

  const mins = Math.round((Date.now() - d.getTime()) / 60_000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  if (mins < 1440) return `${Math.floor(mins / 60)}h ago`;

  return d.toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}
