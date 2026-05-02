# TelcoPilot — App Completion Checklist

## Environment Setup
- [ ] Copy `.env.example` → `.env` and fill in `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_KEY`, `JWT_SECRET`
- [ ] Copy `frontend/.env.local.example` → `frontend/.env.local` and set `NEXT_PUBLIC_API_URL`
- [ ] Install: Docker Desktop 4.x, Node.js 22+, .NET 10 SDK, .NET Aspire workload

## Backend
- [ ] Verify all 5 modules compile: Identity, Network, Alerts, Analytics, AI
- [ ] Confirm EF Core migrations run cleanly on startup (`dotnet aspire run`)
- [ ] Confirm seed data loads (default users / roles)
- [ ] Add input validation to AI module endpoints
- [ ] Add structured error logging to AI module
- [ ] Implement Notification Service enhancements
- [ ] Add health-check endpoints for all services
- [ ] Fix caching performance gaps
- [ ] Optimize event processing and DB connection pooling
- [ ] Enforce security headers and RBAC on all protected routes

## Chat & State Persistence
- [ ] Create `Conversations` and `Messages` DB schema + EF migration
- [ ] Implement backend CRUD endpoints for conversations and messages
- [ ] Implement `ChatStore` (MobX) with load/send/persist logic
- [ ] Implement `AuthStore` with token refresh mechanism
- [ ] Implement `UiStore` for sidebar/panel state
- [ ] Wire `RootStore` hydration flow on app load
- [ ] Add optimistic send to Copilot message input
- [ ] Verify cross-tab chat state synchronization

## Frontend
- [ ] Run `npm install` inside `/frontend`
- [ ] Verify all 5 pages render: Dashboard, Alerts, Map, Insights, Copilot
- [ ] Test auth gating — unauthenticated routes redirect to login
- [ ] Confirm `api.ts` sends JWT Bearer token on all requests
- [ ] Test Copilot UI end-to-end (send message → AI response displayed)
- [ ] Verify NetworkMap component loads and renders node data
- [ ] Run `npm run build` with zero errors

## DevOps / Infrastructure
- [ ] Run `docker compose up` — confirm all 5 containers start healthy
- [ ] Confirm NGINX routes `/api/*` → backend and `/` → frontend
- [ ] Confirm Postgres initialises with correct database and user
- [ ] Confirm Redis is reachable from backend
- [ ] Set up structured log output visible in console or aggregator
- [ ] Add Docker health-check probes for all services
- [ ] Confirm `.env` secrets are NOT baked into Docker images

## Testing & Quality
- [ ] Test all 10 API endpoints (Auth, Chat, Metrics, Alerts, Map)
- [ ] Verify JWT flow: register → login → token → use → reject expired
- [ ] Smoke-test full journey: login → dashboard → alerts → map → copilot
- [ ] Confirm BCrypt hashing applied on registration
- [ ] Confirm audit log entries written for key actions
