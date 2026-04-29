// Unified API client. Reads/writes the access token from cookies on the server
// (Next App Router) and from localStorage on the client. All requests target the
// same /api prefix — NGINX handles routing in compose, Next rewrites handle dev.
//
// 401 handling: a single in-flight refresh attempt is shared across concurrent
// requests so a token rotation doesn't stampede the API with N parallel calls
// to /auth/refresh.

import type {
  Alert, CopilotAnswer, LoginResponse, MapResponse, MetricsResponse, AuditEntry,
  UserListItem, DocumentListItem, DocumentProvider, McpPlugin, McpInvocationResult,
  ConversationSummary, ConversationDetail,
} from "./types";

const API_BASE = "/api";

// Pluggable token provider — the AuthStore registers itself here at boot so the
// fetch wrapper can read the latest token without importing the store (and creating
// an SSR-time evaluation cycle).
type TokenProvider = () => string | null;
let getAccessToken: TokenProvider = () => {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem("tp_access");
};
type RefreshFn = () => Promise<boolean>;
let triggerRefresh: RefreshFn = async () => false;
let inflightRefresh: Promise<boolean> | null = null;

export function configureApi(opts: { getAccessToken: TokenProvider; refresh: RefreshFn }): void {
  getAccessToken = opts.getAccessToken;
  triggerRefresh = opts.refresh;
}

async function request<T>(path: string, init: RequestInit = {}, allowRefresh = true): Promise<T> {
  const method = (init.method ?? "GET").toUpperCase();
  const headers = new Headers(init.headers);
  if (!(init.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const token = getAccessToken();
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const res = await fetch(`${API_BASE}${path}`, { ...init, headers, cache: "no-store" });

  // Single shared refresh — all concurrent 401s wait on the same promise.
  if (res.status === 401 && allowRefresh) {
    inflightRefresh ??= triggerRefresh().finally(() => { inflightRefresh = null; });
    const refreshed = await inflightRefresh;
    if (refreshed) {
      return request<T>(path, init, false);
    }
  }

  if (res.status === 204) return undefined as T;
  if (!res.ok) {
    let detail = "";
    try { detail = (await res.json()).detail || ""; } catch { /* swallow */ }
    // Include path + method + token presence in the message — the stack trace alone
    // wasn't enough to diagnose 401s in production. Now you see "401 GET /chat/conversations
    // (no bearer)" or "(bearer)" right in the console.
    const tokenHint = token ? "bearer" : "no bearer";
    throw new ApiError(res.status, detail || res.statusText, method, path, tokenHint);
  }
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export class ApiError extends Error {
  public readonly status: number;
  public readonly method?: string;
  public readonly path?: string;
  public readonly tokenHint?: string;
  constructor(status: number, message: string, method?: string, path?: string, tokenHint?: string) {
    const enriched = method && path
      ? `${status} ${method} ${path} (${tokenHint}): ${message}`
      : message;
    super(enriched);
    this.name = "ApiError";
    this.status = status;
    this.method = method;
    this.path = path;
    this.tokenHint = tokenHint;
  }
}

export const api = {
  // Auth
  login: (email: string, password: string) =>
    request<LoginResponse>("/auth/login", { method: "POST", body: JSON.stringify({ email, password }) }),
  refresh: (refreshToken: string) =>
    // allowRefresh=false: refreshing the refresh token would loop on 401.
    request<LoginResponse>("/auth/refresh", { method: "POST", body: JSON.stringify({ refreshToken }) }, false),
  me: () => request<AuthUserMe>("/auth/me"),

  // User CRUD (manager+ for read/create/update; admin for delete)
  users: () => request<UserListItem[]>("/auth/users"),
  createUser: (body: {
    email: string; password: string; fullName: string; handle: string;
    role: string; team: string; region: string;
  }) => request<UserListItem>("/auth/users", { method: "POST", body: JSON.stringify(body) }),
  updateUser: (id: string, body: { fullName: string; handle: string; team: string; region: string }) =>
    request<void>(`/auth/users/${encodeURIComponent(id)}`, { method: "PUT", body: JSON.stringify(body) }),
  changeUserRole: (id: string, role: string) =>
    request<void>(`/auth/users/${encodeURIComponent(id)}/role`, { method: "PUT", body: JSON.stringify({ role }) }),
  setUserActive: (id: string, isActive: boolean) =>
    request<void>(`/auth/users/${encodeURIComponent(id)}/active`, { method: "PUT", body: JSON.stringify({ isActive }) }),
  deleteUser: (id: string) =>
    request<void>(`/auth/users/${encodeURIComponent(id)}`, { method: "DELETE" }),

  // Operations
  chat: (query: string, conversationId?: string | null) =>
    request<CopilotAnswer>("/chat", {
      method: "POST",
      body: JSON.stringify({ query, conversationId: conversationId ?? null }),
    }),
  map: () => request<MapResponse>("/map"),
  alerts: (opts: { severity?: string; active?: boolean } = {}) => {
    const q = new URLSearchParams();
    if (opts.severity) q.set("severity", opts.severity);
    if (opts.active) q.set("active", "true");
    const qs = q.toString();
    const suffix = qs ? "?" + qs : "";
    return request<Alert[]>(`/alerts${suffix}`);
  },
  ackAlert: (id: string) => request<void>(`/alerts/${encodeURIComponent(id)}/ack`, { method: "POST" }),

  // Conversations (durable chat history)
  listConversations: (take = 50) =>
    request<ConversationSummary[]>(`/chat/conversations?take=${take}`),
  getConversation: (id: string) =>
    request<ConversationDetail>(`/chat/conversations/${encodeURIComponent(id)}`),
  renameConversation: (id: string, title: string) =>
    request<void>(`/chat/conversations/${encodeURIComponent(id)}`, { method: "PATCH", body: JSON.stringify({ title }) }),
  deleteConversation: (id: string) =>
    request<void>(`/chat/conversations/${encodeURIComponent(id)}`, { method: "DELETE" }),

  // Analytics
  metrics: () => request<MetricsResponse>("/metrics"),
  audit: (take = 50) => request<AuditEntry[]>(`/metrics/audit?take=${take}`),

  // Documents
  documents: () => request<DocumentListItem[]>("/documents"),
  documentProviders: () => request<DocumentProvider[]>("/documents/providers"),
  uploadDocument: (form: FormData) =>
    request<DocumentListItem>("/documents/upload", { method: "POST", body: form }),
  linkDocument: (body: {
    title: string; fileName: string; contentType: string; sizeBytes: number;
    region?: string; tags?: string; category: string;
    source: string; storageKey: string; externalReference?: string;
  }) => request<{ id: string; title: string; source: string; status: string }>("/documents/link", { method: "POST", body: JSON.stringify(body) }),
  reindexDocument: (id: string) => request<void>(`/documents/${encodeURIComponent(id)}/reindex`, { method: "POST" }),
  deleteDocument: (id: string) => request<void>(`/documents/${encodeURIComponent(id)}`, { method: "DELETE" }),

  // MCP
  mcpPlugins: () => request<McpPlugin[]>("/mcp/plugins"),
  mcpInvoke: (body: { pluginId: string; capability: string; arguments?: Record<string, unknown>; correlationId?: string }) =>
    request<McpInvocationResult>("/mcp/invoke", { method: "POST", body: JSON.stringify(body) }),
};

export type AuthUserMe = {
  id: string;
  email: string;
  name: string;
  handle: string;
  role: string;
  team: string;
  region: string;
};
