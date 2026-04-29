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
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  lastLoginAtUtc: string | null;
};

export type DocumentSource =
  | "LocalUpload"
  | "GoogleDrive"
  | "OneDrive"
  | "SharePoint"
  | "AzureBlob";

export type IndexingStatus = "Pending" | "InProgress" | "Indexed" | "Failed";

export type DocumentListItem = {
  id: string;
  title: string;
  fileName: string;
  sizeBytes: number;
  category: string;
  region: string;
  tags: string;
  source: DocumentSource;
  status: IndexingStatus;
  version: number;
  uploadedBy: string;
  uploadedAtUtc: string;
  indexedAtUtc: string | null;
  lastIndexError: string | null;
  externalReference: string | null;
};

export type DocumentProvider = {
  source: DocumentSource;
  value: number;
  isAvailable: boolean;
};

export type McpCapabilityParameter = {
  name: string;
  type: string;
  description: string;
  required: boolean;
};

export type McpCapability = {
  name: string;
  description: string;
  parameters: McpCapabilityParameter[];
};

export type McpPlugin = {
  pluginId: string;
  displayName: string;
  kind: "Internal" | "ExternalMcpServer" | "ExternalApi";
  capabilities: McpCapability[];
};

export type McpInvocationResult = {
  pluginId: string;
  capability: string;
  isSuccess: boolean;
  output: unknown;
  error: string | null;
  durationMs: number;
  correlationId: string | null;
};
