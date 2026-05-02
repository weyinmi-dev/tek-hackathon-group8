import { autorun, makeAutoObservable, runInAction } from "mobx";
import { api, ApiError } from "@/lib/api";
import type {
  AssistantMessageMetadata, ConversationDetail, ConversationMessage,
  ConversationSummary, CopilotAnswer, SkillTraceEntry,
} from "@/lib/types";
import { hydrate, persist } from "./persistence";
import type { AuthStore } from "./AuthStore";

const CHAT_KEY = "tp_chat_v1";

/**
 * Lightweight, UI-shaped message. Server messages map into this so the chat
 * panel only knows one shape regardless of source. <c>pending</c> marks an
 * optimistic user message awaiting confirmation; <c>error</c> marks a failed
 * assistant attempt that the user can retry.
 */
export interface ChatMessage {
  id: string;
  role: "system" | "user" | "assistant";
  content: string;
  createdAtUtc: string;
  pending?: boolean;
  error?: string;
  // Assistant only — derived from messages.metadata for replay.
  provider?: string;
  confidence?: number;
  skillTrace?: SkillTraceEntry[];
  attachments?: string[];
}

interface ChatSnapshot {
  // We persist only what we need to restore the SAME conversation on refresh:
  //   - the active conversation id (the messages themselves are re-fetched from the server)
  //   - a small recent-list cache so the sidebar shows something instantly while the
  //     fresh list loads in the background
  activeConversationId: string | null;
  recentConversations: ConversationSummary[];
}

const SYSTEM_MESSAGE: ChatMessage = {
  id: "system-banner",
  role: "system",
  content: "TelcoPilot · v1.4 · Powered by Azure OpenAI + Semantic Kernel · Context: Lagos metro NOC",
  createdAtUtc: new Date(0).toISOString(),
};

export class ChatStore {
  conversations: ConversationSummary[] = [];
  activeConversationId: string | null = null;
  messages: ChatMessage[] = [SYSTEM_MESSAGE];

  /** Per-conversation loading state — UI shows a skeleton while a switch is in flight. */
  loadingConversationId: string | null = null;
  listingLoaded = false;
  /** Live skill trace while an assistant response is in flight. Not persisted. */
  pendingTrace: SkillTraceEntry[] = [];
  sending = false;
  hasHydrated = false;

  private _disposers: Array<() => void> = [];

