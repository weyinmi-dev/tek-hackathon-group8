# TelcoPilot

**AI-Native Telco Operations Platform** — natural-language network intelligence for the Lagos metro NOC.

> "Stop digging through logs. **Ask the network.**"

A modular-monolith .NET 10 backend, a Next.js 15 (TypeScript) frontend, Semantic-Kernel-powered Copilot wired to Azure OpenAI, all behind a single NGINX gateway.

---

## What it is

| | |
|---|---|
| **Architecture** | Modular monolith — one backend deployable, in-process MediatR + domain events. No HTTP between modules. |
| **Backend** | .NET 10 · ASP.NET Core minimal APIs · EF Core 10 (Postgres) · Redis cache · MediatR · Serilog · JWT |
| **AI** | Microsoft Semantic Kernel + Azure OpenAI (with deterministic Mock fallback) |
| **Frontend** | Next.js 15 · App Router · TypeScript · React 19 |
| **Gateway** | NGINX (single backend upstream — modular monolith stays one logical service) |
| **Local orchestration** | .NET Aspire AppHost + user-secrets |
| **Containerized** | 5 services: nginx · frontend · backend · postgres · redis |

---

## Quick start (Docker)

```bash
cp .env.example .env                # tweak secrets if you want
docker compose up --build           # ~3 min first build
open http://localhost               # → /login
```

**Demo logins** (any of the 8 seeded users — all share the demo password):

| Email                       | Role     | What you can do                                              |
|-----------------------------|----------|--------------------------------------------------------------|
| `oluwaseun.a@telco.lag`     | engineer | Copilot, map, alerts read + ack, dashboard                   |
| `amaka.o@telco.lag`         | manager  | + users, audit log, assign incidents                         |
| `tunde.b@telco.lag`         | admin    | full access                                                  |
| `kemi.a@telco.lag`          | viewer   | read-only dashboard, alerts, map                             |

Password for all demo users: **`Telco!2025`**

---

## The five exposed APIs

All endpoints live behind a single NGINX upstream — every module shares the `/api/*` prefix.

| Method | Path                        | Module    | Auth         |
|-------:|-----------------------------|-----------|--------------|
| POST   | `/api/auth/login`           | Identity  | anonymous    |
| POST   | `/api/auth/refresh`         | Identity  | anonymous    |
| GET    | `/api/auth/me`              | Identity  | Bearer       |
| GET    | `/api/auth/users`           | Identity  | manager+     |
| POST   | `/api/chat`                 | AI        | engineer+    |
| GET    | `/api/metrics`              | Analytics | Bearer       |
| GET    | `/api/metrics/audit`        | Analytics | manager+     |
| GET    | `/api/alerts?severity=…`    | Alerts    | Bearer       |
| POST   | `/api/alerts/{id}/ack`      | Alerts    | engineer+    |
| GET    | `/api/map`                  | Network   | Bearer       |

Swagger at <http://localhost/swagger> in development.

---

## Architecture

### Modular monolith

```
                              NGINX :80
                                 │
              ┌──────────────────┴──────────────────┐
              │                                     │
       /api/* upstream                       /  upstream
              │                                     │
        backend :8080                       frontend :3000
   (Web.Api — ONE process)                   (Next.js)
              │
   ┌──────────┼─────────┬──────────┬──────────┐
   │          │         │          │          │
Identity   Network   Alerts    Analytics     AI
 module    module    module     module     module
   ▲          ▲         ▲          ▲          │
   └──────────┴─────────┴──────────┘          │
        in-process MediatR                    ▼
        + domain events                   Semantic Kernel
                                       (Diagnostics / Outage /
                                        Recommendation skills)
                                              │
                                              ▼
                                        Azure OpenAI
                                       (or Mock fallback)
```

**The architectural invariant:** the backend is **one** container with **one** NGINX upstream. Splitting modules across containers — or adding `upstream ai`, `upstream alerts` — would silently turn this into a distributed system and break the in-process MediatR contract. Don't do it.

### Module layout (per module)

