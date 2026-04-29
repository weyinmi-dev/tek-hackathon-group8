# Setup and Local Development

This document is the complete getting-started guide for TelcoPilot. It covers two development options: Option A (Docker Compose — full stack, recommended for demos and first-time setup) and Option B (.NET Aspire + npm dev server — hot reload, recommended for active development).

---

## Prerequisites

Install all of the following before proceeding.

| Tool | Version | Purpose | Install |
|---|---|---|---|
| **Docker Desktop** | 4.x or later | Runs the full Docker Compose stack (Option A) | [docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop/) |
| **.NET SDK** | 10.0 | Builds and runs the backend (Option B) | [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) |
| **Node.js** | 22 LTS | Builds and runs the frontend (Option B) | [nodejs.org](https://nodejs.org/) |
| **Git** | 2.x | Clone the repository | [git-scm.com](https://git-scm.com/) |
| **A terminal** | — | Bash, PowerShell, or Windows Terminal | Pre-installed on all platforms |

**Optional but recommended**:

| Tool | Purpose |
|---|---|
| VS Code | Editor with C# Dev Kit and ESLint extensions |
| JetBrains Rider | Full IDE for .NET; excellent EF Core tooling |
| Docker Desktop Dashboard | GUI for container logs and resource monitoring |

---

## Option A: Docker Compose (Full Stack — Recommended)

This option starts the entire platform — NGINX, frontend, backend, PostgreSQL, and Redis — with a single command. No .NET SDK or Node.js required on the host machine.

### Step 1: Clone and Configure

```bash
git clone <repository-url>
cd tek-hackathon-group8
```

Copy the environment template:

```bash
cp .env.example .env
```

Open `.env` and review the values. The defaults work for local development. The only value you **must** change for security if sharing the environment is `JWT_SECRET`:

```env
POSTGRES_PASSWORD=postgres
JWT_SECRET=dev-secret-change-in-production-minimum-32-chars
AI_PROVIDER=Mock
AZURE_OPENAI_ENDPOINT=
AZURE_OPENAI_API_KEY=
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
ASPNETCORE_ENVIRONMENT=Development
```

### Step 2: Build and Start

```bash
docker compose up --build
```

The first build downloads base images and compiles the application. This takes 3–5 minutes on a typical machine. Subsequent starts without code changes take under 30 seconds.

**What you will see in the logs**:

```
telcopilot.postgres  | database system is ready to accept connections
telcopilot.backend   | Now listening on: http://[::]:8080
telcopilot.backend   | Seeding identity data...
telcopilot.backend   | Seeding network data...
telcopilot.backend   | Seeding alerts data...
telcopilot.frontend  | ▲ Next.js 15.x.x
telcopilot.nginx     | /docker-entrypoint.sh: Configuration complete
```

### Step 3: Open the Application

Once all services are healthy:

| URL | What it is |
|---|---|
| `http://localhost` | TelcoPilot application (login page) |
| `http://localhost/health` | Health check — should return `{"status":"Healthy"}` |
| `http://localhost/swagger` | Swagger UI (Development environment only) |

### Step 4: Log In

Use any of the demo accounts:

| Email | Role | Password |
|---|---|---|
| oluwaseun.a@telco.lag | Engineer | Telco!2025 |
| amaka.o@telco.lag | Manager | Telco!2025 |
| tunde.b@telco.lag | Admin | Telco!2025 |
| kemi.a@telco.lag | Viewer | Telco!2025 |

### Stopping the Stack

```bash
# Stop containers but keep data volumes
docker compose down

# Stop containers AND delete all data (full reset)
docker compose down -v
```

Use `down -v` when you want to reset the database to the seeded state (e.g., before a demo, or to clear acknowledged alerts from a previous session).

---

## Option B: .NET Aspire + npm Dev Server (Hot Reload)

This option is recommended for active development. It provides:
- Hot reload for both the backend (via `dotnet watch`) and frontend (via Next.js Fast Refresh)
- The Aspire Dashboard at `https://localhost:17017` for distributed traces, logs, and metrics
- Automatic service discovery — no manual connection string management

### Prerequisites for Option B

- .NET SDK 10 installed and available on `PATH`
- Node.js 22 installed
- Docker Desktop running (Aspire uses Docker to provision PostgreSQL and Redis)

### Step 1: Set the JWT Secret (first time only)

```bash
cd src/WebApi
dotnet user-secrets set "Jwt__Secret" "dev-secret-at-least-32-characters-long"
cd ../..
```

User-secrets are stored on your local machine, not in the repository. This prevents accidental secret commits.

### Step 2: Start the Aspire AppHost

```bash
dotnet run --project src/AppHost
```

Aspire will:
1. Start a PostgreSQL 17 container (with pgvector)
2. Start a Redis 7 container
3. Start the WebApi backend
4. Open the Aspire Dashboard at `https://localhost:17017`

Watch the terminal output for the backend URL (Aspire assigns a dynamic port, e.g., `https://localhost:7245`).

### Step 3: Start the Frontend Dev Server

In a second terminal:

```bash
cd frontend
npm install    # first time only
npm run dev
```

The frontend dev server starts at `http://localhost:3000`.

**Important**: In Option B, the frontend calls the backend directly (not through NGINX). The `NEXT_PUBLIC_API_URL` environment variable must point to the Aspire-assigned backend URL. For convenience, set it in `frontend/.env.local`:

```env
NEXT_PUBLIC_API_URL=https://localhost:7245/api
```

Replace `7245` with the port Aspire assigned to the WebApi resource.

### Step 4: Open the Application

| URL | What it is |
|---|---|
| `http://localhost:3000` | TelcoPilot frontend (hot reload) |
| `https://localhost:17017` | Aspire Dashboard (traces, logs, metrics) |
| `https://localhost:7245/swagger` | Swagger UI (backend — port may differ) |

---

## Switching to Azure OpenAI

By default, `AI_PROVIDER=Mock` runs the MockCopilotOrchestrator. To use Azure OpenAI:

### Option A (Docker Compose)

Edit `.env`:

```env
AI_PROVIDER=AzureOpenAi
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_OPENAI_API_KEY=your-key-here
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

Then restart:

```bash
docker compose down
docker compose up
```

(No `--build` needed — only config changed.)

### Option B (Aspire)

```bash
cd src/WebApi
dotnet user-secrets set "AI_PROVIDER" "AzureOpenAi"
dotnet user-secrets set "AzureOpenAi__Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAi__ApiKey" "your-key-here"
dotnet user-secrets set "AzureOpenAi__DeploymentName" "gpt-4o-mini"
```

Restart the AppHost.

---

## Troubleshooting

### Docker Desktop is not running

**Symptom**: `docker compose up` fails with "Cannot connect to the Docker daemon."

**Fix**: Launch Docker Desktop and wait for it to fully start (the Docker whale icon in the system tray should be steady, not animated).

---

### Port 80 is already in use

**Symptom**: `docker compose up` fails with "port is already allocated" or "bind: address already in use."

**Fix on Windows**: Check IIS. Open `services.msc` and stop the "World Wide Web Publishing Service" if running. Alternatively, change the NGINX port in `docker-compose.yml`:

```yaml
nginx:
  ports:
    - "8080:80"   # map host 8080 to container 80
```

Then access the application at `http://localhost:8080`.

---

### Backend fails to connect to PostgreSQL

**Symptom**: Backend logs show `Connection refused` or `FATAL: password authentication failed for user "postgres"`.

**Cause A**: PostgreSQL container not yet healthy on first start. The `depends_on: service_healthy` in Docker Compose should handle this, but on very slow machines the backend may start before pg_isready passes.

**Fix A**: Wait 10–15 seconds and restart just the backend:

```bash
docker compose restart backend
```

**Cause B**: `POSTGRES_PASSWORD` in `.env` does not match the existing volume's initialised password.

**Fix B**: Run `docker compose down -v` to destroy the volume, then `docker compose up --build`.

---

### npm install fails with peer dependency errors

**Symptom**: `npm install` in the frontend directory fails.

**Fix**: Use the `--legacy-peer-deps` flag:

```bash
npm install --legacy-peer-deps
```

This is occasionally needed when a transitive dependency has not updated its peer declaration for a new Next.js or React version.

---

### The backend starts but no data appears in the UI

**Symptom**: Login works, but the Dashboard shows zeros and the Alerts page is empty.

**Cause**: The seeders ran on a previous container start and the data already exists, but a `down -v` was not run before `up --build`.

**Fix**: Verify by checking the backend logs for seeder output. If seeders did not run, run:

```bash
docker compose down -v && docker compose up --build
```

---

### Swagger returns 404

**Symptom**: `http://localhost/swagger` returns a 404.

**Cause**: Swagger is only enabled when `ASPNETCORE_ENVIRONMENT=Development`.

**Fix**: Confirm `.env` has `ASPNETCORE_ENVIRONMENT=Development`. Restart the backend.

---

## Cross-References

- Environment variable full reference: [13_Infrastructure_and_Deployment.md](13_Infrastructure_and_Deployment.md)
- Demo walkthrough and scenarios: [17_Project_Demo_Guide.md](17_Project_Demo_Guide.md)
- Demo account role details: [10_User_Roles_and_RBAC.md](10_User_Roles_and_RBAC.md)
