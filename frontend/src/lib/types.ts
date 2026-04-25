// Mirrors the backend DTOs (kept hand-written for editor friendliness; tiny surface area).

export type LoginResponse = {
  accessToken: string;
  refreshToken: string;
  accessExpiresAtUtc: string;
  refreshExpiresAtUtc: string;
  user: AuthUser;
};

export type AuthUser = {
  id: string;
  email: string;
  fullName: string;
  handle: string;
  role: "engineer" | "manager" | "admin" | "viewer";
  team: string;
  region: string;
};

export type Tower = {
  id: string;       // backend "code"
  name: string;
  region: string;
  lat: number; lng: number;
  x: number; y: number;
  signal: number; load: number;
  status: "ok" | "warn" | "critical";
  issue: string | null;
};

export type RegionHealth = {
  name: string;
  towers: number;
  critical: number;
  warn: number;
  avgSignal: number;
};

export type MapResponse = {
  towers: Tower[];
  regions: RegionHealth[];
  totalTowers: number;
  onlineTowers: number;
};

export type Alert = {
  id: string;
  sev: "critical" | "warn" | "info";
  status: string;
  title: string;
  region: string;
  tower: string;
  cause: string;
  users: number;
  confidence: number;
  time: string;
};

export type Kpi = {
  label: string;
  value: string;
  unit: string;
  delta: string;
  trend: "up" | "down";
  sub: string;
};

export type SparkSeries = {
  uptime: number[];
  latency: number[];
  incident: number[];
  towers: number[];
  subs: number[];
  queries: number[];
};

export type RegionHealthMetric = { name: string; avgSignal: number; tone: "ok" | "warn" | "crit" };
export type IncidentTypeBreakdown = { type: string; count: number };

export type MetricsResponse = {
  kpis: Kpi[];
  sparks: SparkSeries;
  regions: RegionHealthMetric[];
  incidentTypes: IncidentTypeBreakdown[];
};

export type AuditEntry = {
  time: string;
  actor: string;
  role: string;
  action: string;
  target: string;
  ip: string;
};

export type SkillTraceEntry = { skill: string; function: string; durationMs: number; status: string };

export type CopilotAnswer = {
  answer: string;
  confidence: number;
  skillTrace: SkillTraceEntry[];
  attachments: string[];
  provider: string;
};

export type UserListItem = {
  id: string;
  email: string;
  fullName: string;
  handle: string;
  role: string;
  team: string;
  region: string;
  lastLoginAtUtc: string | null;
};
