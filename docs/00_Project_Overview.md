# TelcoPilot — Project Overview

> **"Stop digging through logs. Ask the network."**

TelcoPilot is an AI-native Network Operations Center (NOC) platform purpose-built for MTN Nigeria's Lagos metro operations. It replaces the traditional pattern of engineers manually correlating dashboards, log files, and tribal knowledge with a single intelligent interface that reasons over live network data, historical incident reports, and operational runbooks — and responds in plain language.

---

## Vision

Nigerian telecoms are among the most operationally complex in sub-Saharan Africa. The Lagos metro alone spans 1,200+ towers serving millions of subscribers across eight distinct regions, each with its own congestion patterns, infrastructure age, and civil-works exposure. When a tower goes critical, the lag between event and mitigation is measured in minutes of lost subscriber experience.

TelcoPilot's vision is to collapse the mean time to identify and respond to network anomalies by giving every NOC engineer — regardless of seniority — access to the same diagnostic intelligence that a senior engineer builds over years of pattern recognition.

---

## The Problem It Solves

Lagos NOC engineers currently face:

- **Log fragmentation**: telemetry lives in siloed systems — radio, transport, core, and field ticket tools are separate interfaces
- **Manual correlation**: determining whether a Lekki slowdown is a fiber cut, congestion, or power failure requires querying multiple systems and applying pattern memory
- **Alert fatigue**: dozens of alerts per shift, many of them noise, with no automated triage or root-cause hypothesis
- **Knowledge loss**: diagnostic expertise lives in senior engineers' heads, not in a queryable system
- **No unified audit trail**: compliance and post-incident review are manual, reconstruction-based processes

---

## The Solution

TelcoPilot delivers a unified, AI-native NOC platform with five integrated capabilities:

| Capability | What it delivers |
|---|---|
| **AI Copilot** | Natural-language network diagnostics powered by Azure OpenAI + Semantic Kernel |
| **Live Network Map** | Canvas-rendered tower topology with real-time status, region health, and Best Signal Zones |
| **Intelligent Alerts** | AI-attributed root-cause hypotheses, confidence scores, and in-line acknowledgment |
| **Executive Analytics** | KPI strip, sparklines, incident type distribution, SLA compliance, and regional health breakdown |
| **Full Audit Trail** | Every Copilot query and admin action recorded, timestamped, and role-attributed |

---

## Key Capabilities Summary

### AI-Native Intelligence
- Natural language queries: *"Why is Lagos West slow?"* → structured ROOT CAUSE / AFFECTED / RECOMMENDED ACTIONS / CONFIDENCE answer
- Azure OpenAI GPT-4o-mini with Semantic Kernel automatic function calling
- 5 Kernel plugins (DiagnosticsSkill, OutageSkill, RecommendationSkill, KnowledgeSkill, InternalToolsSkill)
- RAG pipeline backed by pgvector — ingests incident reports, SOPs, and tower performance history
- MCP-style plugin architecture for extensible tool integration
- MockCopilotOrchestrator for full demo operation without Azure OpenAI credentials
- Every query persisted as ChatLog + AuditEntry

### Network Operations
- 15 seeded Lagos metro towers across 8 regions with realistic signal/load/status data
- TowerStatus derived from live metrics: Critical (signal < 40% OR load > 90% OR incident), Warn, Ok
- Map with canvas rendering, tower selection, hover detail, and Best Signal Zones panel
- Cross-module INetworkApi contract keeps the AI layer decoupled from network data storage

### Security and Compliance
- JWT bearer authentication with HMAC-SHA256, 30-second clock skew tolerance
- Rotating refresh tokens — raw token never stored, only SHA-256 hash
- BCrypt password hashing at cost factor 11
- 4-role RBAC: Viewer, Engineer, Manager, Admin — enforced at API layer (ASP.NET policies) and UI layer
- Full audit trail: actor, role, action, target, timestamp, source IP on every significant operation
- Secrets via environment variables; user-secrets for local dev; never committed to source control

### Enterprise Architecture
- Modular monolith: 5 in-process modules (Identity, Network, Alerts, Analytics, AI) sharing a single transaction boundary
- CQRS with MediatR — commands and queries separated, pipeline behaviors applied uniformly
- Result<T> monad — no uncaught exceptions surface from handlers
- 4-layer pipeline: ExceptionHandling → Logging → Validation → QueryCaching
- Per-module PostgreSQL schemas (5 schemas, 1 database) with EF Core and snake_case naming
- Redis caching for high-frequency read queries (map: 15s TTL, metrics: configurable)
- Docker Compose: 5-service stack deployable from a single `docker compose up --build`

### Frontend UX
- Next.js 15 + React 19 (App Router) with TypeScript
- Dark-first CSS variable theme system with light/dark toggle
- 8 pages: Login, Dashboard (Command Center), Copilot, Alerts, Map, Insights, Audit, Users
- Skill trace animation: live agent panel shows which Semantic Kernel plugins were invoked
- Answer formatting: tower codes (TWR-*) and incident IDs (INC-*) highlighted inline
- 30-second polling refresh; NGINX reverse proxy with single upstream invariant

---

## Value Proposition

| Stakeholder | What TelcoPilot delivers |
|---|---|
| **NOC Engineer** | Ask the network in plain language; get root cause, affected towers, and concrete actions in seconds |
| **NOC Manager** | Real-time KPI strip, audit trail, and team activity — full situational awareness without log-diving |
| **Platform Admin** | RBAC management, user lifecycle, full audit compliance |
| **Executive** | SLA compliance gauge, incident trend sparklines, regional health breakdown |
| **MTN Nigeria Infra Team** | Docker-native, cloud-ready, stateless JWT — fits existing Azure/container estate |

