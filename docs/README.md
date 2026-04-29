# TelcoPilot Documentation

> **"Stop digging through logs. Ask the network."**

TelcoPilot is an AI-native Network Operations Center (NOC) platform built for MTN Nigeria's Lagos metro operations. This documentation suite covers the complete system: architecture, AI design, infrastructure, API reference, user guide, and presentation materials.

---

## Quick Navigation

**Not sure where to start?** Use this guide to find the right document for your role.

| I am a... | Start with |
|---|---|
| **Judge / Evaluator** | [18_Presentation_Talking_Points.md](18_Presentation_Talking_Points.md) → [17_Project_Demo_Guide.md](17_Project_Demo_Guide.md) → [00_Project_Overview.md](00_Project_Overview.md) |
| **Developer setting up locally** | [20_Setup_and_Local_Development.md](20_Setup_and_Local_Development.md) → [04_Backend_Architecture.md](04_Backend_Architecture.md) |
| **Solutions Architect** | [02_System_Architecture.md](02_System_Architecture.md) → [14_Scalability_and_Enterprise_Readiness.md](14_Scalability_and_Enterprise_Readiness.md) → [13_Infrastructure_and_Deployment.md](13_Infrastructure_and_Deployment.md) |
| **Security Reviewer** | [09_Authentication_and_Security.md](09_Authentication_and_Security.md) → [10_User_Roles_and_RBAC.md](10_User_Roles_and_RBAC.md) |
| **Frontend Developer** | [05_Frontend_Architecture.md](05_Frontend_Architecture.md) → [16_UI_UX_and_User_Flows.md](16_UI_UX_and_User_Flows.md) |
| **AI / ML Engineer** | [06_AI_and_Intelligence_Architecture.md](06_AI_and_Intelligence_Architecture.md) → [07_MCP_and_RAG_Architecture.md](07_MCP_and_RAG_Architecture.md) |
| **API Consumer** | [12_API_Documentation.md](12_API_Documentation.md) |
| **NOC Operator** | [16_UI_UX_and_User_Flows.md](16_UI_UX_and_User_Flows.md) → [17_Project_Demo_Guide.md](17_Project_Demo_Guide.md) |

---

## Full Document Index

### Foundation

| File | Description |
|---|---|
| [00_Project_Overview.md](00_Project_Overview.md) | Vision, capabilities summary, value proposition, and thematic coverage checklist |
| [01_Business_Problem_and_Solution.md](01_Business_Problem_and_Solution.md) | The operational pain points in a Lagos NOC, the business case for TelcoPilot, and the ROI narrative |
| [02_System_Architecture.md](02_System_Architecture.md) | High-level architecture, modular monolith rationale, request lifecycle, and full container topology diagrams |
| [03_Technology_Stack.md](03_Technology_Stack.md) | Complete technology table with selection rationale for every framework, library, and service |

### Backend

| File | Description |
|---|---|
| [04_Backend_Architecture.md](04_Backend_Architecture.md) | Module layout, CQRS with MediatR, pipeline behaviors, Result<T> pattern, cross-module API contracts |
| [08_Database_and_Data_Flow.md](08_Database_and_Data_Flow.md) | Five-schema PostgreSQL design, EF Core configuration, Redis caching strategy, and end-to-end data flow |
| [09_Authentication_and_Security.md](09_Authentication_and_Security.md) | JWT bearer authentication, refresh token rotation, BCrypt hashing, CORS, and secrets management |
| [15_Error_Handling_Logging_and_Monitoring.md](15_Error_Handling_Logging_and_Monitoring.md) | Pipeline behavior chain, Result<T> monad, Serilog structured logging, OpenTelemetry, health checks |

### AI and Intelligence

| File | Description |
|---|---|
| [06_AI_and_Intelligence_Architecture.md](06_AI_and_Intelligence_Architecture.md) | Semantic Kernel orchestration, three-skill architecture, ICopilotOrchestrator abstraction, SkillTrace |
| [07_MCP_and_RAG_Architecture.md](07_MCP_and_RAG_Architecture.md) | MCP plugin registry design, RAG pipeline pathway, pgvector integration, document ingestion |

