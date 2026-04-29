# Technology Stack

This document provides a complete inventory of every technology used in TelcoPilot, the version selected, its role in the system, and the rationale for choosing it over alternatives.

---

## Full Technology Stack Table

### Backend

| Layer | Technology | Version | Role | Why This Choice |
|---|---|---|---|---|
| Runtime | .NET | 10.0 | Application host and runtime | Latest LTS-adjacent .NET with C# 13; Aspire-native; performance improvements in ASP.NET Core minimal APIs |
| Web framework | ASP.NET Core Minimal APIs | .NET 10 | HTTP endpoint registration and routing | Minimal APIs eliminate controller ceremony; `IEndpoint` pattern keeps endpoints discoverable and testable; groups cleanly under `/api` |
| CQRS/Mediator | MediatR | 12.x | In-process command/query dispatch; pipeline behaviors | Decouples endpoint from handler; enables uniform pipeline (validation, logging, caching) without cross-cutting code in handlers |
| ORM | Entity Framework Core | 9.x (Npgsql provider) | Database access, schema management | EF Core with snake_case naming convention; per-module DbContext for schema isolation; strongly typed queries; pgvector support via Npgsql |
| AI orchestration | Semantic Kernel | 1.74 | LLM orchestration, automatic function calling, plugin registration | Azure OpenAI-native; structured plugin system (KernelFunction attribute); FunctionChoiceBehavior.Auto() enables agentic tool use without manual routing |
| Validation | FluentValidation | 11.x | Request validation in MediatR pipeline | Declarative rule chains; integrates cleanly with ValidationPipelineBehavior; produces structured error responses compatible with Result<T> |
| Logging | Serilog | 4.x | Structured logging with contextual enrichment | Sink-agnostic; reads from appsettings.json; integrates with UseSerilogRequestLogging() for per-request timing; OpenTelemetry-compatible |
| Password hashing | BCrypt.Net-Next | 5.x | Credential hashing with adaptive cost | BCrypt cost 11 provides ~300ms hash time — sufficient to resist brute force without noticeable login latency |
| JWT | Microsoft.IdentityModel.Tokens | .NET 10 built-in | JWT generation and validation | Native ASP.NET Core integration; HMAC-SHA256; configurable issuer/audience/clock-skew |
| Caching client | StackExchange.Redis | 2.x | Redis client for QueryCachingPipelineBehavior and ICacheService | Industry-standard .NET Redis client; connection pooling; async-first API |
| Health checks | AspNetCore.HealthChecks.UI.Client | 8.x | Health endpoint at `/health` with structured response | Standard format readable by load balancers and container orchestrators |
| OpenTelemetry | Aspire ServiceDefaults | .NET 10 | Distributed tracing, metrics, logging export | Pre-configured OTLP export via `AddServiceDefaults()`; no manual instrumentation needed for HTTP/DB spans |
| pgvector | Pgvector (Npgsql extension) | 0.3.x | Vector type for EF Core, cosine distance queries | Required for RAG similarity search; registered at NpgsqlDataSource level so EF parameter serialization works correctly |

### Frontend

| Layer | Technology | Version | Role | Why This Choice |
|---|---|---|---|---|
| Framework | Next.js | 15 | Full-stack React framework with App Router | App Router enables per-route layouts, server/client component split, and middleware-based auth gating |
| UI library | React | 19 | Component model and state management | React 19 concurrent features; hooks-only state (no Redux needed at this complexity level) |
| Language | TypeScript | 5.x | Type safety across the frontend | Types mirror backend DTOs; `AuthUser`, `CopilotAnswer`, `Tower` etc. provide full IDE support and catch contract drift at compile time |
| Build | Node.js | 22 (Alpine) | Build and runtime host | LTS; Alpine image keeps final container to ~120MB in Next.js standalone mode |
| API client | Fetch (custom wrapper) | Browser/Node built-in | Typed API client in `lib/api.ts` | No third-party HTTP client needed; custom `request<T>()` handles token injection, error normalisation, and FormData content-type detection |

### AI / Intelligence

| Layer | Technology | Version | Role | Why This Choice |
|---|---|---|---|---|
| LLM provider | Azure OpenAI | GPT-4o-mini | Chat completion with function calling | GPT-4o-mini provides strong reasoning at low cost; Azure deployment satisfies enterprise data-residency requirements |
| Embedding model | Azure OpenAI | text-embedding-* (configurable) | Semantic embedding for RAG pipeline | Same Azure resource as chat completion; 1536-dimension default matches text-embedding-3-small output |
| Fallback embedder | HashingEmbeddingGenerator | In-process | Deterministic embedding for offline/Mock mode | Token-overlap relevance without any API cost; RAG pipeline still functions end-to-end |
| Orchestration | Semantic Kernel | 1.74 | Kernel plugin registration, function calling, chat history | Native Azure OpenAI support; `FunctionChoiceBehavior.Auto()` lets the model decide which skills to invoke without manual routing logic |
| Vector store | pgvector (PostgreSQL extension) | pg17 | Cosine distance similarity search for RAG retrieval | No additional database to manage; `<=>` cosine distance operator; integrates via Npgsql EF Core provider; `PgVectorKnowledgeStore` implements `IKnowledgeStore` |

