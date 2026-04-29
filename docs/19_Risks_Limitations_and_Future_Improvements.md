# Risks, Limitations, and Future Improvements

This document provides an honest assessment of TelcoPilot's current limitations, acknowledged technical debt, and a prioritised roadmap for production evolution. Acknowledging limitations explicitly demonstrates architectural maturity — the ability to distinguish between what is appropriate for a hackathon demo versus what would be required for a production MTN deployment.

---

## Current Limitations

### 1. EnsureCreatedAsync Instead of EF Core Migrations

**What it means**: TelcoPilot uses `EnsureCreatedAsync()` at startup to create the database schema from EF Core model definitions, combined with seeders that insert demo data on a clean database. There are no EF Core migration files.

**Why it was done**: For a hackathon demo, `EnsureCreatedAsync()` provides fast, zero-configuration startup. You run `docker compose up --build` and the database is ready with seeded data. Creating and maintaining migration files adds iteration time with no demo benefit.

**Why it matters for production**: `EnsureCreatedAsync()` cannot evolve a schema. If a column needs to be added, a table renamed, or an index created on existing data, there is no migration to apply. The production path requires generating an EF Core baseline migration from the current model and switching to `MigrateAsync()` at startup.

**Effort to fix**: 1–2 days. `dotnet ef migrations add InitialCreate` generates the baseline. Replacing `EnsureCreatedAsync()` with `context.Database.MigrateAsync()` is a one-line change per module.

---

### 2. Stateless Copilot — No Conversation History

**What it means**: Every Copilot query is independent. If you ask "Why is Lagos West slow?" and then follow up with "What about Ikeja?", the AI has no memory of the first exchange. Each query starts a fresh Semantic Kernel kernel with no prior context.

**Why it was done**: Implementing conversation history requires persistent session management (associating a sequence of messages with a session ID), context window management (deciding how many previous turns to include), and careful prompt engineering to maintain coherent multi-turn reasoning. This was out of scope for the hackathon timeframe.

**Why it matters**: In a real NOC diagnostic session, follow-up questions are the norm. "What are the affected towers?" → "OK, what's the history of TWR-LW-003?" → "Are there any maintenance windows scheduled?" is a natural diagnostic conversation. Without history, the engineer must re-state context on every turn.

**Effort to fix**: Phase 1 roadmap item. Requires: session ID generation, `ChatHistory` storage in Redis or PostgreSQL, inclusion of previous N turns in the Semantic Kernel kernel context.

---

### 3. 30-Second Polling Instead of Server-Sent Events

**What it means**: The Dashboard and Alerts page refresh their data by calling the API every 30 seconds via a `setInterval` loop in the browser. There is no push mechanism — the server cannot notify the client of a new critical alert the moment it occurs.

**Why it was done**: 30-second polling is straightforward to implement and reliable. SSE (Server-Sent Events) requires persistent HTTP connections, server-side event emission infrastructure, and client-side reconnection handling. WebSockets add more complexity. For a demo where data changes are simulated, polling is sufficient.

**Why it matters**: For a live NOC, a 30-second lag between a tower going critical and the alert appearing on the engineer's screen could be the difference between a minor degradation and a full outage. Real-time push is a safety requirement, not a UX enhancement.

**Effort to fix**: Phase 1 roadmap item. ASP.NET Core has native SSE support via `IAsyncEnumerable`. The frontend switch from polling to `EventSource` is well-documented. Redis Pub/Sub can be the notification bus for alert state changes.

---

### 4. Hardcoded Latency Chart Data on the Insights Page

**What it means**: The 24-hour per-region latency chart on the Insights page renders from static data defined in the page component, not from an API call. The data is realistic-looking but does not change.

**Why it was done**: Generating time-series latency data requires either a time-series telemetry store (InfluxDB, Azure Monitor, TimescaleDB) or aggregated metric snapshots stored in PostgreSQL. Both are infrastructure additions beyond the demo scope.

**Why it matters**: For a manager reviewing SLA compliance, stale chart data is misleading. An executive presenting to a board should not be looking at hardcoded numbers.

**Effort to fix**: Medium effort. The simplest production path is a `MetricSnapshot` table in the Analytics schema, populated by a background job that aggregates tower metrics every 5 minutes. The chart API endpoint queries this table and the frontend renders live data.

---

### 5. No Real-Time Tower Metric Updates

**What it means**: Tower signal strength, load percentages, and status are seeded values. They do not change during a session unless manually updated in the database.

**Why it was done**: Simulating live telemetry changes requires a background job that randomly or deterministically mutates tower metrics over time — effectively a simulation engine. This was cut in favour of focusing on the AI and UX layers.

**Why it matters**: In production, tower metrics change continuously. The map and dashboard should reflect these changes. The current state creates a false impression of static network health.

**Effort to fix**: A background service (`IHostedService`) that updates tower metrics on a configurable interval would close this gap for the demo. Production would replace this with a real telemetry ingestion pipeline from MTN's NMS.

---

### 6. No Azure Key Vault Integration

