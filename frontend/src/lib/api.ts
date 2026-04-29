// Unified API client. Reads/writes the access token from cookies on the server
// (Next App Router) and from localStorage on the client. All requests target the
// same /api prefix — NGINX handles routing in compose, Next rewrites handle dev.

import type {
  Alert, CopilotAnswer, LoginResponse, MapResponse, MetricsResponse, AuditEntry,
  UserListItem, DocumentListItem, DocumentProvider, McpPlugin, McpInvocationResult,
} from "./types";

type Json = Record<string, unknown> | unknown[] | string | number | boolean | null;

const API_BASE = "/api";

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  // Don't override Content-Type if the body is FormData — let the browser set the multipart boundary.
  if (!(init.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  // Browser-side: pull token from localStorage. Server components shouldn't
  // call this directly; use the server helpers below.
  if (typeof window !== "undefined") {
    const token = window.localStorage.getItem("tp_access");
    if (token) headers.set("Authorization", `Bearer ${token}`);
  }

  const res = await fetch(`${API_BASE}${path}`, { ...init, headers, cache: "no-store" });
  if (res.status === 204) return undefined as T;
  if (!res.ok) {
    let detail = "";
    try { detail = (await res.json()).detail || ""; } catch { /* swallow */ }
    throw new ApiError(res.status, detail || res.statusText);
  }
  // No body? (some endpoints return 200 with empty body)
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message); this.name = "ApiError"; }
}

export const api = {
  // Auth
  login: (email: string, password: string) =>
    request<LoginResponse>("/auth/login", { method: "POST", body: JSON.stringify({ email, password }) }),
  refresh: (refreshToken: string) =>
    request<LoginResponse>("/auth/refresh", { method: "POST", body: JSON.stringify({ refreshToken }) }),
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
  chat: (query: string) =>
    request<CopilotAnswer>("/chat", { method: "POST", body: JSON.stringify({ query }) }),
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
