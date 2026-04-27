# DevOps Onboarding — TelcoPilot Infrastructure

> Audience: anyone responsible for **building, running, deploying, observing,
> or troubleshooting** the TelcoPilot stack — locally or in production. Read
> [`README.md`](README.md) first for the cross-cutting orientation, then
> skim [`backend.md`](backend.md) §1–§2 to internalise the modular-monolith
> invariant before touching any compose file or NGINX config.

---

## 1. The deployment topology

Five containers, one Docker network, one public port:

```
                        ┌──────────────────┐
                Browser ─►   nginx :80     │  ← only exposed port
                        │  (reverse proxy) │
                        └──┬────────────┬──┘
                  proxy_pass│            │proxy_pass
                       /api │            │ /
                            ▼            ▼
                  ┌─────────────┐  ┌─────────────┐
                  │ backend     │  │ frontend    │
                  │ Web.Api     │  │ Next.js     │
                  │ :8080       │  │ :3000       │
                  └────┬─┬──────┘  └─────────────┘
                       │ │
              postgres │ │ redis
                       ▼ ▼
              ┌──────────┐  ┌─────────┐
              │ postgres │  │ redis   │
              │ :5432    │  │ :6379   │
              └──────────┘  └─────────┘
                       │
                volume: telcopilot-pg-data
```

Defined in [`docker-compose.yml`](../../docker-compose.yml). The five services
are:

