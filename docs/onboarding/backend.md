# Backend Onboarding — TelcoPilot Modular Monolith

> Audience: developers contributing to the [`src/`](../../src/) directory
> (anything under .NET / C#). Read [`README.md`](README.md) first for the
> cross-cutting orientation.

This document is exhaustive on purpose. The TelcoPilot backend has more
deliberate constraints than its size suggests, and getting them wrong
silently breaks the AI module. Take the time.

---

## 1. The shape of the system

The backend is a **modular monolith**. That phrase is doing a lot of work, so
unpack it carefully:

- **Monolith**, because there is exactly one runtime artefact —
  [`src/Web.Api/`](../../src/Web.Api/) — produced by one `dotnet publish` and
  hosted in **one container** behind **one** NGINX upstream.
- **Modular**, because that single process is composed from five
  independently-owned business modules
  ([`src/Modules/Identity`](../../src/Modules/Identity/),
  [`Network`](../../src/Modules/Network/),
  [`Alerts`](../../src/Modules/Alerts/),
  [`Analytics`](../../src/Modules/Analytics/),
  [`Ai`](../../src/Modules/Ai/)). Each module owns its own Postgres schema,
  its own DbContext, its own MediatR handlers, its own seeders, and its own
  *public contract project* (`Modules.<Name>.Api`).

Modules **never** talk to each other over HTTP. They communicate via:

1. **In-process MediatR** — for command/query routing within a module, or for
   integration events between modules.
2. **`Modules.<Name>.Api`** interfaces — for synchronous "give me data right
   now" calls (e.g., the AI module's `DiagnosticsSkill` calls `INetworkApi`
   to fetch tower metrics).
3. **Domain events + the in-memory event bus**
   ([`Infrastructure/Events`](../../src/Infrastructure/Events/)) — for
   loose-coupled "X happened" notifications.

That is the contract. It is enforced socially (code review) and structurally
(the `.Api` projects reference *nothing*; the `.Domain` projects reference
only `SharedKernel`; cross-module references must go through `.Api`).

### The architectural invariant (do not break)

> **One backend container. One NGINX upstream. No HTTP hops between modules.**

If you ever feel the urge to give the AI module its own container, or to add
a second `upstream` block to NGINX, or to put a `HttpClient` between two
modules — **stop**. Doing any of those silently turns this into a distributed
system. The AI module's `MockCopilotOrchestrator` and Semantic-Kernel skills
both rely on direct in-process calls into Network and Alerts. Once you add
the network hop, you have:

- different process boundaries → different config sources → mismatched JWT
  secrets → 401s on internal calls;
- network partitions → retry storms → cascading failures during demo;
- distributed transactions → eventual consistency → audit log losing entries.

We chose modular monolith *specifically* to avoid this. If you genuinely need
to split a module out later, that is a deliberate, multi-week migration —
not a one-PR decision.

---

## 2. Tech stack — exact versions and why