### Data Infrastructure

| Layer | Technology | Version | Role | Why This Choice |
|---|---|---|---|---|
| Primary database | PostgreSQL | 17 (pgvector/pgvector:pg17 image) | All relational data + vector storage | pgvector image is upstream pg17 with the `vector` extension preinstalled; single database for all modules reduces operational complexity |
| Cache / session | Redis | 7-alpine | Query result caching; AOF persistence | AOF (`--appendonly yes`) survives container restarts; Alpine image; StackExchange.Redis provides connection pooling |
| Document storage | Local filesystem (named volume) | Docker volume | RAG document file storage | `telcopilot-doc-store` named volume survives `docker compose down`; production path is Azure Blob Storage (PlaceholderCloudStorageProvider already wired) |

### Infrastructure and Operations

| Layer | Technology | Version | Role | Why This Choice |
|---|---|---|---|---|
| API gateway / reverse proxy | NGINX | 1.27-alpine | Single public port, `/api/*` and `/` routing | Zero-config reverse proxy; single upstream per logical service; `keepalive 16` for connection reuse; `server_tokens off` for security |
| Containerisation | Docker / Docker Compose | Docker Compose v2 | 5-service orchestration: nginx, frontend, backend, postgres, redis | Single `docker compose up --build` deploys the entire stack; named volumes for persistence; bridge network isolates services |
| Backend build | .NET SDK multi-stage Dockerfile | .NET SDK 10 → aspnet:10 | Layer-cached restore, Release publish, minimal runtime image | Separate restore layer caches `dotnet restore` on package-lock change; final image uses the smaller `aspnet:10` runtime-only image |
| Frontend build | Node multi-stage Dockerfile | node:22-alpine (3 stages) | Dependency install, Next.js build, Next standalone runner | Next.js `output: 'standalone'` bundles the minimum runtime; final image is ~120MB; `npm install --legacy-peer-deps` handles peer resolution |
| Local dev orchestration | .NET Aspire AppHost | .NET Aspire .NET 10 | Service discovery, dashboard, hot-reload wiring | Aspire dashboard shows logs, traces, and metrics from all services; `AddServiceDefaults()` wires OTLP without manual configuration |

---

## Why These Choices Over Alternatives

### Modular Monolith vs. Microservices
Covered in depth in [02_System_Architecture.md](02_System_Architecture.md). The short answer: in-process MediatR pipeline behaviors require same-process execution; cross-module calls are sub-millisecond method calls, not HTTP hops; single deployment unit simplifies the hackathon demo.

### MediatR vs. Direct Service Injection
Direct service injection would allow handlers to call any service in the container. MediatR's `ISender` interface keeps the coupling explicit: an endpoint sends a command or query with a specific shape, and the handler for that shape is resolved. This makes the behavior pipeline universally applicable without decorating every service class.

### PostgreSQL + pgvector vs. Dedicated Vector Database
Adding a dedicated vector database (Pinecone, Weaviate, Qdrant) would introduce another service to operate, monitor, and pay for. The pgvector extension is pre-installed on the PostgreSQL 17 image used in the Docker Compose stack (`pgvector/pgvector:pg17`). The `PgVectorKnowledgeStore` implementation uses the `<=>` cosine distance operator via `VectorExtensions.CosineDistance()` — fully functional vector similarity search without any additional infrastructure.

### Next.js App Router vs. Pages Router
App Router enables per-route layouts (`(authed)/layout.tsx` wraps all authenticated pages with the sidebar and top bar) without duplication. The `middleware.ts` pattern for auth gating is cleaner in App Router. React 19's concurrent features are available only in App Router.

### Semantic Kernel vs. LangChain / LlamaIndex (.NET)
Semantic Kernel is Microsoft's official .NET AI SDK, maintained in lockstep with the Azure OpenAI SDK. `FunctionChoiceBehavior.Auto()` enables agentic function calling with minimal configuration — the model receives JSON schema for each `[KernelFunction]`-decorated method and decides which to invoke. The `AddAzureOpenAIChatCompletion()` extension integrates directly with the Kernel builder. LangChain.NET and LlamaIndex .NET are less mature and less actively maintained for production .NET 10 use cases.

### Redis AOF vs. No Persistence
`redis-server --appendonly yes` in the Docker Compose command enables Append-Only File persistence. This means the Redis cache survives container restarts — cached query results are not lost on `docker compose restart`. For a demo environment this avoids a cold-start period where all queries hit the database. The `telcopilot-redis-data` named volume preserves the AOF file across `docker compose down`.

### BCrypt Cost 11
BCrypt cost 10 is the historical default. Cost 11 doubles the hash time (approximately 300ms on typical NOC server hardware), which is imperceptible at login but doubles the cost of a brute-force attack against a leaked password hash. The marginal UX cost is zero; the security improvement is measurable.
