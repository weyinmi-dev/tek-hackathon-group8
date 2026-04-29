"use client";

import { useState } from "react";
import { observer } from "mobx-react-lite";
import { useChatStore } from "@/lib/stores/StoreProvider";
import { Btn } from "@/components/UI";

/**
 * Sidebar listing of past conversations for the signed-in user. Selecting an
 * entry calls into the ChatStore which fetches the messages and pins the
 * conversation id so a refresh restores the same session.
 */
export const ConversationsSidebar = observer(function ConversationsSidebar() {
  const chat = useChatStore();
  const [renaming, setRenaming] = useState<string | null>(null);
  const [draftTitle, setDraftTitle] = useState("");

  return (
    <aside style={{
      borderRight: "1px solid var(--line)", background: "var(--bg-1)",
      display: "flex", flexDirection: "column", height: "100%", minWidth: 0,
    }}>
      <div style={{
        padding: "14px 14px 10px", borderBottom: "1px solid var(--line)",
        display: "flex", alignItems: "center", justifyContent: "space-between",
      }}>
        <div className="mono uppr" style={{ fontSize: 10, color: "var(--ink-3)", letterSpacing: ".12em" }}>SESSIONS</div>
        <Btn small primary onClick={() => chat.newConversation()}>+ New</Btn>
      </div>

      <div style={{ flex: 1, overflowY: "auto", padding: "8px 0" }}>
        {!chat.listingLoaded && chat.conversations.length === 0 && (
          <div style={{ padding: 14, color: "var(--ink-3)", fontSize: 11.5 }} className="mono">
            ⌁ loading sessions…
          </div>
        )}
        {chat.listingLoaded && chat.conversations.length === 0 && (
          <div style={{ padding: 14, color: "var(--ink-3)", fontSize: 11.5 }} className="mono">
            No saved sessions yet. Ask the Copilot to start one.
          </div>
        )}
        {chat.conversations.map(c => {
          const active = chat.activeConversationId === c.id;
          if (renaming === c.id) {
            return (
              <div key={c.id} style={{ padding: "8px 12px" }}>
                <input
                  autoFocus
                  value={draftTitle}
                  onChange={e => setDraftTitle(e.target.value)}
                  onBlur={async () => {
                    if (draftTitle.trim() && draftTitle !== c.title) {
                      await chat.renameConversation(c.id, draftTitle.trim());
                    }
                    setRenaming(null);
                  }}
                  onKeyDown={async e => {
                    if (e.key === "Enter") (e.target as HTMLInputElement).blur();
                    if (e.key === "Escape") setRenaming(null);
                  }}
                  style={{
                    width: "100%", padding: "6px 8px", borderRadius: 5,
                    border: "1px solid var(--accent-line)", background: "var(--bg-2)",
                    color: "var(--ink)", fontSize: 12, fontFamily: "var(--sans)", outline: "none",
                  }}
                />
              </div>
            );
          }
          return (
            <button
              key={c.id}
              onClick={() => chat.selectConversation(c.id)}
              onDoubleClick={() => { setRenaming(c.id); setDraftTitle(c.title); }}
              style={{
                appearance: "none", width: "100%", textAlign: "left",
                padding: "10px 12px", border: "none", cursor: "pointer",
                background: active ? "var(--bg-2)" : "transparent",
                borderLeft: "3px solid " + (active ? "var(--accent)" : "transparent"),
                color: "var(--ink)", display: "flex", flexDirection: "column", gap: 4,
              }}
            >
              <div style={{
                fontSize: 12.5, fontWeight: active ? 500 : 400, color: "var(--ink)",
                whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis",
              }}>{c.title}</div>
              <div style={{
                display: "flex", justifyContent: "space-between",
                fontSize: 10, color: "var(--ink-3)", fontFamily: "var(--mono)",
              }}>
                <span>{c.messageCount} turns</span>
                <span>{relativeTime(c.lastMessageAtUtc ?? c.updatedAtUtc)}</span>
              </div>
              {active && (
                <div style={{ display: "flex", gap: 6, marginTop: 4 }}>
                  <button
                    onClick={e => { e.stopPropagation(); setRenaming(c.id); setDraftTitle(c.title); }}
                    style={miniBtn}
                  >rename</button>
                  <button
                    onClick={async e => {
                      e.stopPropagation();
                      if (window.confirm(`Delete "${c.title}"?`)) await chat.deleteConversation(c.id);
                    }}
                    style={{ ...miniBtn, color: "var(--crit)" }}
                  >delete</button>
                </div>
              )}
            </button>
          );
        })}
      </div>
    </aside>
  );
});

const miniBtn: React.CSSProperties = {
  appearance: "none", border: "1px solid var(--line-2)", background: "var(--bg-3)",
  color: "var(--ink-3)", padding: "2px 6px", borderRadius: 4,
  fontSize: 10, fontFamily: "var(--mono)", cursor: "pointer",
};

function relativeTime(iso: string): string {
  const then = new Date(iso).getTime();
  const diff = Date.now() - then;
  const m = Math.floor(diff / 60_000);
  if (m < 1) return "just now";
  if (m < 60) return `${m}m`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h`;
  const d = Math.floor(h / 24);
  if (d < 7) return `${d}d`;
  return new Date(iso).toLocaleDateString();
}
