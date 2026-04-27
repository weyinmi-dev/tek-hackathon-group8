# TelcoPilot — Onboarding

Welcome. This folder is the **first stop for every new contributor** to the
TelcoPilot codebase. It assumes nothing about your prior exposure to the project
and walks you from a fresh clone to a fully working local environment, then on
to your first meaningful change.

> If you only read one paragraph, read this one.
>
> TelcoPilot is an **AI-Native Telco Operations Platform**. It is a *modular
> monolith* on the backend ([.NET 10](https://dotnet.microsoft.com/) +
> [ASP.NET Core](https://learn.microsoft.com/aspnet/core) minimal APIs +
> [PostgreSQL](https://www.postgresql.org/) + [Redis](https://redis.io/) +
> [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) on
> top of [Azure OpenAI](https://learn.microsoft.com/azure/ai-services/openai/)),
> a [Next.js 15](https://nextjs.org/) (App Router, React 19, TypeScript)
> frontend, all fronted in production by a single
> [NGINX](https://nginx.org/) gateway. Locally we orchestrate everything with
> [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/); in production we
> orchestrate with `docker compose`.

---

## How this folder is organised

| Document                                     | Read this if you are…                                                                       |
|----------------------------------------------|---------------------------------------------------------------------------------------------|
| [`README.md`](README.md) (this file)         | Brand new — start here for the 30-minute high-level orientation and environment bootstrap. |
| [`frontend.md`](frontend.md)                 | Working on the [Next.js](https://nextjs.org/) UI in [`frontend/`](../../frontend/).        |
| [`backend.md`](backend.md)                   | Working on the .NET modular monolith in [`src/`](../../src/).                              |
| [`devops.md`](devops.md)                     | Working on Docker, Aspire, NGINX, deployments, secrets, or CI/CD.                          |

Each of those three documents is **self-contained and exhaustively explanatory**
— you can hand any one of them to a new hire on day one and expect them to be
unblocked without needing to ask questions.

---

## What you are about to work on (the 90-second tour)

TelcoPilot's pitch is *"Stop digging through logs. **Ask the network.**"* It is
the operations console for a fictional Lagos-metro telco's Network Operations
Center (NOC). Engineers, managers, admins, and viewers log in, see live KPIs,
inspect a heat-map of cell towers, triage smart alerts, and — the headline
feature — **ask the network natural-language questions** ("why is Lekki
degraded?", "which towers are about to fail?", "summarise tonight's incidents").

The Copilot is powered by a Semantic-Kernel orchestrator that calls three
deterministic skills (`DiagnosticsSkill`, `OutageSkill`,
`RecommendationSkill`). Those skills reach into the rest of the monolith over
**in-process** interfaces (`INetworkApi`, `IAlertsApi`) — never over HTTP. That
constraint is *load-bearing*: see the architecture invariant below.

```
                         NGINX :80
                            │
            ┌───────────────┴───────────────┐
            │                               │
       /api/* upstream                  / upstream
            │                               │
      backend :8080                   frontend :3000
   (Web.Api — ONE process)             (Next.js standalone)
            │
   ┌────────┼────────┬────────┬────────┐
   │        │        │        │        │
Identity Network  Alerts  Analytics   AI
 module   module   module   module   module
   ▲        ▲        ▲        ▲        │
   └────────┴────────┴────────┘        │
        in-process MediatR              ▼
        + domain events             Semantic Kernel
                                  ↓ Azure OpenAI
                                    (or Mock)
```

### The architectural invariant — please internalise this

The backend is **one container** with **one NGINX upstream**. Every module
shares the same OS process, the same MediatR pipeline, the same Postgres
*database* (with schema-per-module isolation). If you ever feel the urge to:

- give a module its own container,
- add a second NGINX upstream (`upstream ai`, `upstream alerts`, …), or
- introduce HTTP/gRPC between two modules,

**stop, and read [`backend.md`](backend.md) §"The modular monolith
contract"**. Doing any of those silently turns this into a distributed system
and breaks the in-process MediatR + domain-events contract that the AI module
in particular depends on.

---

## Repository tour

```
tek-hackthn-grp8/
├── README.md                  # marketing-grade product overview (read second)
├── docker-compose.yml         # 5-service production-shaped stack
├── .env.example               # env vars consumed by docker compose
├── Directory.Build.props      # net10.0, nullable, warnings-as-errors, Sonar
├── TelcoPilot.slnx            # solution file (slnx, the new lightweight format)
│
├── docs/                      # ← you are here
│   ├── instructions.md        # legacy / scratchpad — ignore unless told otherwise
│   └── onboarding/            # this folder
│
├── frontend/                  # Next.js 15 (App Router, React 19, TypeScript)
│   ├── Dockerfile             # multi-stage build → next standalone
│   ├── next.config.mjs        # /api/* rewrite → backend
│   └── src/
│       ├── app/               # routes (login + (authed) group)
│       ├── components/        # Sidebar, TopBar, NetworkMap, Copilot, UI
│       └── lib/               # api client, types, useAuth hook
│
├── src/                       # .NET 10 backend
│   ├── AppHost/               # Aspire local orchestrator (boots Postgres+Redis+API+frontend)
│   ├── Web.Api/               # composition root (Program.cs, endpoints, swagger)
│   ├── Application/           # shared CQRS abstractions + MediatR pipeline behaviors
│   ├── Infrastructure/        # shared infra (db, cache, healthchecks, in-mem event bus)
│   ├── ServiceDefaults/       # Aspire OTel + service discovery + resilience
│   ├── SharedKernel/          # Result, Entity, Error, IDomainEvent
│   └── Modules/               # the five business modules
│       ├── Identity/          # users, roles, JWT, login/refresh
│       ├── Network/           # towers, regions, signal heatmap
│       ├── Alerts/            # incidents w/ AI cause + ack flow
│       ├── Analytics/         # KPIs, sparklines, audit trail
│       └── Ai/                # Semantic Kernel + 3 skills + Mock fallback
│
└── deploy/
    ├── Dockerfile.backend     # multi-stage SDK 10 → aspnet:10 runtime
    └── nginx/nginx.conf       # single-upstream gateway config
```

---

## Prerequisites — install these once, in this order

> Cross-reference the per-area docs for OS-specific notes (Windows / macOS /
> Linux). The list below is the union; you do **not** need all of them if you
> only ever touch one layer.

| Tool                       | Version    | Why we need it                                                                                |
|----------------------------|------------|-----------------------------------------------------------------------------------------------|
| [Git](https://git-scm.com/)| any recent | source control                                                                                |
| [Docker Desktop](https://docs.docker.com/desktop/) (or Docker Engine + compose v2) | 24+ | runs the full 5-service stack locally |
| [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)              | 10.0.x | builds + runs the backend and Aspire AppHost                       |
| [Node.js](https://nodejs.org/)                                                | 22 LTS | builds + runs the Next.js frontend (matches the Docker base image) |
| [npm](https://www.npmjs.com/) (ships with Node)                               | 10+    | frontend dep manager                                               |
| [Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling) | latest | only if you want `dotnet run --project src/AppHost` |
| [PostgreSQL client](https://www.postgresql.org/download/) (`psql`)            | 17     | optional — handy for poking at the demo database                   |
| [openssl](https://www.openssl.org/)                                           | any    | generating the JWT signing secret                                  |

> Windows users: WSL2 is **strongly recommended** for both Docker and the
> general developer experience. Native Windows works but you will hit more
> path-separator and line-ending edge cases.

---

## First boot — the five-minute path (Docker, no .NET SDK required)

```bash
git clone <repo-url>
cd tek-hackthn-grp8
cp .env.example .env                 # edit JWT_SECRET if you want
docker compose up --build            # ~3 min the first time
```

Once compose reports all five services as `Started`, open
<http://localhost> in your browser. You should see the TelcoPilot login screen.
Log in as any seeded demo user (the email is pre-filled on the form):

| Email                       | Role     | Password      |
|-----------------------------|----------|---------------|
| `oluwaseun.a@telco.lag`     | engineer | `Telco!2025`  |
| `amaka.o@telco.lag`         | manager  | `Telco!2025`  |
| `tunde.b@telco.lag`         | admin    | `Telco!2025`  |
| `kemi.a@telco.lag`          | viewer   | `Telco!2025`  |

If anything goes wrong: the most common first-run failures are addressed in
[`devops.md`](devops.md) §"Troubleshooting first-boot".

---

## First boot — the developer path (Aspire, hot-reload, recommended for backend work)

```bash
# 1. One-time secrets — set the JWT signing key for local dev
dotnet user-secrets --project src/AppHost init
dotnet user-secrets --project src/AppHost set "Jwt:Secret" "$(openssl rand -base64 48)"

# 2. Boot Postgres + Redis + Web.Api + Next.js via Aspire
dotnet run --project src/AppHost
# → Aspire dashboard opens at https://localhost:17017
# → Web.Api on a dynamically-assigned port (visible in dashboard)
# → Frontend on a dynamically-assigned port too
```

Aspire wires everything end-to-end (service discovery, environment variable
forwarding, OTel) so you do not need to manually start anything else. See
[`backend.md`](backend.md) §"Aspire local development" for the full breakdown.

If you only want to work on the frontend and have Docker running the rest of
the stack, see [`frontend.md`](frontend.md) §"Standalone frontend dev".

---

## The five exposed APIs (memorise this table)

Every endpoint lives behind a single NGINX upstream and shares the `/api/*`
prefix. There is no versioning by design — this is one logical service.

| Method | Path                        | Module    | Auth                       |
|-------:|-----------------------------|-----------|----------------------------|
| POST   | `/api/auth/login`           | Identity  | anonymous                  |
| POST   | `/api/auth/refresh`         | Identity  | anonymous                  |
| GET    | `/api/auth/me`              | Identity  | Bearer                     |
| GET    | `/api/auth/users`           | Identity  | manager+                   |
| POST   | `/api/chat`                 | AI        | engineer+                  |
| GET    | `/api/metrics`              | Analytics | Bearer                     |
| GET    | `/api/metrics/audit`        | Analytics | manager+                   |
| GET    | `/api/alerts?severity=…`    | Alerts    | Bearer                     |
| POST   | `/api/alerts/{id}/ack`      | Alerts    | engineer+                  |
| GET    | `/api/map`                  | Network   | Bearer                     |

Swagger UI lives at <http://localhost/swagger> in development.

---

## Workflow norms

- **Branches:** cut from `main`. Use a short kebab-cased descriptor:
  `feat/copilot-skill-router`, `fix/alert-ack-409`. The branch policy on
  `main` is enforced via PRs.
- **Commits:** small, focused, present-tense imperative
  ("Add audit-log filter for IP", not "added"). The recent history shows the
  cadence — keep it.
- **Builds:** `dotnet build` for the backend (warnings are errors — see
  [Directory.Build.props](../../Directory.Build.props)). `npm run build` for
  the frontend.
- **Linters / analyzers:** SonarAnalyzer.CSharp runs on every backend build;
  `eslint-config-next` runs on the frontend. **Do not** suppress warnings
  globally; suppress at the smallest possible scope and document why.
- **Secrets:** never commit. Local: .NET user-secrets. Docker: `.env` (which
  is gitignored). Production: real secret store (Key Vault / Doppler / AWS
  Secrets Manager — out of scope for this repo).

---

## Where to ask for help

- For architectural questions, read the relevant area doc end-to-end first,
  then [`README.md`](../../README.md) at repo root.
- The single most authoritative reference for *"how does this run end-to-end?"*
  is [`docker-compose.yml`](../../docker-compose.yml) — five services, one
  network, all environment variables in one place.
- The single most authoritative reference for *"what are the contracts between
  modules?"* is the per-module `Modules.<Name>.Api/` project — those are the
  only types one module is allowed to consume from another.

Welcome aboard.