  constructor(private auth: AuthStore) {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  /**
   * Browser-only hydration. See AuthStore.boot — same SSR/CSR-mismatch reasoning.
   * Reading localStorage and starting autoruns must wait until after mount.
   */
  boot(): void {
    if (this.hasHydrated || typeof window === "undefined") return;

    hydrate<ChatSnapshot>(CHAT_KEY, snap => {
      this.activeConversationId = snap.activeConversationId;
      this.conversations = snap.recentConversations ?? [];
    });

    this._disposers.push(autorun(() => persist(CHAT_KEY, this.snapshot)));

    // Server hydration: when the user becomes authenticated, load the sidebar list
    // and (if a conversation was pinned) the active conversation's messages.
    this._disposers.push(autorun(() => {
      if (!this.auth.hasHydrated || !this.auth.isAuthenticated) return;
      // listingLoaded gate prevents this from looping every time the array changes.
      if (!this.listingLoaded) {
        void this.loadConversations();
      }
    }));
    this._disposers.push(autorun(() => {
      if (!this.auth.hasHydrated || !this.auth.isAuthenticated) return;
      if (this.activeConversationId && this.activeConversationId !== this.loadedConversationId) {
        void this.loadConversation(this.activeConversationId);
      }
    }));

    // When the user logs out, blow away in-memory chat state.
    this._disposers.push(autorun(() => {
      if (this.auth.hasHydrated && !this.auth.isAuthenticated && this.listingLoaded) {
        runInAction(() => this.reset());
      }
    }));

    runInAction(() => { this.hasHydrated = true; });
  }

  /** Track the most recently loaded conversation so the autorun above doesn't double-fetch. */
  private loadedConversationId: string | null = null;

  get snapshot(): ChatSnapshot {
    return {
      activeConversationId: this.activeConversationId,
      // Cap the persisted recent list so localStorage doesn't grow unbounded.
      recentConversations: this.conversations.slice(0, 25),
    };
  }

  get activeConversation(): ConversationSummary | undefined {
    return this.conversations.find(c => c.id === this.activeConversationId);
  }

  reset(): void {
    this.conversations = [];
    this.activeConversationId = null;
    this.messages = [SYSTEM_MESSAGE];
    this.loadingConversationId = null;
    this.listingLoaded = false;
    this.pendingTrace = [];
    this.loadedConversationId = null;
  }

  /** Refresh sidebar listing from the server. Background — UI keeps last cached value visible. */
  async loadConversations(): Promise<void> {
    try {
      const list = await api.listConversations();
      runInAction(() => {
        this.conversations = list;
        this.listingLoaded = true;
      });
    } catch {
      // Soft-fail — sidebar shows cached entries.
      runInAction(() => { this.listingLoaded = true; });
    }
  }

  /** Load a single conversation's messages. Replaces the active message list. */
  async loadConversation(id: string): Promise<void> {
    runInAction(() => {
      this.loadingConversationId = id;
      this.messages = [SYSTEM_MESSAGE];
    });
    try {
      const detail = await api.getConversation(id);
      runInAction(() => {
        this.messages = [SYSTEM_MESSAGE, ...detail.messages.map(toChatMessage)];
        this.activeConversationId = id;
        this.loadedConversationId = id;
      });
    } catch {
      // Stale id (deleted, or owned by another user after a session swap) — fall back
      // to a fresh session so the user is never stranded on a missing chat.
      runInAction(() => {
        this.activeConversationId = null;
        this.loadedConversationId = null;
        this.messages = [SYSTEM_MESSAGE];
      });
    } finally {
      runInAction(() => { this.loadingConversationId = null; });
    }
  }

  /** Start a fresh conversation — clears the active id; the next ask() opens one server-side. */
  newConversation(): void {
    this.activeConversationId = null;
    this.loadedConversationId = null;
    this.pendingTrace = [];
    this.messages = [SYSTEM_MESSAGE];
  }

  selectConversation(id: string | null): void {
    this.activeConversationId = id;
    this.pendingTrace = [];
    if (id) {
      void this.loadConversation(id);
    } else {
      this.messages = [SYSTEM_MESSAGE];
    }
  }

  /**
   * Send a message. Optimistically appends the user turn, kicks off the trace
   * animation, then awaits the assistant response and reconciles.
   */
  async ask(query: string): Promise<void> {
    const trimmed = query.trim();
    if (!trimmed || this.sending) return;

    const tempId = `pending-${crypto.randomUUID?.() ?? Date.now()}`;
    const optimisticUser: ChatMessage = {
      id: tempId,
      role: "user",
      content: trimmed,
      createdAtUtc: new Date().toISOString(),
      pending: true,
    };

    runInAction(() => {
      this.messages = [...this.messages, optimisticUser];
      this.sending = true;
      this.pendingTrace = [
        { skill: "IntentParser", function: "parseQuery", durationMs: 0, status: "running" },
      ];
    });

    try {
      const answer: CopilotAnswer = await api.chat(trimmed, this.activeConversationId);
      runInAction(() => {
        // Reconcile the optimistic user message with the server-issued id.
        this.messages = this.messages.map(m =>
          m.id === tempId
            ? { ...m, id: answer.userMessageId, pending: false }
            : m,
        );
        this.messages = [...this.messages, {
          id: answer.assistantMessageId,
          role: "assistant",
          content: answer.answer,
          createdAtUtc: new Date().toISOString(),
          provider: answer.provider,
          confidence: answer.confidence,
          skillTrace: answer.skillTrace,
          attachments: answer.attachments,
        }];
        this.pendingTrace = answer.skillTrace;

        // Pin the conversation id so a refresh restores this same session.
        const wasNew = !this.activeConversationId;
        this.activeConversationId = answer.conversationId;
        this.loadedConversationId = answer.conversationId;

        // Keep the sidebar in sync without a round-trip: bump or insert the row.
        if (wasNew) {
          this.conversations = [{
            id: answer.conversationId,
            title: trimmed.length > 80 ? trimmed.slice(0, 80) + "…" : trimmed,
            messageCount: 2,
            createdAtUtc: new Date().toISOString(),
            updatedAtUtc: new Date().toISOString(),
            lastMessageAtUtc: new Date().toISOString(),
          }, ...this.conversations];
        } else {
          this.conversations = this.conversations.map(c =>
            c.id === answer.conversationId
              ? { ...c, messageCount: c.messageCount + 2, updatedAtUtc: new Date().toISOString(), lastMessageAtUtc: new Date().toISOString() }
              : c,
          );
        }
      });
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Copilot is unavailable.";
      const fallback = buildFallbackAnswer(e, msg);
      runInAction(() => {
        // Mark the optimistic user message resolved (no longer pending) and append
        // an assistant error card so the user can see what went wrong + retry.
        this.messages = this.messages.map(m => m.id === tempId ? { ...m, pending: false, error: msg } : m);
        this.messages = [...this.messages, {
          id: `err-${Date.now()}`,
          role: "assistant",
          content: fallback,
          createdAtUtc: new Date().toISOString(),
          provider: "error",
          confidence: 0.1,
          skillTrace: [],
          attachments: [],
        }];
      });
    } finally {
      runInAction(() => {
        this.sending = false;
        this.pendingTrace = [];
      });
    }
  }

  async deleteConversation(id: string): Promise<void> {
    await api.deleteConversation(id);
    runInAction(() => {
      this.conversations = this.conversations.filter(c => c.id !== id);
      if (this.activeConversationId === id) {
        this.activeConversationId = null;
        this.loadedConversationId = null;
        this.messages = [SYSTEM_MESSAGE];
      }
    });
  }

  async renameConversation(id: string, title: string): Promise<void> {
    await api.renameConversation(id, title);
    runInAction(() => {
      this.conversations = this.conversations.map(c => c.id === id ? { ...c, title } : c);
    });
  }

  dispose(): void {
    this._disposers.forEach(d => d());
  }
}

// Classify a thrown ask() error into a category we can phrase usefully. The
// browser surfaces fetch failures as TypeErrors before any HTTP status is
// received, so a network outage and a backend 500 look very different even
// though they both end up here. Distinguishing them stops us telling the user
// "check docker compose logs" when their wifi is down.
type FailureCategory = "offline" | "auth" | "rate-limited" | "backend" | "unknown";

function classifyAskFailure(e: unknown): FailureCategory {
  // navigator.onLine is the single most reliable offline signal — if the OS
  // reports no connection, trust it regardless of what the error looks like.
  if (typeof navigator !== "undefined" && navigator.onLine === false) return "offline";

  if (e instanceof ApiError) {
    if (e.status === 401 || e.status === 403) return "auth";
    if (e.status === 429) return "rate-limited";
    if (e.status >= 500) return "backend";
    return "backend";
  }

  // fetch() rejects with a TypeError when the request never reaches the server
  // (DNS failure, no route, CORS preflight blocked, etc.). Browsers don't
  // standardize the message, but they all use TypeError for this class.
  if (e instanceof TypeError) return "offline";

  return "unknown";
}

function buildFallbackAnswer(e: unknown, msg: string): string {
  const category = classifyAskFailure(e);

  switch (category) {
    case "offline":
      return `ROOT CAUSE
No network connection — the browser couldn't reach the TelcoPilot API.

AFFECTED
• This Copilot turn was not sent to the server.
• Conversation history is unchanged.

RECOMMENDED ACTIONS
1. Check your internet connection and try the query again.
2. If you're on VPN, confirm the corporate network is reachable.
3. Reload the page once connectivity returns to resync session state.

CONFIDENCE
10 % — ${msg}`;

    case "auth":
      return `ROOT CAUSE
Authentication rejected by the API — your session may have expired.

AFFECTED
• This Copilot turn was not processed.
• Other read endpoints may also start returning 401.

RECOMMENDED ACTIONS
1. Sign out and back in to issue a fresh JWT.
2. If the problem persists, ask an admin to verify your account is active.

CONFIDENCE
10 % — ${msg}`;

    case "rate-limited":
      return `ROOT CAUSE
Too many requests — the Copilot endpoint is rate-limiting this client.

AFFECTED
• This Copilot turn was rejected.

RECOMMENDED ACTIONS
1. Wait ~30 seconds and retry.
2. If you're scripting requests, add backoff between asks.

CONFIDENCE
10 % — ${msg}`;

    case "backend":
      return `ROOT CAUSE
Copilot service returned an error — the backend reached the AI orchestrator and threw.

AFFECTED
• Backend chat endpoint returned an error.

RECOMMENDED ACTIONS
1. Verify the backend container is healthy (\`docker compose ps\`)
2. Check Ai:* config — Mock provider works without Azure OpenAI keys
3. Inspect logs: \`docker compose logs backend\`

CONFIDENCE
10 % — ${msg}`;

    default:
      return `ROOT CAUSE
Copilot request failed for an unrecognized reason.

AFFECTED
• This Copilot turn was not completed.

RECOMMENDED ACTIONS
1. Retry the query.
2. Check the browser console for additional detail.
3. If it persists, share the message below with the team.

CONFIDENCE
10 % — ${msg}`;
  }
}

function toChatMessage(m: ConversationMessage): ChatMessage {
  // Backend serializes MessageRole as a PascalCase string via JsonStringEnumConverter
  // (see Web.Api/Program.cs). Lower-case for comparison so we don't break if that
  // converter is ever swapped for one with a different naming policy.
  const role = String(m.role).toLowerCase();
  const roleName: ChatMessage["role"] =
    role === "user" ? "user" : role === "assistant" ? "assistant" : "system";
  const base: ChatMessage = {
    id: m.id,
    role: roleName,
    content: m.content,
    createdAtUtc: m.createdAtUtc,
  };
  if (roleName !== "assistant" || !m.metadata) return base;

  try {
    const meta = JSON.parse(m.metadata) as AssistantMessageMetadata;
    return {
      ...base,
      provider: meta.Provider,
      confidence: meta.Confidence,
      skillTrace: meta.SkillTrace,
      attachments: meta.Attachments,
    };
  } catch {
    return base;
  }
}