---

## Thematic Area Coverage Checklist

### Core Platform
- [x] Executive Dashboard with real-time KPIs
- [x] Analytics — sparklines, regional health, incident type distribution, SLA compliance
- [x] User management — create, update, delete, activate/deactivate users
- [x] RBAC — 4 roles, policy-enforced at API and UI layers
- [x] Authentication — JWT bearer, refresh token rotation, BCrypt hashing

### AI-Native Intelligence
- [x] Natural language interface — ask in plain English, get structured answers
- [x] AI Copilot with Azure OpenAI GPT-4o-mini + Semantic Kernel
- [x] Network diagnostics via DiagnosticsSkill (live tower/region metrics)
- [x] Outage alerts with AI-attributed root-cause hypotheses (OutageSkill)
- [x] Connectivity recommendations with runbook-backed actions (RecommendationSkill)
- [x] RAG pipeline — historical incident reports, SOPs, tower performance indexed via pgvector
- [x] KnowledgeSkill — retrieves relevant historical context during Copilot answers
- [x] MCP-style plugin architecture (NetworkMcpPlugin, AlertsMcpPlugin, IMcpPluginRegistry)

### Security
- [x] Secure login with JWT + BCrypt
- [x] RBAC enforced on all protected endpoints
- [x] Secure API — Bearer tokens, CORS whitelist, no raw secrets in code
- [x] Secrets management — env vars, user-secrets, `.env.example` pattern
- [x] Audit trail — every Copilot query, admin action, and role change logged

### Data and Infrastructure
- [x] PostgreSQL 17 with pgvector extension for vector similarity search
- [x] Redis 7 for query result caching
- [x] Azure OpenAI integration (gpt-4o-mini for chat, configurable embedding deployment)
- [x] Semantic Kernel 1.74 with automatic function calling
- [x] Intelligent workflows — orchestrated multi-skill AI reasoning pipeline
- [x] RAG indexing pipeline — document ingestion, text chunking, embedding generation, vector storage

### Enterprise Readiness
- [x] Docker Compose — 5-service stack, single-command deploy
- [x] NGINX reverse proxy — single public port (80), `/api/*` and `/` routing
- [x] Multi-stage Dockerfiles — minimal runtime images
- [x] .NET Aspire AppHost — local development orchestration
- [x] Structured logging with Serilog + OpenTelemetry via ServiceDefaults
- [x] Health check endpoint at `/health`
- [x] Error handling — ExceptionHandlingPipelineBehavior + GlobalExceptionHandler + Result<T> monad
- [x] Cloud-first: stateless JWT, Redis-backed caching, container-native deployment

---

## Document Index

| File | Contents |
|---|---|
| [01_Business_Problem_and_Solution.md](01_Business_Problem_and_Solution.md) | Business context, impact, ROI narrative |
| [02_System_Architecture.md](02_System_Architecture.md) | High-level architecture, Mermaid diagrams |
| [03_Technology_Stack.md](03_Technology_Stack.md) | Full technology table with rationale |
| [04_Backend_Architecture.md](04_Backend_Architecture.md) | CQRS, pipeline behaviors, Result<T>, module contracts |
| [05_Frontend_Architecture.md](05_Frontend_Architecture.md) | Next.js App Router, auth, RBAC, components |
| [06_AI_and_Intelligence_Architecture.md](06_AI_and_Intelligence_Architecture.md) | Semantic Kernel, skills, orchestrators, SkillTrace |
| [07_MCP_and_RAG_Architecture.md](07_MCP_and_RAG_Architecture.md) | MCP plugin layer, RAG pipeline, pgvector |
| [08_Database_and_Data_Flow.md](08_Database_and_Data_Flow.md) | Schema isolation, EF Core, Redis caching, data flow |
| [09_Authentication_and_Security.md](09_Authentication_and_Security.md) | JWT, refresh rotation, BCrypt, CORS, secrets |
| [10_User_Roles_and_RBAC.md](10_User_Roles_and_RBAC.md) | Role definitions, permission matrix, enforcement |
| [11_Executive_Dashboard_and_Analytics.md](11_Executive_Dashboard_and_Analytics.md) | KPIs, sparklines, audit trail, compliance |
| [12_API_Documentation.md](12_API_Documentation.md) | All endpoints, request/response shapes, auth |
| [13_Infrastructure_and_Deployment.md](13_Infrastructure_and_Deployment.md) | Docker Compose, NGINX, Dockerfiles, env vars |
| [14_Scalability_and_Enterprise_Readiness.md](14_Scalability_and_Enterprise_Readiness.md) | Scaling path, Redis, stateless JWT, adoption checklist |
| [15_Error_Handling_Logging_and_Monitoring.md](15_Error_Handling_Logging_and_Monitoring.md) | Pipeline behaviors, Serilog, OpenTelemetry, health |
| [16_UI_UX_and_User_Flows.md](16_UI_UX_and_User_Flows.md) | Design philosophy, page UX, Copilot UX, theme |
| [17_Project_Demo_Guide.md](17_Project_Demo_Guide.md) | Step-by-step demo script, scenarios, tips |
| [18_Presentation_Talking_Points.md](18_Presentation_Talking_Points.md) | Elevator pitch, judge Q&A, thematic mapping |
| [19_Risks_Limitations_and_Future_Improvements.md](19_Risks_Limitations_and_Future_Improvements.md) | Honest limitations, roadmap, production path |
| [20_Setup_and_Local_Development.md](20_Setup_and_Local_Development.md) | Prerequisites, Docker Compose, Aspire, troubleshooting |