| Layer                    | Choice                                                                                                | Pinned at                                            | Why                                                                                          |
|--------------------------|-------------------------------------------------------------------------------------------------------|------------------------------------------------------|----------------------------------------------------------------------------------------------|
| Runtime / SDK            | [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)                                          | [`Directory.Build.props`](../../Directory.Build.props) `net10.0` | Latest LTS-class release; matches Aspire 13.                                       |
| Web framework            | ASP.NET Core minimal APIs                                                                             | [`src/Web.Api/Web.Api.csproj`](../../src/Web.Api/Web.Api.csproj) | Minimal-API endpoints + `IEndpoint` discovery (no MVC controllers).             |
| ORM                      | EF Core 10 + [Npgsql](https://www.npgsql.org/) provider                                               | per module                                           | One DbContext per module; snake-case naming; schema-per-module.                              |
| In-process mediation     | [MediatR](https://github.com/jbogard/MediatR)                                                         | [`src/Application`](../../src/Application/)          | Commands, queries, pipeline behaviors, handler discovery.                                    |
| Validation               | [FluentValidation](https://docs.fluentvalidation.net/)                                                | per module                                           | Auto-applied via `ValidationPipelineBehavior`.                                               |
| Logging                  | [Serilog](https://serilog.net/) + Console + Seq sinks                                                 | [`src/Web.Api/Web.Api.csproj`](../../src/Web.Api/Web.Api.csproj) | Structured logs, request enrichment, optional Seq dashboard.                |
| Cache                    | [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/) via `IDistributedCache`   | [`Infrastructure/Caching`](../../src/Infrastructure/Caching/) | Backs `QueryCachingPipelineBehavior` for `ICachedQuery` markers.                |
| Auth                     | [JWT bearer](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication) + BCrypt + rotating refresh tokens | [`Modules/Identity`](../../src/Modules/Identity/) | Self-contained tokens, no external IdP. SHA-256 hash of refresh token persisted, raw token never stored. |
| AI                       | [Microsoft Semantic Kernel](https://learn.microsoft.com/semantic-kernel/) + Azure OpenAI **or** deterministic Mock | [`Modules/Ai`](../../src/Modules/Ai/) | Real planner with auto function-calling on three skills, with a no-creds fallback. |
| Local orchestration      | [.NET Aspire 13](https://learn.microsoft.com/dotnet/aspire/) AppHost                                  | [`src/AppHost/AppHost.csproj`](../../src/AppHost/AppHost.csproj) | Spins up Postgres + Redis + Web.Api + the Next.js frontend with one command.  |
| Static analysis          | [SonarAnalyzer.CSharp](https://www.nuget.org/packages/SonarAnalyzer.CSharp/)                          | [`Directory.Build.props`](../../Directory.Build.props) | Warnings are errors. `AnalysisMode=All`. Enforced on every build.                            |
| Health checks            | [`Microsoft.Extensions.Diagnostics.HealthChecks`](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks) + Npgsql + Redis probes | [`Infrastructure/DependencyInjection.cs`](../../src/Infrastructure/DependencyInjection.cs) | `/health` is the compose / liveness endpoint. |
| Telemetry                | [OpenTelemetry](https://opentelemetry.io/) (metrics + traces + logs) via Aspire `ServiceDefaults`     | [`src/ServiceDefaults/Extensions.cs`](../../src/ServiceDefaults/Extensions.cs) | OTLP exporter optional via `OTEL_EXPORTER_OTLP_ENDPOINT`.                  |

---

## 3. Local setup — step by step

### 3.1 Prerequisites

1. **.NET SDK 10.0.x** — confirm with `dotnet --info`. If you see anything
   below 10.0, install from
   <https://dotnet.microsoft.com/download/dotnet/10.0>.
2. **Aspire workload** (optional but recommended):
   ```bash
   dotnet workload install aspire
   ```
3. **Docker Desktop** running (Aspire uses it under the hood for Postgres /
   Redis containers).
4. **`openssl`** (for generating the JWT signing key — Git Bash on Windows
   ships it).

### 3.2 First build

```bash
git clone <repo-url>
cd tek-hackthn-grp8
dotnet restore
dotnet build
```

Expect zero warnings. `Directory.Build.props` sets
`TreatWarningsAsErrors=true` and `AnalysisMode=All`, so any new warning
**will** break your build. Fix the warning at the smallest possible scope —
a `#pragma warning disable` is acceptable only with a comment explaining why.

### 3.3 Aspire local development (recommended)

```bash
# One-time secrets — set the JWT signing key
dotnet user-secrets --project src/AppHost init
dotnet user-secrets --project src/AppHost set "Jwt:Secret" "$(openssl rand -base64 48)"

# Optional: switch the AI module to real Azure OpenAI
dotnet user-secrets --project src/AppHost set "Ai:Provider"               "AzureOpenAi"
dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:Endpoint"   "https://<your>.openai.azure.com/"
dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:ApiKey"     "<key>"
dotnet user-secrets --project src/AppHost set "Ai:AzureOpenAi:Deployment" "gpt-4o-mini"

# Run
dotnet run --project src/AppHost
```

What happens:

- The Aspire AppHost ([`src/AppHost/Program.cs`](../../src/AppHost/Program.cs))
  declares four resources: Postgres (with PgWeb), Redis, Web.Api, and the
  Next.js frontend. Aspire boots the containers, waits for health, then
  launches the API and frontend, wiring environment variables (connection
  strings, `BACKEND_INTERNAL_URL`) automatically.
- The Aspire dashboard opens at <https://localhost:17017>. From there you
  can see logs, traces, and the dynamically-assigned ports for the API
  and frontend.
- User-secrets values (Jwt + Ai) are forwarded into Web.Api's environment
  via `WithEnvironment(...)` calls
  ([`AppHost/Program.cs:38-42`](../../src/AppHost/Program.cs#L38-L42)).
  This means **you set secrets once on the AppHost project**, not on every
  module — this is the correct mental model.

### 3.4 Standalone Web.Api (no Aspire)

```bash
# Bring up just Postgres + Redis (and seq if you want)
docker compose up postgres redis

# Run the API directly — uses appsettings.Development.json connection strings
dotnet run --project src/Web.Api
```

The Development connection strings
([`appsettings.Development.json`](../../src/Web.Api/appsettings.Development.json))
point at `postgres:5432` and `redis:6379`, which **only resolve inside the
docker network**. For naked `dotnet run`, override on the command line:

```bash
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__Database="Host=localhost;Port=5432;Database=telcopilot;Username=postgres;Password=postgres;Include Error Detail=true" \
ConnectionStrings__Cache="localhost:6379" \
Jwt__Secret="$(openssl rand -base64 48)" \
dotnet run --project src/Web.Api
```

(Or just use Aspire — it does this for you.)

### 3.5 Running the full stack via Docker

See [`devops.md`](devops.md) §"Docker compose" for the production-shaped
loop. Short version: `docker compose up --build`.

---

## 4. Repository layout

### 4.1 Solution file

[`TelcoPilot.slnx`](../../TelcoPilot.slnx) is the new lightweight
[`.slnx`](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/)
solution format — XML-based, smaller, easier to merge than legacy `.sln`.
Visual Studio 17.13+ and `dotnet` CLI 10 both understand it natively.

### 4.2 Top-level projects

```
src/
├── AppHost/              # Aspire local orchestrator (executable)
├── Web.Api/              # composition root + endpoint host (the one that ships)
├── Application/          # shared CQRS abstractions + MediatR pipeline behaviors
├── Infrastructure/       # shared infra (db connection factory, redis cache, healthchecks, in-mem event bus)
├── ServiceDefaults/      # Aspire OTel + service discovery + standard resilience
├── SharedKernel/         # Result, Entity, Error, IDomainEvent, Ensure, IDateTimeProvider
└── Modules/              # the five business modules (see §4.3)
```

#### `SharedKernel`

The smallest project. **Zero dependencies.** Read the files end-to-end on day
one — they are short and you will use them constantly:

- [`Result.cs`](../../src/SharedKernel/Result.cs) — every command/query
  handler returns `Result<T>` rather than throwing.
- [`Entity.cs`](../../src/SharedKernel/Entity.cs) — base class for
  aggregates; carries a `RaiseDomainEvent` queue.
- [`Error.cs`](../../src/SharedKernel/Error.cs) +
  [`ErrorType.cs`](../../src/SharedKernel/ErrorType.cs) — enumerated error
  shapes (`NotFound`, `Validation`, `Conflict`, `Failure`).
- [`IDomainEvent.cs`](../../src/SharedKernel/IDomainEvent.cs) — marker
  interface; events flow through MediatR.
- [`Ensure.cs`](../../src/SharedKernel/Ensure.cs) — argument guards
  (`Ensure.NotNullOrEmpty(value)`).
- [`IDateTimeProvider.cs`](../../src/SharedKernel/IDateTimeProvider.cs) —
  inject this everywhere instead of `DateTime.UtcNow` so tests can pin time.

#### `Application` (shared)

[`src/Application`](../../src/Application/) is the **shared** Application
layer — the abstractions the per-module Application projects depend on. It
contains:

- [`Abstractions/Messaging/`](../../src/Application/Abstractions/Messaging/) —
  `ICommand`, `ICommandHandler<T>`, `IQuery`, `IQueryHandler<T>` (thin marker
  interfaces over MediatR's `IRequest`).
- [`Abstractions/Caching/`](../../src/Application/Abstractions/Caching/) —
  `ICacheService` and `ICachedQuery` (mark a query as cacheable; the pipeline
  behavior handles the rest).
- [`Abstractions/Behaviors/`](../../src/Application/Abstractions/Behaviors/)
  — the four MediatR pipeline behaviors:

  | Behavior                           | What it does                                                              |
  |------------------------------------|---------------------------------------------------------------------------|
  | `ExceptionHandlingPipelineBehavior`| Catches unhandled exceptions, logs them, rethrows.                        |
  | `RequestLoggingPipelineBehavior`   | Adds Serilog enrichment for the request name + correlation id.            |
  | `ValidationPipelineBehavior`       | Resolves `IValidator<T>` if registered; short-circuits with `Result.Failure(Error.Validation(...))` on failure. |
  | `QueryCachingPipelineBehavior`     | If the query implements `ICachedQuery`, returns the cached value (Redis) or executes + caches. |

  These are wired in
  [`Application/DependencyInjection.cs`](../../src/Application/DependencyInjection.cs)
  in the order shown.

- [`Abstractions/Events/`](../../src/Application/Abstractions/Events/) —
  `IEventBus`, `IIntegrationEvent` for cross-module pub/sub.
- [`Abstractions/Notifications/`](../../src/Application/Abstractions/Notifications/)
  — `INotificationService` (currently a no-op; placeholder).
- [`Abstractions/Data/`](../../src/Application/Abstractions/Data/) —
  `IDbConnectionFactory` for raw-SQL access (used in a couple of read paths
  where Dapper is more ergonomic than EF).

#### `Infrastructure` (shared)

[`src/Infrastructure`](../../src/Infrastructure/) is the **shared**
Infrastructure layer — concrete implementations of the shared abstractions:

- `DbConnectionFactory` (Npgsql) — singleton.
- `CacheService` — wraps `IDistributedCache` (Redis or in-memory if no
  connection string).
- `EventBus` + `InMemoryMessageQueue` + `IntegrationEventProcessorJob` —
  cross-module pub/sub. The processor is a hosted service that drains the
  in-memory queue.
- `DateTimeProvider` (singleton) and `NotificationService` (transient).
- `AddHealthChecks(...)` — adds Npgsql + Redis probes if connection strings
  exist.

#### `ServiceDefaults`

The Aspire "service defaults" project
([`Extensions.cs`](../../src/ServiceDefaults/Extensions.cs)). Wires:

- OpenTelemetry (logs + metrics + traces).
- Service discovery (used by `HttpClient` defaults — irrelevant for the
  monolith but enabled for completeness).
- Standard resilience pipeline on `HttpClient` defaults.
- A `/health` and `/alive` endpoint pair (registered in dev only — see
  [`Extensions.cs:79-87`](../../src/ServiceDefaults/Extensions.cs#L79-L87)).

`Web.Api` calls `builder.AddServiceDefaults()` first thing in
[`Program.cs:30`](../../src/Web.Api/Program.cs#L30).

#### `Web.Api`

The composition root. Read
[`Program.cs`](../../src/Web.Api/Program.cs) end-to-end on your first day —
it is 135 lines and tells you exactly how the system boots:

1. `AddServiceDefaults()` — Aspire OTel etc.
2. Serilog from configuration.
3. CORS for `localhost:3000` / `localhost` / `127.0.0.1:3000`.
4. Swagger w/ Bearer scheme.
5. JWT bearer auth from `JwtOptions`.
6. Authorization policies (`RequireEngineer`, `RequireManager`,
   `RequireAdmin`).
7. **The big block** — `AddApplication() + AddInfrastructure() +
   AddIdentityApplication() + AddIdentityInfrastructure() + Network /
   Alerts / Analytics / Ai`. Each module's pair of `Add*Application` /
   `Add*Infrastructure` extension methods registers its own services. **The
   order is significant** — Identity must come before any module whose
   handlers query users.
8. Global exception handler + ProblemDetails.
9. Swagger gen options.
10. `AddEndpoints(Assembly.GetExecutingAssembly())` — discovers every
    `IEndpoint` in `Web.Api` via reflection
    ([`Extensions/EndpointExtensions.cs`](../../src/Web.Api/Extensions/EndpointExtensions.cs))
    and registers them under the `/api` route group.

After `Build()`:

- `MapEndpoints(apiGroup)` mounts every `IEndpoint` under `/api`.
- In Development: enable Swagger, run `ApplyMigrationsAsync()`, run
  `SeedDataAsync()`. **Both are idempotent.**
- CORS, health-check route, request-context middleware, Serilog request
  logging, exception handler, auth, authorisation.

The endpoints themselves live in
[`Web.Api/Endpoints/`](../../src/Web.Api/Endpoints/), grouped by module
folder. Each implements `IEndpoint`:

```csharp
public sealed class Login : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("auth/login", async (Request request, ISender sender, CancellationToken ct) =>
        {
            Result<LoginResponse> result = await sender.Send(new LoginCommand(...), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Auth)
        .AllowAnonymous();
    }

    public sealed record Request(string Email, string Password);
}
```

**Pattern to follow when you add an endpoint:**
1. Drop a new file in the matching subfolder under `Web.Api/Endpoints/`.
2. Implement `IEndpoint`. Use `MapPost / MapGet / ...`.
3. Send a MediatR command/query through `ISender`.
4. Return `result.Match(Results.Ok, CustomResults.Problem)` — never throw,
   never `return Results.BadRequest(…)` directly. The
   [`CustomResults.Problem`](../../src/Web.Api/Infrastructure/CustomResults.cs)
   helper produces a proper RFC 7807 ProblemDetails response from the
   `Error` value.
5. Add `[Authorize(Policy = Policies.RequireXyz)]` or `.AllowAnonymous()`.
6. Add `.WithTags(Tags.Xyz)` for Swagger grouping.

### 4.3 The five modules

Each module has the same four-project shape (Identity is missing the `.Api`
project pattern in its name but still exposes `IIdentityApi`):

```
src/Modules/<Name>/
├── Modules.<Name>.Domain/         # entities, value objects, domain events. refs: SharedKernel only.
├── Modules.<Name>.Application/    # CQRS commands/queries + handlers. refs: Application + Domain.
├── Modules.<Name>.Infrastructure/ # EF DbContext, repositories, seeders, options. refs: all of the above + .Api.
└── Modules.<Name>.Api/            # cross-module contracts (interfaces + DTOs). refs: nothing.
```

The **`.Api` project is the public face**. If module B needs anything from
module A, it must consume `Modules.A.Api`. If module A wants to use module
B's data, the implementation lives in `Modules.A.Infrastructure/Api/` (an
adapter that implements `Modules.B.Api`'s interface against module A's data).
Inverting that direction — having module A reference `Modules.B.Infrastructure`
— is the cardinal sin: it couples implementations and breaks the modular
boundary.

#### Module: Identity

Owns: users, roles, JWT issuance, refresh-token rotation, password hashing.

- **Domain** — `User` aggregate with `Email`, `PasswordHash`, `Role`, `Team`,
  `Region`. `RefreshToken` aggregate with hashed value + expiry.
- **Application** — `LoginCommand`, `RefreshTokenCommand`,
  `GetCurrentUserQuery`, `GetUsersQuery`. `IPasswordHasher` interface
  (BCrypt impl in Infrastructure). `JwtOptions`. `Policies` /
  `Roles` constants — used by the `[Authorize(Policy=…)]` attribute on
  endpoints.
- **Infrastructure** — `IdentityDbContext` (schema `identity`),
  `BcryptPasswordHasher` (work factor 11), `JwtTokenGenerator`, the
  `IdentitySeeder` (8 demo users, all sharing password `Telco!2025`,
  see [§5.3](#53-the-demo-users)).
- **Api** — `IIdentityApi`: lookup helpers other modules can call (e.g., for
  audit-log enrichment).

#### Module: Network

Owns: cell towers, regions, signal heatmap.

- **Domain** — `Tower`, `Region` aggregates.
- **Application** — `GetMapQuery` (cached via `ICachedQuery`),
  `INetworkApi` consumers.
- **Infrastructure** — `NetworkDbContext` (schema `network`),
  `NetworkApi` adapter (implementation of `INetworkApi`),
  `NetworkSeeder`.
- **Api** — `INetworkApi` (used by `DiagnosticsSkill` and
  `RecommendationSkill` in the AI module).

#### Module: Alerts

Owns: incidents, severity, AI-attributed cause, ack flow.

- **Domain** — `Alert` aggregate with severity, status, AI-cause,
  `Acknowledge()` method that emits a domain event.
- **Application** — `GetAlertsQuery` (filter by severity / active),
  `AcknowledgeAlertCommand`.
- **Infrastructure** — `AlertsDbContext` (schema `alerts`), `AlertsSeeder`,
  `AlertsApi` adapter.
- **Api** — `IAlertsApi` (used by `OutageSkill` in the AI module).

#### Module: Analytics

Owns: KPIs, sparklines, region/incident breakdowns, audit log.

- **Domain** — `AuditEntry` and KPI value objects.
- **Application** — `GetMetricsQuery` (cached), `GetAuditTrailQuery`.
- **Infrastructure** — `AnalyticsDbContext` (schema `analytics`),
  `AnalyticsSeeder` (deterministic KPI series), audit-entry persistence.
- **Api** — `IAnalyticsApi` for cross-module audit writes.

#### Module: Ai

Owns: the Copilot. Differentiator of the product.

```
src/Modules/Ai/
├── Modules.Ai.Domain/Conversations/        # ChatLog aggregate (audit trail)
├── Modules.Ai.Application/
│   ├── Copilot/AskCopilot/                 # AskCopilotCommand + handler
│   └── SemanticKernel/ICopilotOrchestrator.cs
└── Modules.Ai.Infrastructure/
    ├── Database/AiDbContext.cs             # schema `ai`
    ├── SemanticKernel/
    │   ├── AiOptions.cs                    # bound from "Ai" config section
    │   ├── MockCopilotOrchestrator.cs      # deterministic — calls real INetworkApi/IAlertsApi
    │   ├── SemanticKernelOrchestrator.cs   # real Semantic Kernel + Azure OpenAI
    │   └── Skills/
    │       ├── DiagnosticsSkill.cs
    │       ├── OutageSkill.cs
    │       └── RecommendationSkill.cs
    └── DependencyInjection.cs              # provider switch (Mock vs AzureOpenAi)
```

Provider selection happens in
[`Modules.Ai.Infrastructure/DependencyInjection.cs`](../../src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs):

```csharp
bool useAzure =
    string.Equals(ai.Provider, "AzureOpenAi", StringComparison.OrdinalIgnoreCase) &&
    !string.IsNullOrWhiteSpace(ai.AzureOpenAi.Endpoint) &&
    !string.IsNullOrWhiteSpace(ai.AzureOpenAi.ApiKey);
```

**The fallback to Mock is automatic** — if `Ai:Provider` is `AzureOpenAi`
but the endpoint or key is empty, you silently get the Mock. This is
deliberate (so a misconfigured demo machine still works) but it means **you
must check the response's `provider` field** in the UI to know which one
actually served a given request.

The full request flow from `POST /api/chat` to a rendered answer is in
[`README.md` §"The AI module"](../../README.md#the-ai-module-the-differentiator)
at the repo root. Internalise that diagram.

---

## 5. Cross-cutting concerns

### 5.1 Persistence — schema-per-module

All five modules share **one** Postgres database (`telcopilot`) but each owns
a **distinct schema**: `identity`, `network`, `alerts`, `analytics`, `ai`.
Each module's DbContext sets its `MigrationsHistoryTable` to live in its own
schema (see e.g.
[`Modules.Ai.Infrastructure/DependencyInjection.cs:24`](../../src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L24)).

Today there are **no EF Core migrations**. Schema is bootstrapped via
`EnsureCreatedAsync` per DbContext, with duplicate-table / duplicate-schema
errors swallowed for idempotent re-runs
([`Web.Api/Extensions/MigrationExtensions.cs:30-48`](../../src/Web.Api/Extensions/MigrationExtensions.cs#L30-L48)).
This is fine for the demo and intolerable for prod. When you graduate, the
swap is:

1. `dotnet ef migrations add Init --project src/Modules/<Name>/Modules.<Name>.Infrastructure --startup-project src/Web.Api -- --context <Name>DbContext`
2. Replace `EnsureSchemaAsync<T>` with `ctx.Database.MigrateAsync()`.

Naming convention is **snake_case** for tables and columns
(`UseSnakeCaseNamingConvention()` is registered in every module's DbContext
setup). C# property `LastLoginAtUtc` → column `last_login_at_utc`.

### 5.2 Seeding

[`Web.Api/Extensions/SeedExtensions.cs`](../../src/Web.Api/Extensions/SeedExtensions.cs)
runs once at startup in Development. Each module's seeder is idempotent
(no-ops if data already present). To re-seed from a clean state:

```bash
docker compose down -v          # destroys the postgres volume
docker compose up               # seeders re-run on first request
```

(Aspire equivalent: the dashboard has a "Stop" then "Delete data volume"
control on the postgres resource.)

### 5.3 The demo users

Defined in
[`Modules.Identity.Infrastructure/Seed/IdentitySeeder.cs:25-35`](../../src/Modules/Identity/Modules.Identity.Infrastructure/Seed/IdentitySeeder.cs#L25-L35).
Eight users; all share the password `Telco!2025` (BCrypt cost 11). The four
roles are:

| Role     | Constant value | Policies it satisfies                                |
|----------|----------------|------------------------------------------------------|
| engineer | `engineer`     | `RequireEngineer`                                    |
| manager  | `manager`      | `RequireEngineer`, `RequireManager`                  |
| admin    | `admin`        | `RequireEngineer`, `RequireManager`, `RequireAdmin`  |
| viewer   | `viewer`       | none — read-only on Bearer-only endpoints            |

Mapping is in
[`src/Web.Api/Program.cs:75-80`](../../src/Web.Api/Program.cs#L75-L80).

### 5.4 Auth — JWT bearer

- The login flow returns `accessToken` (HS256, secret = `Jwt:Secret`,
  default lifetime 30 min) and `refreshToken` (raw value returned to client,
  SHA-256 hash stored).
- `ClockSkew = 30s` — be aware when testing expiry.
- Claims include `sub`, `email`, `handle`, `role`, `team`, `region`. The
  `Ask` endpoint pulls `handle` and `role` to attribute Copilot queries in
  the audit log
  ([`Web.Api/Endpoints/Chat/Ask.cs:19-22`](../../src/Web.Api/Endpoints/Chat/Ask.cs#L19-L22)).
- The signing key is **read at startup** from `Jwt:Secret`. If it is
  missing, `Program.cs` throws and the API will not boot — this is
  intentional, fail loud rather than start with an unsigned-token bug.

### 5.5 CQRS pipeline behaviors

Every MediatR command/query is wrapped in this order:

1. `ExceptionHandlingPipelineBehavior` (outermost) — log + rethrow.
2. `RequestLoggingPipelineBehavior` — Serilog enrichment.
3. `ValidationPipelineBehavior` — FluentValidation if a validator is
   registered. If validation fails, return
   `Result.Failure(Error.Validation(...))` *without* invoking the handler.
4. `QueryCachingPipelineBehavior` (innermost) — only kicks in for
   `ICachedQuery` implementations (`GetMapQuery`, `GetMetricsQuery`).

Order matters. Logging wraps validation so even validation failures appear
in logs. Caching is innermost so cache hits short-circuit *after* validation
(no point caching invalid input).

### 5.6 Caching

- Backed by Redis via `IDistributedCache`
  ([`Infrastructure/Caching/CacheService.cs`](../../src/Infrastructure/Caching/CacheService.cs)).
- Trigger by implementing `ICachedQuery` on your query record:
  ```csharp
  public sealed record GetMapQuery : IQuery<MapResponse>, ICachedQuery
  {
      public string CacheKey   => "network:map";
      public TimeSpan? Expiration => TimeSpan.FromSeconds(30);
  }
  ```
- The pipeline behavior reads/writes around the handler. **No manual cache
  calls in handlers** — that breaks invalidation reasoning.
- Cache keys are flat strings. Pick a `module:<entity>:<id>` convention.
- For invalidation, write to the cache directly via `ICacheService.RemoveAsync`
  from the command handler that mutated the underlying data.

### 5.7 Logging — Serilog + request enrichment

- Configured from `appsettings*.json` (see Serilog block in
  [`appsettings.Development.json`](../../src/Web.Api/appsettings.Development.json)).
- Sinks: Console (always) + Seq (`http://seq:5341`, optional — bring up Seq
  via compose if you want a local dashboard).
- Every request gets enriched with `MachineName`, `ThreadId`, and a
  correlation id (via
  [`Middleware/RequestContextLoggingMiddleware.cs`](../../src/Web.Api/Middleware/RequestContextLoggingMiddleware.cs)).
- `Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore` are pinned at
  `Warning` to keep the noise down.

### 5.8 Telemetry — OpenTelemetry

`ServiceDefaults.Extensions.ConfigureOpenTelemetry` enables ASP.NET Core +
HttpClient + runtime metrics, plus tracing on the same. To export:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://your-collector:4317
```

Aspire's local dashboard already shows traces and metrics without any
exporter config.

### 5.9 Health checks

- `/health` (mapped in `Program.cs:121-124`) returns the
  AspNetCore.HealthChecks.UI JSON envelope. This is what compose's
  `depends_on healthcheck` would target if we wired it.
- `/alive` (dev only) returns just the `live`-tagged checks.
- The health-check builder adds Npgsql + Redis probes automatically when the
  connection strings are present
  ([`Infrastructure/DependencyInjection.cs:58-72`](../../src/Infrastructure/DependencyInjection.cs#L58-L72)).

---

## 6. Common workflows

### 6.1 Adding a new endpoint to an existing module

Worked example: add `GET /api/alerts/stats` returning aggregate counts.

1. **Define the query** in
   `Modules.Alerts.Application/Stats/GetAlertStatsQuery.cs`:
   ```csharp
   public sealed record GetAlertStatsQuery : IQuery<AlertStatsResponse>;

   public sealed record AlertStatsResponse(int Total, int Critical, int Warn, int Info);
   ```
2. **Implement the handler** alongside it:
   ```csharp
   internal sealed class GetAlertStatsQueryHandler(AlertsDbContext db)
       : IQueryHandler<GetAlertStatsQuery, AlertStatsResponse>
   {
       public async Task<Result<AlertStatsResponse>> Handle(GetAlertStatsQuery req, CancellationToken ct)
       {
           // ... EF query …
           return Result.Success(new AlertStatsResponse(...));
       }
   }
   ```
3. **Register dependencies if needed** in `AddAlertsApplication` (most
   handlers are auto-discovered via MediatR assembly scanning, so this is
   usually a no-op).
4. **Add the endpoint** in `Web.Api/Endpoints/Alerts/Stats.cs`:
   ```csharp
   public sealed class Stats : IEndpoint
   {
       public void MapEndpoint(IEndpointRouteBuilder app) =>
           app.MapGet("alerts/stats", async (ISender sender, CancellationToken ct) =>
           {
               var result = await sender.Send(new GetAlertStatsQuery(), ct);
               return result.Match(Results.Ok, CustomResults.Problem);
           })
           .WithTags(Tags.Alerts)
           .RequireAuthorization();
   }
   ```
5. **Hand-test**: `curl -H "Authorization: Bearer <token>"
   http://localhost/api/alerts/stats`.
6. **Update the frontend client** — add the type to
   [`frontend/src/lib/types.ts`](../../frontend/src/lib/types.ts) and the
   method to [`frontend/src/lib/api.ts`](../../frontend/src/lib/api.ts).

### 6.2 Adding a new module

Rare, but possible. Copy the four-project skeleton (`.Domain`,
`.Application`, `.Infrastructure`, `.Api`) from the smallest existing module
(Analytics is the most compact). Then:

1. Add references in `TelcoPilot.slnx`.
2. Add `Add<Name>Application` and `Add<Name>Infrastructure` extension
   methods.
3. Wire them into [`Web.Api/Program.cs:82-94`](../../src/Web.Api/Program.cs#L82-L94)
   *in dependency order*.
4. Add the module's DbContext to
   [`MigrationExtensions.ApplyMigrationsAsync`](../../src/Web.Api/Extensions/MigrationExtensions.cs).
5. Add the seeder to
   [`SeedExtensions.SeedDataAsync`](../../src/Web.Api/Extensions/SeedExtensions.cs).
6. Add csproj references to
   [`deploy/Dockerfile.backend`](../../deploy/Dockerfile.backend) (yes, all
   four — the Dockerfile copies them up-front for layer caching).

### 6.3 Adding a new field to an existing entity

1. Add the property to the domain entity in `.Domain/`.
2. Update the entity's EF configuration in
   `.Infrastructure/Database/Configurations/<Entity>Configuration.cs` if
   non-default mapping is needed.
3. **Today (no migrations):** drop the volume — `docker compose down -v` —
   and let `EnsureCreatedAsync` rebuild. Persistence loss is acceptable in
   demo mode; not in prod.
4. **Tomorrow (with migrations):** `dotnet ef migrations add <Name>` against
   the module's project.
5. Update the response DTO in `.Application/`.
6. Update the frontend type and any consuming components.

### 6.4 Calling another module from your handler

Example: an Alerts handler wants to write an audit entry to Analytics.

```csharp
internal sealed class AcknowledgeAlertCommandHandler(
    AlertsDbContext db,
    IAnalyticsApi analytics)              // <-- inject the .Api interface, NEVER the concrete type
    : ICommandHandler<AcknowledgeAlertCommand>
{
    public async Task<Result> Handle(AcknowledgeAlertCommand cmd, CancellationToken ct)
    {
        // ... mutate the alert ...
        await db.SaveChangesAsync(ct);
        await analytics.WriteAuditAsync(...);
        return Result.Success();
    }
}
```

The implementation of `IAnalyticsApi` lives in
`Modules.Analytics.Infrastructure.Api/AnalyticsApi.cs` and is registered in
`AddAnalyticsInfrastructure`. The Alerts module never references
`Modules.Analytics.Infrastructure` — only `Modules.Analytics.Api`.

### 6.5 Switching the AI provider live

Without rebuilding:

```bash
# Aspire dev:
dotnet user-secrets --project src/AppHost set "Ai:Provider" "AzureOpenAi"
# (set Endpoint / ApiKey / Deployment too)
# Restart AppHost.

# Docker compose:
echo "AI_PROVIDER=AzureOpenAi" >> .env
echo "AZURE_OPENAI_ENDPOINT=https://<your>.openai.azure.com/" >> .env
echo "AZURE_OPENAI_API_KEY=<key>" >> .env
docker compose up -d --force-recreate backend
```

To verify it took effect, ask the Copilot anything and check the
`provider` field in the response — `"AzureOpenAi"` vs `"Mock"`.

---

## 7. Conventions and norms

- **Result, not exceptions.** Handlers return `Result<T>`. Reserve exceptions
  for *unexpected* failures (DB connection lost, NPE you missed). Expected
  business failures are `Result.Failure(Error.Validation/NotFound/Conflict(...))`.
- **Records over classes** for DTOs and query/command types. Always
  `sealed`.
- **`internal sealed`** handlers — handlers don't need to be public; sealing
  them helps the JIT and the SonarAnalyzer rules.
- **Snake-case** Postgres columns (auto-applied via EF naming convention).
- **No magic strings** for roles or policies — use
  [`Modules.Identity.Application.Authorization.Roles`](../../src/Modules/Identity/Modules.Identity.Application/Authorization/Policies.cs)
  and `Policies` constants.
- **No DI service locator.** Constructor injection only. If you find
  yourself reaching for `IServiceProvider`, you're probably setting up a
  factory — extract it.
- **Async all the way down.** Handlers, repos, EF calls — async. Always pass
  `CancellationToken` through.
- **Don't `Task.Wait` / `.Result`**. Sonar will fail the build.
- **Comments**: explain *why*, not *what*. The codebase already does this
  well — preserve the style.

---

## 8. Debugging cheat sheet

| Symptom                                       | Likely cause                                                                                            | Where to look                                                                                                  |
|-----------------------------------------------|---------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------|
| API throws on startup with "Jwt configuration is missing" | `Jwt:Secret` not set                                                                                | [`Program.cs:58-59`](../../src/Web.Api/Program.cs#L58-L59) — set via user-secrets / env.                         |
| 500 on every request, logs show DB connection refused | Postgres not up, or wrong host/port                                                                  | `appsettings.Development.json` connection string vs your env.                                                  |
| 401 with valid token                          | Token signed with a different `Jwt:Secret` than the API was started with                                | Restart API after rotating the secret; tell the frontend to log out + back in.                                 |
| 403 instead of 401                            | Token is valid; role is insufficient                                                                    | Decode the JWT, compare to the policy on the endpoint.                                                         |
| Copilot returns "Mock" but you set provider to AzureOpenAi | Endpoint or key empty → silent fallback                                                              | [`Modules.Ai.Infrastructure/DependencyInjection.cs:34-37`](../../src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L34-L37) |
| New table not created on next run             | The duplicate-schema swallow in `MigrationExtensions` masks creation failures too                       | Drop the volume (`docker compose down -v`) or attach a debugger.                                               |
| `dotnet build` fails on a Sonar warning       | Warnings are errors. Read the rule.                                                                     | [`Directory.Build.props`](../../Directory.Build.props) — do not relax globally.                                |

---

## 9. References

- The product README at [`README.md`](../../README.md) — keeps the marketing
  description and the architecture diagram in sync.
- The composition root [`Web.Api/Program.cs`](../../src/Web.Api/Program.cs)
  — read this end-to-end on day one.
- The Aspire entry point
  [`AppHost/Program.cs`](../../src/AppHost/Program.cs).
- The AI module wiring
  [`Modules.Ai.Infrastructure/DependencyInjection.cs`](../../src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs).
- Microsoft docs:
  [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/),
  [Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis),
  [EF Core 10](https://learn.microsoft.com/ef/core/),
  [Semantic Kernel](https://learn.microsoft.com/semantic-kernel/).
