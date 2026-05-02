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
  assignedTeam: string | null;
  dispatchTarget: string | null;
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

export type RegionLatencySeries = { name: string; color: string; series: number[] };
export type TopCopilotQuery = { query: string; count: number };

export type MetricsResponse = {
  kpis: Kpi[];
  sparks: SparkSeries;
  regions: RegionHealthMetric[];
  incidentTypes: IncidentTypeBreakdown[];
  regionLatency: RegionLatencySeries[];
  topQueries: TopCopilotQuery[];
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
  conversationId: string;
  userMessageId: string;
  assistantMessageId: string;
};

// Mirrors Modules.Ai.Domain.Conversations.MessageRole. The C# enum is `int`-backed,
// but Web.Api registers a global JsonStringEnumConverter (Program.cs), so the wire
// format is the PascalCase enum name — not the numeric value. Matching the wire
// format here keeps toChatMessage honest; comparing role === 1 was silently
// falling through to "system" for every rehydrated message after a refresh.
export type MessageRole = "System" | "User" | "Assistant" | "Tool";
export const MessageRoleName: Record<MessageRole, "system" | "user" | "assistant" | "tool"> = {
  System: "system",
  User: "user",
  Assistant: "assistant",
  Tool: "tool",
};

// Shape stored in messages.metadata for assistant turns — see MessageMetadata in
// Modules.Ai.Application.Copilot.AskCopilot.AskCopilotCommandHandler.
export type AssistantMessageMetadata = {
  Provider: string;
  Confidence: number;
  SkillTrace: SkillTraceEntry[];
  Attachments: string[];
};

export type ConversationSummary = {
  id: string;
  title: string;
  messageCount: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  lastMessageAtUtc: string | null;
};

export type ConversationMessage = {
  id: string;
  role: MessageRole;
  content: string;
  metadata: string | null;
  createdAtUtc: string;
};

export type ConversationDetail = {
  id: string;
  title: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  messages: ConversationMessage[];
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

// ── Energy module ──────────────────────────────────────────────────────────────
// Mirrors the DTOs returned by /api/energy/*. Field names match what the Energy
// pages already consume (was hardcoded in lib/energy-data.ts before phase 2).

export type EnergySiteDto = {
  id: string;          // tower / site code
  name: string;
  region: string;
  source: "grid" | "generator" | "battery" | "solar";
  battPct: number;
  dieselPct: number;
  solarKw: number;
  gridUp: boolean;
  dailyDieselLitres: number;
  costNgn: number;
  uptimePct: number;
  solar: boolean;       // has solar at all?
  health: "ok" | "degraded" | "critical";
  anomaly: string | null;
};

export type EnergyKpiDto = {
  label: string;
  value: string;
  unit: string;
  delta: string;
  trend: "up" | "down";
  sub: string;
};

export type EnergyAnomalyDto = {
  id: string;
  site: string;
  kind: "fuel-theft" | "sensor-offline" | "gen-overuse" | "battery-degrade" | "predicted-fault";
  sev: "critical" | "warn" | "info";
  t: string;            // HH:mm
  detail: string;
  conf: number;         // 0-1
  model: string;
  acknowledged: boolean;
};

export type DieselTracePoint = { at: string; dieselPct: number; litresDelta: number };

export type EnergyMixSlice = { source: string; pct: number };

export type OptimizationProjection = {
  baselineDailyOpexMillionsNgn: number;
  optimizedDailyOpexMillionsNgn: number;
  dailySavingsMillionsNgn: number;
  annualSavingsBillionsNgn: number;
  dieselReductionPct: number;
  co2AvoidedTonnesPerYear: number;
  baselineSeries: number[];
  optimizedSeries: number[];
  energyMix: EnergyMixSlice[];
};

export type EnergyRecommendation = {
  title: string;
  detail: string;
  tone: "accent" | "warn" | "info";
  estimatedDailySavingsNgn: number;
};