```
src/Modules/<Name>/
├── Modules.<Name>.Domain/          # entities, value objects, domain events  (refs: SharedKernel)
├── Modules.<Name>.Application/     # CQRS commands/queries, handlers          (refs: Application + Domain)
├── Modules.<Name>.Infrastructure/  # EF DbContext, repositories, seed         (refs: all of the above + .Api)
└── Modules.<Name>.Api/             # cross-module contracts (interfaces+DTOs) (refs: nothing)
```

The **`.Api` project** is the public face other modules consume: e.g. the AI module's skills call `INetworkApi` to get tower metrics. **It's a same-process interface — no HTTP.**

### Per-module Postgres schemas

Each module owns its own Postgres schema (`identity`, `network`, `alerts`, `analytics`, `ai`) on the **same** database. Database isolation without container fragmentation.

### Pipeline behaviors

The shared MediatR pipeline (in `src/Application`) wraps every command/query with:
1. `ExceptionHandlingPipelineBehavior` — log + rethrow
2. `RequestLoggingPipelineBehavior` — Serilog enrichment
3. `ValidationPipelineBehavior` — FluentValidation if a validator is registered
4. `QueryCachingPipelineBehavior` — Redis-backed cache for `ICachedQuery` markers (`GetMapQuery`, `GetMetricsQuery`)

---

## The AI module (the differentiator)

`POST /api/chat` flow:

```
HTTP request
   │
   ▼
ChatEndpoint  ──MediatR──►  AskCopilotCommandHandler
                                 │
                                 ▼
                          ICopilotOrchestrator
                              ╱        ╲
              SemanticKernelOrchestrator   MockCopilotOrchestrator
            (Azure OpenAI, real SK plan)   (deterministic, calls real APIs)
                              │
              ┌───────────────┼────────────────┐
              ▼               ▼                ▼
      DiagnosticsSkill   OutageSkill   RecommendationSkill
              │               │                │
              ▼               ▼                │
        INetworkApi     IAlertsApi             │
              │               │                │
              └────►  in-process call  ◄───────┘
                          to other
                         modules — NO
                          HTTP HOPS
```

The orchestrator is selected at startup via `Ai:Provider`:
- `Mock` (default) — no Azure OpenAI required, still hits the real Network/Alerts modules
- `AzureOpenAi` — full Semantic Kernel with auto function-calling against the three skills

Switch providers without redeploying:
```bash
# Aspire (local dev with user-secrets):
dotnet user-secrets --project src/AppHost set "Ai:Provider"               "AzureOpenAi"
dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:Endpoint"   "https://<your>.openai.azure.com/"
dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:ApiKey"     "<key>"
dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:Deployment" "gpt-4o-mini"

# Docker compose:
echo "AI_PROVIDER=AzureOpenAi"                            >> .env
echo "AZURE_OPENAI_ENDPOINT=https://<your>.openai.azure.com/" >> .env
echo "AZURE_OPENAI_API_KEY=<key>"                         >> .env
docker compose up -d --force-recreate backend
```
### AI Output and Observability

The Copilot returns structured responses including:

- Answer → generated insight  
- Confidence score → reliability of the response  
- Skill trace → sequence of executed skills  
- Provider → AI engine used  

All interactions are logged and persisted for audit and analytics purposes.


---
###  context awareness 

Each query includes user identity and role, enabling the system to generate context-aware responses tailored to different operational roles (e.g., Engineer vs Manager).


---




---

## Local development modes

### Mode 1 — Aspire (recommended for backend dev)

```bash
# One-time secrets
dotnet user-secrets --project src/AppHost init
dotnet user-secrets --project src/AppHost set "Jwt:Secret" "$(openssl rand -base64 48)"

# Run
dotnet run --project src/AppHost
# → opens Aspire dashboard at https://localhost:17017
# → boots Postgres + Redis + Web.Api

# Frontend (separate terminal)
cd frontend && npm install && npm run dev
# → http://localhost:3000
```

### Mode 2 — Docker compose (recommended for full demo)

```bash
docker compose up --build
# → http://localhost
```

---

## Folder structure