**What it means**: Secrets (`JWT_SECRET`, `AZURE_OPENAI_API_KEY`, database password) are passed via environment variables from the `.env` file. This is safe for development (`.env` is in `.gitignore`) but is not the production secrets management pattern for Azure.

**Effort to fix**: Low for Azure deployments. Azure Container Apps supports Key Vault secret references natively. The application configuration is already reading from environment variables — the change is at the deployment infrastructure level, not in application code.

---

## Technical Debt Summary

| Item | Priority | Effort | Impact |
|---|---|---|---|
| EF Core migrations | High | Low | Schema evolution and production safety |
| Multi-turn Copilot | High | Medium | Core AI UX value |
| SSE for live alerts | High | Medium | Real-time operational safety |
| Live latency chart | Medium | Medium | Analytics accuracy |
| Live tower metric simulation | Medium | Low | Demo realism |
| Azure Key Vault | Medium | Low | Production security |
| Non-root container users | Medium | Low | Container security posture |
| TLS in Docker Compose | Low | Medium | Development-to-production parity |

---

## Roadmap

### Phase 1 — Production Hardening (1–2 Months)

These are the changes required to move from a demo to a production-ready system:

- Replace `EnsureCreatedAsync()` with EF Core migrations across all five module `DbContext` implementations
- Implement Server-Sent Events for real-time alert push to the frontend
- Add multi-turn conversation history to the Copilot (Redis-backed `ChatHistory` per session)
- Add Key Vault integration for production secrets
- Add live tower metric simulation background service for demo realism; wired to NMS adapter for production
- Add EF Core migration runner to the Docker Compose startup sequence

### Phase 2 — AI Intelligence Deepening (2–4 Months)

- **RAG Pipeline**: Implement the pgvector-backed document ingestion pipeline. Ingest NOC runbooks, equipment SOPs, and historical incident reports. Update `KnowledgeSkill` to perform real vector similarity search. This enables the Copilot to answer questions grounded in institutional knowledge, not just live metrics.
- **MCP Server Integration**: Replace the current Semantic Kernel skill implementation with Model Context Protocol server plugins. `NetworkMcpPlugin` and `AlertsMcpPlugin` already exist as scaffold. MCP standardises the tool-use protocol and enables third-party tool integration without custom adapters.
- **Embedding model**: Deploy an Azure OpenAI embedding model deployment and wire it to the document ingestion pipeline.

### Phase 3 — Predictive Intelligence (4–8 Months)

- **Failure prediction**: Train a lightweight ML model on historical incident data to predict tower failure probability based on signal degradation patterns, load trends, and maintenance history. Surface predictions in the Dashboard as "At-Risk Towers" panel.
- **Automated remediation workflows**: When the AI attributes high-confidence root cause to a known solvable issue (e.g., backup transport not activated), trigger an automated remediation workflow with human-in-the-loop approval.
- **Anomaly detection**: Add statistical anomaly detection to the metrics pipeline. Alerts for statistically unusual patterns that do not yet meet a threshold trigger rule.

### Phase 4 — Platform Expansion (8–12 Months)

- **Mobile NOC app**: React Native application with the same auth layer and API contract. Shift-on-call engineers receive push notifications for critical alerts and can query the Copilot from the field.
- **MTN OSS/BSS integration**: Replace the seeded Network and Alerts data with live feeds from MTN's Network Management System, Element Management System, and fault management platform via the existing `INetworkApi` and `IAlertsApi` adapter interfaces.
- **Multi-tenant architecture**: Add tenant ID columns across all schemas, tenant-scoped JWT claims, and row-level security policies in PostgreSQL. This enables TelcoPilot to serve multiple MTN regions or even multiple operators from a single deployment.
- **Audit export and SIEM integration**: Export audit entries to a SIEM system (Splunk, Microsoft Sentinel) for compliance archival and anomaly detection.

---

## Risk Matrix

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Azure OpenAI rate limiting under high Copilot query volume | Medium | Medium | Redis-cache common queries; implement request throttling per user |
| PostgreSQL schema drift if EnsureCreatedAsync used in production | High | High | Migrate to EF Core migrations before any production deployment |
| JWT secret rotation causing session invalidation | Low | Medium | Deploy short-lived tokens; notify users; rotate off-hours |
| Seeded tower data misrepresents real network state | High | Low (demo only) | Clearly communicate demo/real data split in all presentations |
| Docker image vulnerabilities in base images | Medium | Medium | Schedule regular `docker pull` + `trivy scan` in CI pipeline |
| Redis cache inconsistency after forced container restart | Low | Low | AOF persistence means cache survives graceful restarts; cold start is handled |
| MTN OSS API changes breaking the network data adapter | Medium | High | Adapter interface (`INetworkApi`) isolates impact to the Infrastructure layer only |

---

## Cross-References

- Current architecture decisions and trade-offs: [02_System_Architecture.md](02_System_Architecture.md)
- RAG and MCP pathway detail: [07_MCP_and_RAG_Architecture.md](07_MCP_and_RAG_Architecture.md)
- Enterprise readiness checklist: [14_Scalability_and_Enterprise_Readiness.md](14_Scalability_and_Enterprise_Readiness.md)
