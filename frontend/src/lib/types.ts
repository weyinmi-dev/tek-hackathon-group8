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

/**
 * OSM-derived geo context attached to every site-keyed list item the API returns.
 * Mirrors `Web.Api.Endpoints.Geo.GeoSummary`. `nearestFuelStationMetres` is null
 * when no station was found within the 15km Overpass search radius. All fields
 * are computed once per site per 24h cache TTL — safe to render directly.
 */
export type GeoSummary = {
  latitude: number;
  longitude: number;
  regionType: "urban" | "suburban" | "rural" | "remote";
  accessibilityScore: number;          // 0-100
  nearestFuelStationMetres: number | null;
  nearestFuelStationName: string | null;
  address: string | null;
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
  geo: GeoSummary | null;
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
  | "AzureBlob"
  | "WebLink";

export type IndexingStatus = "Pending" | "InProgress" | "Indexed" | "Failed" | "Rejected" | "Cancelled";

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
  rejectionReason: string | null;
  externalReference: string | null;
};

export type DocumentProvider = {
  source: DocumentSource;
  value: number;
  isAvailable: boolean;
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
  geo: GeoSummary | null;
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
  geo: GeoSummary | null;
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

// ── Energy analytics ──────────────────────────────────────────────────────────
// Mirrors EnergyMetricsResponse from /api/energy/metrics. Powers the energy panel
// on the Operations Dashboard (regional health, mix, anomaly type breakdown,
// OPEX trend, top diesel burners).

export type EnergyRegionHealthDto = {
  name: string;
  sites: number;
  critical: number;
  degraded: number;
  ok: number;
  avgUptimePct: number;
  avgBattPct: number;
  tone: "ok" | "warn" | "crit";
};

export type EnergyMixSliceDto = { source: string; pct: number };

export type EnergyAnomalyTypeBreakdownDto = { kind: string; count: number };

export type TopDieselBurnerDto = {
  siteCode: string;
  name: string;
  region: string;
  dailyDieselLitres: number;
  dailyCostNgn: number;
};

export type EnergyMetricsResponse = {
  regions: EnergyRegionHealthDto[];
  energyMix: EnergyMixSliceDto[];
  anomalyTypes: EnergyAnomalyTypeBreakdownDto[];
  opexTrend: number[];
  topBurners: TopDieselBurnerDto[];
  openAnomalies: number;
  criticalSites: number;
  fleetUptimePct: number;
  avgBatteryPct: number;
  dailyOpexNgn: number;
};

// ── Network ingestion ────────────────────────────────────────────────────────
// Mirrors Modules.Network.Application.Ingestion.Pipeline.IngestionRunSummary
// returned by POST /api/network/ingest.

export type IngestionStatus =
  | "Pending"
  | "Parsing"
  | "Analyzing"
  | "Deciding"
  | "Persisting"
  | "Projecting"
  | "Completed"
  | "Failed";

export type StageTiming = {
  stage: IngestionStatus;
  startedAt: string;
  endedAt: string;
  succeeded: boolean;
  failureReason: string | null;
};

export type IngestionRunSummary = {
  ingestionRunId: string;
  contentHash: string;
  finalStatus: IngestionStatus;
  eventsParsed: number;
  anomaliesDetected: number;
  alertsCreated: number;
  alertsUpdated: number;
  optimizationsCreated: number;
  topologyChanged: boolean;
  deduplicatedFromPriorRun: boolean;
  stageTimings: StageTiming[];
  failureReason: string | null;

  // Synchronisation report. Zeroed for a flat log upload, which syncs nothing.
  recordsCreated: number;
  recordsUpdated: number;
  recordsArchived: number;
  telemetryRowsAppended: number;
  warnings: string[];
  changes: SyncChange[];
  syncedSites: SyncedSite[];

  // File-index metadata. Nullable because a run that failed before it was persisted has none.
  fileName: string | null;
  submittedBy: string | null;
  startedAt: string | null;
  completedAt: string | null;
  durationMs: number | null;
};

/**
 * One record an upload touched. The counts say how many; these say which — the question an operator
 * actually asks when a sync does something unexpected.
 *
 * The API serialises the enum as its name, not its ordinal (the Web.Api pipeline is configured with
 * a string enum converter), so this is a string union rather than 0 | 1 | 2.
 */
export type SyncAction = "Created" | "Updated" | "Archived";

export type SyncChange = {
  entityType: string;
  entityKey: string;
  action: SyncAction;
  siteCode: string | null;
  detail: string | null;
};

/** One site an upload touched — the provenance row the FILES tab renders. */
export type SyncedSite = {
  siteCode: string;
  siteName: string;
  siteId: string;
  region: string;
  provider: string;
  environment: string;
  vendor: string | null;
  technologies: string;
  healthScore: number | null;
  requestId: string;
  snapshotVersion: number;
  generatedAt: string;
  capturedAt: string | null;
};

export type IngestionRunPage = {
  runs: IngestionRunSummary[];
  total: number;
};

// ── Site detail ────────────────────────────────────────────────────────────
// Mirrors Modules.Network.Application.Sites.SiteDetail — GET /api/network/sites/{code}.
// Every snapshot-sourced field is nullable: a seeded tower that has never received an OSS
// snapshot has a name and a status but no provider, no equipment and no readings.

export type SnapshotAlarm = {
  alarmId: string;
  severity: string;
  category: string | null;
  type: string | null;
  status: string | null;
  source: string | null;
  raisedAt: string | null;
  description: string | null;
};

export type SnapshotKpi = { name: string; value: number; unit: string | null };

export type SnapshotEnvironmental = {
  temperature: number | null;
  humidity: number | null;
  batteryVoltage: number | null;
  generatorFuelPercent: number | null;
  generatorRunning: boolean | null;
  mainPowerAvailable: boolean | null;
  airConditionerStatus: string | null;
  doorOpen: boolean | null;
  smokeDetected: boolean | null;
};

export type SnapshotPerformance = {
  measurementInterval: string | null;
  capturedAt: string | null;
  availabilityPercent: number | null;
  connectedUsers: number | null;
  activeVoiceCalls: number | null;
  activeDataSessions: number | null;
  downlinkTrafficGb: number | null;
  uplinkTrafficGb: number | null;
  averageDownlinkMbps: number | null;
  averageUplinkMbps: number | null;
  packetLossPercent: number | null;
  latencyMs: number | null;
  callSetupSuccessRate: number | null;
  callDropRate: number | null;
  handoverSuccessRate: number | null;
  cellUtilizationPercent: number | null;
  kpis: SnapshotKpi[];
};

export type SiteEquipment = {
  equipmentId: string;
  type: string;
  model: string | null;
  status: string | null;
  isActive: boolean;
  lastSeenAtUtc: string;
  retiredAtUtc: string | null;
};

export type SiteTicket = {
  ticketId: string;
  status: "Open" | "Completed" | "Archived";
  priority: string | null;
  issue: string | null;
  engineerId: string | null;
  engineerName: string | null;
  createdAt: string | null;
  estimatedArrival: string | null;
  completedAt: string | null;
  completedAction: string | null;
};

export type SiteDetail = {
  siteCode: string;
  name: string;
  region: string;
  statusWire: string;
  signalPct: number;
  loadPct: number;
  issue: string | null;
  latitude: number;
  longitude: number;
  updatedAtUtc: string;

  provider: string | null;
  environment: string | null;
  vendor: string | null;
  siteId: string | null;
  technologies: string[];
  healthScore: number | null;
  lastSynchronisedAt: string | null;
  lastHeartbeat: string | null;
  snapshotVersion: number | null;

  environmental: SnapshotEnvironmental | null;
  performance: SnapshotPerformance | null;
  activeAlarms: SnapshotAlarm[];

  equipment: SiteEquipment[];
  tickets: SiteTicket[];
  lastMaintenanceDate: string | null;
  nextScheduledMaintenance: string | null;
};

// ── Telemetry ──────────────────────────────────────────────────────────────
// Every metric is nullable on purpose: a feed may omit any of them, and a gap in a series is
// information. Charts must skip nulls rather than plotting them as zero.

export type SiteTelemetryPoint = {
  at: string;
  healthScore: number | null;
  signalPct: number | null;
  loadPct: number | null;
  latencyMs: number | null;
  temperatureC: number | null;
  humidityPct: number | null;
  batteryPct: number | null;
  dieselPct: number | null;
  gridUp: boolean | null;
  downlinkTrafficGb: number | null;
  uplinkTrafficGb: number | null;
  connectedUsers: number | null;
  availabilityPercent: number | null;
  packetLossPercent: number | null;
  rsrp: number | null;
  sinr: number | null;
  prbUtilization: number | null;
  openAlarmCount: number;
};

export type SiteTelemetry = {
  siteCode: string;
  hours: number;
  points: SiteTelemetryPoint[];
};

// ── Notifications ──────────────────────────────────────────────────────────

export type NotificationKind =
  | "CriticalAlarm"
  | "UploadCompleted"
  | "SynchronizationFailed"
  | "HealthDegraded"
  | "PredictionChanged";

export type AppNotification = {
  id: string;
  kind: NotificationKind;
  severity: "info" | "warn" | "critical";
  title: string;
  body: string;
  siteCode: string | null;
  link: string | null;
  raisedAtUtc: string;
  isRead: boolean;
};

export type NotificationFeed = {
  items: AppNotification[];
  unreadCount: number;
};
