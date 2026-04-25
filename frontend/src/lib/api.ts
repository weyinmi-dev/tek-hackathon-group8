// Unified API client. Reads/writes the access token from cookies on the server
// (Next App Router) and from localStorage on the client. All requests target the
// same /api prefix — NGINX handles routing in compose, Next rewrites handle dev.

import type { Alert, CopilotAnswer, LoginResponse, MapResponse, MetricsResponse, AuditEntry, UserListItem } from "./types";

type Json = Record<string, unknown> | unknown[] | string | number | boolean | null;

const API_BASE = "/api";

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Content-Type", "application/json");

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
  return res.json() as Promise<T>;
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
  users: () => request<UserListItem[]>("/auth/users"),

  // Operations
  chat: (query: string) =>
    request<CopilotAnswer>("/chat", { method: "POST", body: JSON.stringify({ query }) }),
  map: () => request<MapResponse>("/map"),
  alerts: (opts: { severity?: string; active?: boolean } = {}) => {
    const q = new URLSearchParams();
    if (opts.severity) q.set("severity", opts.severity);
    if (opts.active) q.set("active", "true");
    const qs = q.toString();
    return request<Alert[]>(`/alerts${qs ? `?${qs}` : ""}`);
  },
  ackAlert: (id: string) => request<void>(`/alerts/${encodeURIComponent(id)}/ack`, { method: "POST" }),

  // Analytics
  metrics: () => request<MetricsResponse>("/metrics"),
  audit: (take = 50) => request<AuditEntry[]>(`/metrics/audit?take=${take}`),
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