```
TelcoPilot.slnx
Directory.Build.props        # net10.0, nullable, sonar
docker-compose.yml
.env.example
.dockerignore
README.md

src/
├── Application/             # shared CQRS abstractions, pipeline behaviors
├── Infrastructure/          # shared infra (db conn factory, cache, healthchecks, event bus)
├── SharedKernel/            # Result, Entity, Error, IDomainEvent
├── ServiceDefaults/         # Aspire OTel + service discovery
├── Web.Api/                 # composition root + 5 endpoint resources
├── AppHost/                 # Aspire local orchestrator
└── Modules/
    ├── Identity/            # users, roles, JWT, login/refresh
    ├── Network/             # towers, regions, signal heatmap
    ├── Alerts/              # incidents with AI cause + ack flow
    ├── Analytics/           # KPIs, sparklines, audit trail
    └── Ai/                  # Semantic Kernel + 3 skills + Mock fallback

## Frontend Architecture

The frontend is built using Next.js with TypeScript, providing a modular and scalable interface for telecom network operations and AI-assisted insights.

### Component Structure

The UI is organized into reusable components:

- Copilot
  - AI-powered interface for natural language queries
  - Acts as the user entry point for interacting with the system intelligence layer

- Network Maps
  - Visual representation of telecom infrastructure
  - Displays tower locations, signal strength, and load status

---

### Application Modules

The frontend contains a feature-based architecture under `app/authed`, representing authenticated user access.

####  Users Module
- Displays user profiles and roles
- Implements Role-Based Access Control (RBAC)
- Visualizes permissions per role (Engineer, Manager, Admin, Viewer)

####  Audit Module
- Displays system and user activity logs
- Tracks actions such as login, changes, and system events
- Supports filtering and read-only inspection

---

###  Core Functional Modules

####  Dashboard
- Displays KPIs and system performance metrics
- Includes visual charts and summary indicators

####  Alerts
- Displays network incidents categorized by severity
- Includes AI-style summaries of incident causes and impact

####  Map
- Visualizes telecom infrastructure geographically
- Shows signal strength, tower load, and operational status

#### Insights
- Provides aggregated analytics and trends
- Combines backend data with computed visual summaries

####  Copilot
- Natural language interface for querying system intelligence
- Sends requests to backend `/api/chat` endpoint

---

###  Frontend Data Flow

User Input  
→ UI Components  
→ API Client  
→ Backend API (`/api/*`)  
→ Response Rendered in UI

---

###  UI Design System

- Built with Next.js App Router architecture
- Supports light/dark themes
- Uses modular component-based design
- Focused on operational clarity for telecom monitoring

frontend/
├── Dockerfile               # multi-stage Node 22 → next standalone
├── package.json
└── src/
    ├── app/
    │   ├── login/           # split-screen sign-in
    │   └── (authed)/        # all routes behind the auth gate
    │       ├── dashboard/   # Command Center (KPI strip + map + Copilot)
    │       ├── insights/    # full Operations Dashboard
    │       ├── copilot/     # full-screen chat
    │       ├── map/         # Network Map + region health
    │       ├── alerts/      # Smart Alerts feed + ack
    │       ├── users/       # Users & RBAC
    │       └── audit/       # Audit Log
    ├── components/          # UI primitives, Sidebar, TopBar, NetworkMap, Copilot
    └── lib/                 # api client, types, auth hook

deploy/
├── Dockerfile.backend       # multi-stage .NET SDK 10 → aspnet:10 runtime
└── nginx/
    └── nginx.conf           # single backend upstream, /api/* + / routing
```

---

## Security

- **JWT bearer auth** with rotating refresh tokens (SHA-256 hash stored, never the raw token)
- **RBAC** — 4 roles (engineer / manager / admin / viewer) enforced via ASP.NET policies + endpoint attributes
- **BCrypt** password hashing (cost 11)
- **Secrets via env vars or user-secrets** — never committed
- **Audit log** — every Copilot query and admin action persisted to the `analytics.audit_entries` table

---

## What's intentionally NOT here

- ❌ Per-module containers (would break modular monolith)
- ❌ Per-module nginx upstreams (same)
- ❌ Inter-module HTTP/gRPC (same)
- ❌ Hangfire / Outbox processor (out of demo scope, easily re-added — the `IEventBus` + `InMemoryMessageQueue` are still wired)
- ❌ Azure Blob storage (out of demo scope)
- ❌ EF migrations (we use `EnsureCreatedAsync` + idempotent seeders — fast for demo, swap to `Add-Migration` for prod)