| Service                  | Image / build context              | Exposed publicly                     | Persists data?            |
|--------------------------|------------------------------------|--------------------------------------|---------------------------|
| `nginx`                  | `nginx:1.27-alpine`                | **Yes — port 80**                    | No                        |
| `frontend`               | built from `./frontend/Dockerfile` | No (only reachable via nginx)        | No                        |
| `backend`                | built from `./deploy/Dockerfile.backend` | No (only reachable via nginx) | No (it's stateless)       |
| `postgres`               | `postgres:17-alpine`               | No                                   | **Yes — volume `telcopilot-pg-data`** |
| `redis`                  | `redis:7-alpine`                   | No                                   | No (cache only)           |

**Network**: `tekhack-net` (bridge). All five services share it; intra-stack
DNS resolves by service name (`backend`, `postgres`, `redis`, `frontend`,
`nginx`).

**Restart policy**: `unless-stopped` on every service so a docker daemon
restart brings the stack back automatically.

---

## 2. Prerequisites

| Tool                                                                   | Version | Why                                                                       |
|------------------------------------------------------------------------|---------|---------------------------------------------------------------------------|
| [Docker Engine](https://docs.docker.com/engine/install/) / Docker Desktop | 24+  | runs the stack                                                            |
| [Docker Compose v2](https://docs.docker.com/compose/) (built-in to modern Docker) | latest | `docker compose ...` (note: space, not hyphen — the v1 `docker-compose` is EOL) |
| [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)       | 10.0.x  | only if you also run Aspire locally                                       |
| [Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling) | latest  | only for `dotnet run --project src/AppHost`                               |
| [Node.js 22 LTS](https://nodejs.org/)                                  | 22      | only for direct frontend dev outside Docker                               |
| [`openssl`](https://www.openssl.org/)                                  | any     | for generating the JWT signing key                                        |
| [`psql`](https://www.postgresql.org/download/) (optional)              | 17      | poking at the demo database                                               |

> **Windows users**: prefer Docker Desktop with the WSL2 backend. Native
> Hyper-V works but is slower. Make sure the project is checked out *inside*
> the WSL filesystem (e.g. `~/src/...`) — checking it out on `/mnt/c/...`
> kills filesystem performance.

---

## 3. Local environments — three modes, choose by need

### 3.1 Mode 1 — Docker compose (full production-shaped stack)

Best for: full demo, end-to-end testing, anything where you want the same
topology that ships.

```bash
cp .env.example .env                  # only needed once
docker compose up --build             # ~3 minutes the first time
open http://localhost                 # → /login
```

**What happens on `up --build`:**

1. Compose reads [`.env`](../../.env.example) (it is auto-loaded — no `--env-file`
   flag needed).
2. **Postgres** image is pulled (~30 MB), volume `telcopilot-pg-data` is
   created if missing. A healthcheck (`pg_isready`) is registered.
3. **Redis** image is pulled (~30 MB).
4. **Backend** is built from `deploy/Dockerfile.backend`:
   - SDK 10 stage restores all module csprojs (cached layer if no csproj
     changed).
   - SDK 10 stage compiles + `dotnet publish -c Release` to `/app/publish`.
   - `aspnet:10` final stage copies the publish output and exposes :8080.
5. **Frontend** is built from `frontend/Dockerfile`:
   - `npm install --legacy-peer-deps` (cached layer if `package-lock.json`
     unchanged).
   - `npm run build` produces the standalone bundle.
   - Final `node:22-alpine` stage copies `public/`, `.next/standalone`,
     `.next/static` and runs `node server.js`.
6. **NGINX** mounts [`deploy/nginx/nginx.conf`](../../deploy/nginx/nginx.conf)
   read-only and starts.
7. Backend waits for postgres healthcheck to pass before starting.
   On first run, it bootstraps the schema (idempotent
   `EnsureCreatedAsync`) and runs the seeders, so the DB is populated within
   ~5 seconds of startup.

**To stop:**
```bash
docker compose down              # stop + remove containers; volume kept
docker compose down -v           # also drop the postgres volume (RESETS DB)
```

**To rebuild a single service after a code change:**
```bash
docker compose up -d --build backend       # backend only
docker compose up -d --build frontend      # frontend only
```

### 3.2 Mode 2 — Aspire (recommended for backend dev with hot reload)

Best for: any backend code change you want to iterate on.

```bash
# One-time
dotnet workload install aspire
dotnet user-secrets --project src/AppHost init
dotnet user-secrets --project src/AppHost set "Jwt:Secret" "$(openssl rand -base64 48)"

# Each session
dotnet run --project src/AppHost
```

What it does ([`src/AppHost/Program.cs`](../../src/AppHost/Program.cs)):

- Boots **Postgres** via Aspire's container resource on port `5723` (note:
  not the default 5432, to avoid clashing with a local Postgres install) +
  the [PgWeb](https://github.com/sosedoff/pgweb) admin UI.
- Boots **Redis** on port 6379 with `ContainerLifetime.Persistent` (the
  container survives Aspire restarts so cache state isn't lost).
- Launches **Web.Api** as a project resource — meaning it runs the actual
  .NET process on your machine (not a container), so debugger attach + hot
  reload Just Work.
- Launches the **Next.js dev server** as an `AddNpmApp` resource pointed at
  `../../frontend` with `npm run dev`.
- Forwards all the JWT + AI configuration from your user-secrets into the
  API process as environment variables.

The **Aspire dashboard** opens at <https://localhost:17017>. From there:

- **Resources** tab: live status, logs, env vars, and the dynamically-assigned
  port for each project.
- **Console** tab: combined log stream.
- **Traces** tab: distributed traces (just the API + outbound HTTP, since
  there's only one process).
- **Metrics** tab: ASP.NET Core + HttpClient + runtime counters.

### 3.3 Mode 3 — Hybrid (frontend host-side, backend dockerised)

Best for: heavy UI work where you want hot-reload on Next but don't want to
rebuild the backend image on every change.

```bash
docker compose up -d postgres redis backend
cd frontend
BACKEND_INTERNAL_URL=http://localhost:8080 npm run dev
```

Note: `backend` is not exposed publicly via compose, so to make
`localhost:8080` reachable you need to add a port mapping. Either edit
`docker-compose.yml` and add `ports: ["8080:8080"]` to the backend service,
or temporarily run `docker compose run --service-ports backend` instead of
the bg compose `up`. (For day-to-day work, the Aspire path is usually less
fiddly.)

---

## 4. Configuration — every env var explained

The complete production-relevant configuration surface is in
[`.env.example`](../../.env.example) and
[`docker-compose.yml`](../../docker-compose.yml). Mapped to the .NET
configuration system (with `__` as the section separator —
[ASP.NET Core convention](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/)):

| `.env` key                  | Compose env var (passed to backend)            | .NET config path                       | Default                                                            | Notes                                                                                            |
|-----------------------------|------------------------------------------------|----------------------------------------|--------------------------------------------------------------------|--------------------------------------------------------------------------------------------------|
| `POSTGRES_PASSWORD`         | `POSTGRES_PASSWORD` (postgres) + interpolated into the backend connection string | (none — used in `ConnectionStrings:Database`) | `postgres`                                                         | Rotate before any non-local deployment.                                                          |
| `JWT_SECRET`                | `Jwt__Secret`                                  | `Jwt:Secret`                           | `dev-secret-replace-with-32+char-random-via-env-please`            | **Must be ≥ 32 chars.** Generate with `openssl rand -base64 48`.                                 |
| (none — hardcoded)          | `Jwt__Issuer=telcopilot`                       | `Jwt:Issuer`                           | `telcopilot`                                                       | Change only if you're issuing tokens elsewhere.                                                  |
| (none — hardcoded)          | `Jwt__Audience=telcopilot.api`                 | `Jwt:Audience`                         | `telcopilot.api`                                                   | Same.                                                                                            |
| `AI_PROVIDER`               | `Ai__Provider`                                 | `Ai:Provider`                          | `Mock`                                                             | `Mock` or `AzureOpenAi`. **Falls back to Mock silently** if Azure creds are blank.               |
| `AZURE_OPENAI_ENDPOINT`     | `Ai__AzureOpenAi__Endpoint`                    | `Ai:AzureOpenAi:Endpoint`              | empty                                                              | Full URL incl. `https://` and trailing slash.                                                    |
| `AZURE_OPENAI_API_KEY`      | `Ai__AzureOpenAi__ApiKey`                      | `Ai:AzureOpenAi:ApiKey`                | empty                                                              | Treat as a high-value secret.                                                                    |
| `AZURE_OPENAI_DEPLOYMENT`   | `Ai__AzureOpenAi__Deployment`                  | `Ai:AzureOpenAi:Deployment`            | `gpt-4o-mini`                                                      | Must match the deployment name configured in your Azure OpenAI resource.                         |
| `ASPNETCORE_ENVIRONMENT`    | `ASPNETCORE_ENVIRONMENT`                       | (built-in)                             | `Development`                                                      | Flip to `Production` once you have real secrets wired. Disables Swagger + dev-only seeding.      |
| (none)                      | `ASPNETCORE_URLS=http://+:8080`                | (built-in)                             | `http://+:8080`                                                    | The container always binds 8080 internally.                                                      |
| (none)                      | `ConnectionStrings__Database=Host=postgres;...`| `ConnectionStrings:Database`           | (built from `POSTGRES_PASSWORD`)                                   | Compose composes this for you; in Aspire it is set automatically by `WithReference(db)`.         |
| (none)                      | `ConnectionStrings__Cache=redis:6379`          | `ConnectionStrings:Cache`              | `redis:6379`                                                       | Same — Aspire wires it via `WithReference(redis)`.                                               |

For the **frontend container**, the only env var that matters is:

| Var                       | Default                          | Notes                                                                                |
|---------------------------|----------------------------------|--------------------------------------------------------------------------------------|
| `NODE_ENV`                | `production` (in compose)        | Standard Next.js convention.                                                         |
| `BACKEND_INTERNAL_URL`    | `http://backend:8080` (in compose) | Used by [`next.config.mjs`](../../frontend/next.config.mjs) `/api/*` rewrite.       |

---

## 5. NGINX — the gateway, line by line

[`deploy/nginx/nginx.conf`](../../deploy/nginx/nginx.conf) is the only piece
of routing config in the repo. Key things to internalise:

```nginx
upstream telcopilot_backend  { server backend:8080;  keepalive 16; }
upstream telcopilot_frontend { server frontend:3000; keepalive 16; }
```

**Two upstreams. ONE per logical service.** This is the architectural
load-bearing detail. Splitting `telcopilot_backend` into `upstream
telcopilot_ai`, `upstream telcopilot_alerts`, etc. would imply per-module
containers, which would break the in-process MediatR contract. Don't.

```nginx
location /api/ {
    proxy_set_header Host              $host;
    proxy_set_header X-Real-IP         $remote_addr;
    proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header Authorization     $http_authorization;

    proxy_http_version 1.1;
    proxy_set_header Upgrade    $http_upgrade;
    proxy_set_header Connection $connection_upgrade;

    proxy_read_timeout  60s;
    proxy_send_timeout  60s;

    proxy_pass http://telcopilot_backend;
}
```

Notes:

- `Authorization` is forwarded explicitly (most NGINX setups do this by
  default, but pinning it avoids surprises with non-default proxy modules).
- `Upgrade` / `Connection` are wired for WebSockets / SSE. We don't use them
  today, but the frontend is ready (the Copilot is the obvious future
  consumer).
- The 60s read/send timeouts are generous to accommodate Azure OpenAI
  latencies on long Copilot answers. Don't shorten them without checking
  Copilot p99.
- Trailing slash on `location /api/` matters — it strips the prefix from
  the matched URL. Combined with `proxy_pass http://telcopilot_backend;`
  (no trailing slash), the full URL is forwarded verbatim, so
  `/api/auth/login` reaches the backend as `/api/auth/login`. The backend
  expects the `/api` prefix (see
  [`Program.cs:107`](../../src/Web.Api/Program.cs#L107)).

```nginx
location = /healthz { access_log off; return 200 "ok\n"; ... }
location = /backend-health { proxy_pass http://telcopilot_backend/health; }
location /swagger/ { proxy_pass http://telcopilot_backend; }
location / { ... proxy_pass http://telcopilot_frontend; }
```

- `/healthz` is a synthetic NGINX-side health probe.
- `/backend-health` proxies to the backend's `/health` endpoint.
- `/swagger/` is forwarded so demos can hit one origin
  (<http://localhost/swagger>).
- The catch-all `/` goes to the frontend.

When you change this file:

```bash
docker compose exec nginx nginx -t        # syntax check
docker compose restart nginx              # apply
```

Or simpler: `docker compose up -d nginx`.

---

## 6. Postgres — operational notes

- **Image**: `postgres:17-alpine`.
- **Volume**: `telcopilot-pg-data` (named docker volume). Data survives
  `docker compose down`; `docker compose down -v` wipes it.
- **DB / user / password**: `telcopilot` / `postgres` /
  `${POSTGRES_PASSWORD:-postgres}`.
- **Healthcheck**: `pg_isready -U postgres -d telcopilot`, every 5s, up to
  10 retries. Backend won't start until this passes.
- **Schemas**: created on first backend boot — `identity`, `network`,
  `alerts`, `analytics`, `ai`. See
  [`Web.Api/Extensions/MigrationExtensions.cs`](../../src/Web.Api/Extensions/MigrationExtensions.cs).
- **No migrations today**: bootstrap is `EnsureCreatedAsync` per DbContext
  with duplicate-schema swallowing for idempotency. Acceptable for demo,
  not for prod — see [`backend.md`](backend.md) §5.1 for the migration plan.

Connect from the host:

```bash
# Compose mode (postgres NOT exposed on host by default — add a port mapping)
docker compose exec postgres psql -U postgres -d telcopilot

# Aspire mode — postgres is on host port 5723 (custom):
psql -h localhost -p 5723 -U postgres -d telcopilot
# (PgWeb is also wired automatically; check the Aspire dashboard for the URL)
```

Useful queries:

```sql
-- list every TelcoPilot schema
\dn telcopilot* identity network alerts analytics ai

-- count tables per schema
SELECT table_schema, count(*) FROM information_schema.tables
 WHERE table_schema IN ('identity','network','alerts','analytics','ai')
 GROUP BY table_schema;

-- inspect a recent audit entry
SELECT * FROM analytics.audit_entries ORDER BY occurred_at_utc DESC LIMIT 10;
```

To **reset** the database (full wipe + reseed):

```bash
docker compose down -v
docker compose up
```

(Or in Aspire: stop the AppHost, remove the postgres data volume from the
Aspire dashboard or via `docker volume rm`, restart.)

---

## 7. Redis — operational notes

- **Image**: `redis:7-alpine`.
- **No password** in the demo configuration. Add `--requirepass <secret>`
  via `command:` in compose for any non-local deployment, and update the
  `ConnectionStrings:Cache` to include `,password=<secret>`.
- **No persistence** configured (we only cache derived data —
  `GetMapQuery`, `GetMetricsQuery`).
- **Eviction**: defaults (no `maxmemory` policy). Set one in production.

Inspect:

```bash
docker compose exec redis redis-cli
> KEYS *
> GET network:map
> TTL network:map
```

---

## 8. Building images

### 8.1 Backend (`deploy/Dockerfile.backend`)

Build context is the **repo root** (so the Dockerfile can `COPY` every
module's csproj individually). This is why `docker-compose.yml` sets
`context: .` and `dockerfile: deploy/Dockerfile.backend` for the backend
service.

The Dockerfile is a textbook two-stage .NET build with **csproj-first
restore** for layer caching:

1. Stage `build` (`mcr.microsoft.com/dotnet/sdk:10.0`):
   - Copy `Directory.Build.props`.
   - Copy every `.csproj` (one `COPY` per project — keeps the layer reusable).
   - `dotnet restore "src/Web.Api/Web.Api.csproj"` — restores all transitive
     refs.
   - `COPY . .` — bring in source.
   - `dotnet publish -c Release -o /app/publish /p:UseAppHost=false`.
2. Stage `final` (`mcr.microsoft.com/dotnet/aspnet:10.0`):
   - `EXPOSE 8080`, `ASPNETCORE_URLS=http://+:8080`.
   - Copy `/app/publish` and `ENTRYPOINT ["dotnet", "Web.Api.dll"]`.

When you add a new module, **you must also add its csproj copy lines to this
Dockerfile** (see [`backend.md`](backend.md) §6.2 step 6) — otherwise
`dotnet restore` will succeed locally but fail in Docker because the new
csproj wasn't copied into the image before restore.

### 8.2 Frontend (`frontend/Dockerfile`)

Build context: `./frontend`. Three stages (`deps`, `build`, `runner`) — see
[`frontend.md`](frontend.md) §8 for the breakdown.

---

## 9. Logging, monitoring, observability

### 9.1 Logs

- **Console logs** are produced by every service. View with:
  ```bash
  docker compose logs -f                 # tail all
  docker compose logs -f backend         # one service
  docker compose logs --since 5m nginx   # last 5 minutes
  ```
- **Structured logs** (Serilog JSON) are emitted by the backend. Pipe to
  `jq` if you want to filter:
  ```bash
  docker compose logs backend | jq -R 'fromjson? | select(.Level=="Error")'
  ```
- **Seq** (optional structured-log dashboard) — uncomment a `seq` service
  in compose if you want one. The backend's
  [`appsettings.Development.json`](../../src/Web.Api/appsettings.Development.json)
  already targets `http://seq:5341` so it'll pick it up automatically.

### 9.2 Metrics + traces (OpenTelemetry)

`ServiceDefaults` exports OTel metrics and traces. To send them somewhere:

```bash
# .env
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

…and add the collector to compose. The Aspire dashboard already collects
both without any extra config.

### 9.3 Health checks

| URL                                     | Returns                                                                             |
|-----------------------------------------|-------------------------------------------------------------------------------------|
| <http://localhost/healthz>              | NGINX synthetic `ok` (200).                                                         |
| <http://localhost/backend-health>       | Backend `/health` (Npgsql + Redis probes, formatted by AspNetCore.HealthChecks.UI). |
| <http://localhost:8080/health>          | Same as above, hit directly inside the docker network.                              |
| <http://localhost:8080/alive>           | Liveness-tagged subset only (dev only — see `MapDefaultEndpoints`).                 |

Wire these into your orchestrator's liveness / readiness probes. For
Kubernetes, `/alive` is a clean liveness probe; `/backend-health` is a clean
readiness probe (so dependencies have to be healthy before traffic flows).

### 9.4 Useful production probes

```bash
# Is the stack alive end-to-end?
curl -fsSL http://localhost/healthz && curl -fsSL http://localhost/backend-health

# Can I log in?
curl -X POST http://localhost/api/auth/login \
     -H 'Content-Type: application/json' \
     -d '{"email":"oluwaseun.a@telco.lag","password":"Telco!2025"}'

# Is the AI module wired correctly? (need a token first)
TOKEN=$(curl -s ... | jq -r .accessToken)
curl -H "Authorization: Bearer $TOKEN" \
     -H 'Content-Type: application/json' \
     -d '{"query":"why is Lekki degraded?"}' \
     http://localhost/api/chat | jq .provider
# → "Mock" or "AzureOpenAi"
```

---

## 10. Secrets management

The repo has three different mechanisms, used in different places:

| Where                      | Mechanism                                | What lives there                                                 |
|----------------------------|------------------------------------------|------------------------------------------------------------------|
| Aspire local dev           | [.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) on `src/AppHost` | `Jwt:Secret`, `Ai:AzureOpenAi:ApiKey`, etc.                      |
| Standalone `dotnet run` on Web.Api | user-secrets on `src/Web.Api`            | same — but Aspire mode is preferred so values are set in one place. |
| Docker compose             | `.env` (gitignored)                      | `JWT_SECRET`, `POSTGRES_PASSWORD`, `AZURE_OPENAI_API_KEY`, …     |
| Production                 | external secret store + `--env-file`     | same — but mounted from Vault / Doppler / AWS Secrets Manager / Key Vault. |

**Do not** commit any of: `.env` (without the `.example` suffix), a real
`appsettings.Production.json` with secrets, a docker-compose override that
inlines an API key. The `.gitignore` already excludes `.env`; verify with
`git status` after editing.

To rotate a secret in production:

1. Update the value in the secret store.
2. `docker compose up -d --force-recreate backend` (so the env var is re-read).
3. If you rotated `Jwt:Secret`, every issued JWT becomes invalid — clients
   must re-authenticate. Communicate the rotation window.

---

## 11. Common ops tasks

### 11.1 Reset everything (nuclear)

```bash
docker compose down -v
docker volume prune -f                   # removes the postgres data + any dangling volumes
docker system prune -f                   # cleans up dangling images
docker compose up --build
```

### 11.2 Update the backend image after a code change

```bash
docker compose up -d --build backend
docker compose logs -f backend
```

### 11.3 Update the frontend image after a code change

```bash
docker compose up -d --build frontend
```

### 11.4 Tail backend errors only

```bash
docker compose logs backend 2>&1 | grep -E '(ERROR|Error|Exception)'
```

### 11.5 Connect a debugger to the backend in compose

The compose backend image is `Release` and stripped — you can't usefully
debug it. For interactive debugging, **use Aspire** (the Web.Api process
runs on the host, so `dotnet attach` / VS / Rider work normally).

### 11.6 Pre-pull the base images (offline / slow network)

```bash
docker pull mcr.microsoft.com/dotnet/sdk:10.0
docker pull mcr.microsoft.com/dotnet/aspnet:10.0
docker pull node:22-alpine
docker pull nginx:1.27-alpine
docker pull postgres:17-alpine
docker pull redis:7-alpine
```

---

## 12. Troubleshooting first-boot

### "address already in use" / port 80 is taken
Port 80 is shared with anything else listening (IIS on Windows, Apache /
nginx host-side on Linux). Either stop the conflicting service or change
the compose mapping:
```yaml
nginx:
  ports:
    - "8080:80"     # → http://localhost:8080
```

### Backend exits immediately with "Jwt configuration is missing"
Set `JWT_SECRET` in `.env` (or pass `Jwt__Secret` directly). The app
intentionally refuses to boot without one — fail loud over running with a
broken auth chain.

### Backend logs "PostgresException: connection refused"
Almost always means postgres isn't healthy yet. Check
`docker compose ps`; the `postgres` service should report
`(healthy)` next to its status. If it stays `(unhealthy)`, check
`docker compose logs postgres`.

### `docker compose build backend` fails on `dotnet restore`
Most common reason: you added a new `.csproj` to the solution but forgot to
add the `COPY` line for it to
[`deploy/Dockerfile.backend`](../../deploy/Dockerfile.backend). The
restore-only stage doesn't have the source yet, so it will not see the new
project.

### `docker compose build frontend` fails on `npm install`
Likely a peer-dep mismatch. The Dockerfile already uses
`--legacy-peer-deps`. If your new dep needs a newer flag, bump the dep or
pin a transitive. Confirm `npm install --legacy-peer-deps` works on the
host first.

### Frontend builds but pages 404 in the browser
Probably a stale build artefact. From the project root:
```bash
docker compose down
docker compose up --build --force-recreate frontend
```

### Copilot answers in 200ms regardless of provider
You're getting the Mock. Either:
- `Ai:Provider` isn't set to `AzureOpenAi` (check the env in the running
  container: `docker compose exec backend env | grep Ai`).
- The endpoint or key is empty → silent fallback. Set both, recreate the
  backend.

### Postgres volume keeps coming back with stale data
`docker compose down` does **not** drop volumes. Use `docker compose
down -v` (note the `-v`) to remove the named volume. Don't forget to back
up first if anything matters.

### Aspire "Resource X failed to start"
Open the Aspire dashboard, click the failing resource → "Console". Read the
last error. 90% of the time it's a missing user-secret (Jwt or Ai).

---

## 13. Production deployment notes

This repo intentionally **does not ship a production deployment manifest**
(no Helm chart, no Terraform, no GitHub Actions workflow). Add one for your
target environment, but follow these rules:

- **Single backend container.** No matter the orchestrator (Kubernetes,
  ECS, Nomad, Cloud Run, App Service), deploy the backend as a single
  service with horizontally scalable replicas behind a single load-balanced
  endpoint. Do not split modules.
- **One Postgres, one Redis.** The backend is happy with managed offerings
  (RDS, Aurora, Azure Database for PostgreSQL, Upstash, Memorystore). Just
  point `ConnectionStrings:Database` and `ConnectionStrings:Cache` at them.
- **Real secrets store.** Vault / Doppler / Secrets Manager / Key Vault.
  Inject as env vars at runtime (the backend reads everything from
  `IConfiguration`, so any provider works).
- **HTTPS at the edge.** NGINX in this repo is plain-HTTP because it is
  meant to sit behind a TLS-terminating ingress (CloudFront, Cloudflare,
  ALB, Azure Front Door, etc.). Don't expose port 80 directly to the
  internet.
- **`ASPNETCORE_ENVIRONMENT=Production`**. This disables Swagger, disables
  the seeder, and keeps the boot path lean.
- **Replace `EnsureCreatedAsync` with EF Migrations.** See
  [`backend.md`](backend.md) §5.1.
- **Set `maxmemory-policy`** on Redis (e.g. `allkeys-lru`) — defaults will
  OOM under sustained cache pressure.
- **Health probes**: `/alive` for liveness, `/backend-health` (or backend
  `/health` directly) for readiness. The compose nginx already publishes
  both.
- **JWT key rotation**: a JWT issued with the old `Jwt:Secret` becomes
  invalid the moment the new key is loaded. Plan for a logout storm or
  implement key rollover (multiple valid keys for a window) before flipping
  in production.

---

## 14. CI/CD recommendations (not yet implemented)

Until a CI workflow is added, the minimum bar before merging to `main`:

```bash
dotnet build                              # zero warnings
dotnet test                               # if/when tests exist
cd frontend && npm run lint && npm run build
docker compose build                      # ensure both images build clean
docker compose up -d                      # smoke test
curl -fsSL http://localhost/healthz
curl -fsSL http://localhost/backend-health
```

A reasonable first GitHub Actions workflow would:

1. Build the backend (`dotnet build` + `dotnet test`).
2. Build the frontend (`npm ci --legacy-peer-deps && npm run build`).
3. Build both Docker images (no push).
4. Spin up compose, hit `/healthz`, hit `/api/auth/login`, tear down.
5. On `main` push: also tag and push images to your registry.

---

## 15. Quick reference

```bash
# === Docker compose ===
docker compose up --build                 # build + boot the full stack
docker compose up -d                      # boot in background
docker compose ps                         # status
docker compose logs -f <svc>              # tail one service
docker compose exec <svc> sh              # shell into a container
docker compose down                       # stop + remove containers
docker compose down -v                    # also drop the postgres volume

# === Aspire ===
dotnet user-secrets --project src/AppHost set "Jwt:Secret" "$(openssl rand -base64 48)"
dotnet run --project src/AppHost          # boots Postgres + Redis + Web.Api + Next dev
# → dashboard at https://localhost:17017

# === Probes ===
curl -fsSL http://localhost/healthz
curl -fsSL http://localhost/backend-health
curl http://localhost/api/auth/login \
     -H 'Content-Type: application/json' \
     -d '{"email":"oluwaseun.a@telco.lag","password":"Telco!2025"}'

# === Reset ===
docker compose down -v && docker compose up --build
```

Refer back to [`README.md`](README.md) for the cross-cutting orientation,
[`frontend.md`](frontend.md) for UI specifics, and
[`backend.md`](backend.md) for module-level details.