### Frontend

| File | Description |
|---|---|
| [05_Frontend_Architecture.md](05_Frontend_Architecture.md) | Next.js 15 App Router structure, auth context, RBAC, API client layer, component organisation |
| [16_UI_UX_and_User_Flows.md](16_UI_UX_and_User_Flows.md) | Design philosophy, CSS theme tokens, page-by-page UX walkthrough, primary engineer workflow |

### Access Control

| File | Description |
|---|---|
| [10_User_Roles_and_RBAC.md](10_User_Roles_and_RBAC.md) | Four-role model, full permission matrix, ASP.NET Core policy enforcement, frontend gating |
| [11_Executive_Dashboard_and_Analytics.md](11_Executive_Dashboard_and_Analytics.md) | Every KPI card, sparklines, p95 latency chart, SLA compliance donut, audit trail governance value |

### API Reference

| File | Description |
|---|---|
| [12_API_Documentation.md](12_API_Documentation.md) | All 10 endpoints with method, path, auth, request/response shapes, error codes, and curl examples |

### Infrastructure and Operations

| File | Description |
|---|---|
| [13_Infrastructure_and_Deployment.md](13_Infrastructure_and_Deployment.md) | Docker Compose service reference, NGINX config, multi-stage Dockerfiles, environment variables, Aspire, cloud pathway |
| [14_Scalability_and_Enterprise_Readiness.md](14_Scalability_and_Enterprise_Readiness.md) | Horizontal scaling design, Redis caching strategy, health checks, module extraction path, enterprise checklist |

### Getting Started and Demo

| File | Description |
|---|---|
| [20_Setup_and_Local_Development.md](20_Setup_and_Local_Development.md) | Prerequisites, Docker Compose quickstart, Aspire dev setup, Azure OpenAI configuration, troubleshooting |
| [17_Project_Demo_Guide.md](17_Project_Demo_Guide.md) | Five demo scenarios with step-by-step scripts, account selection guide, demo tips, reset instructions |

### Presentation and Planning

| File | Description |
|---|---|
| [18_Presentation_Talking_Points.md](18_Presentation_Talking_Points.md) | Elevator pitch, problem framing, technical differentiators, thematic coverage checklist, judge Q&A answers |
| [19_Risks_Limitations_and_Future_Improvements.md](19_Risks_Limitations_and_Future_Improvements.md) | Honest current limitations, technical debt, four-phase roadmap, risk matrix |

---

## The 30-Second Version

TelcoPilot replaces the manual, multi-tool process of diagnosing network anomalies with a single natural language interface. Type "Why is Lagos West slow?" and get a root cause, affected towers, and prioritised actions in plain English — powered by Azure OpenAI GPT-4o-mini and Semantic Kernel.

Built on a .NET 10 modular monolith with CQRS, PostgreSQL 17, Redis 7, and Next.js 15. Deployed with `docker compose up --build`. Ready for Azure Container Apps.

---

## Repository Structure

```
tek-hackathon-group8/
├── src/                    # .NET backend (modular monolith)
│   ├── AppHost/            # .NET Aspire AppHost for local dev
│   ├── WebApi/             # ASP.NET Core entry point
│   ├── SharedKernel/       # Result<T>, Entity, Error types
│   └── Modules/
│       ├── Identity/       # Auth, users, JWT, refresh tokens
│       ├── Network/        # Towers, map, region health
│       ├── Alerts/         # Incidents, acknowledgment
│       ├── Analytics/      # Metrics, KPIs, audit trail
│       └── AI/             # Copilot, Semantic Kernel, RAG, MCP
├── frontend/               # Next.js 15 application
│   └── src/app/            # App Router pages (login, dashboard, copilot, ...)
├── docs/                   # This documentation suite
├── docker-compose.yml      # Full stack (5 services)
├── nginx.conf              # NGINX reverse proxy config
└── .env.example            # Environment variable template
```

---

*Documentation written for the TelcoPilot hackathon submission — Group 8.*
