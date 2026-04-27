# Frontend Onboarding — TelcoPilot UI

> Audience: developers contributing to the [`frontend/`](../../frontend/)
> directory. Read [`README.md`](README.md) first for the cross-cutting
> orientation; this document then walks you exhaustively through the UI layer
> end-to-end.

---

## 1. What you are building

The TelcoPilot frontend is the **operator-facing console** for a Network
Operations Center (NOC). It is a [Next.js 15](https://nextjs.org/docs)
application built with the [App Router](https://nextjs.org/docs/app),
[React 19](https://react.dev/), and TypeScript 5.7. It renders:

| Route                  | What lives there                                                                                              |
|------------------------|---------------------------------------------------------------------------------------------------------------|
| `/login`               | Split-screen sign-in. Pre-populated with the demo user for fast access.                                       |
| `/dashboard`           | **Command Center** — KPI strip + network map preview + an embedded Copilot panel.                             |
| `/copilot`             | Full-screen Copilot chat — the headline AI feature.                                                           |
| `/map`                 | Network Map (towers + region health).                                                                         |
| `/alerts`              | Smart Alerts feed with severity filter and acknowledgement flow.                                              |
| `/insights`            | The wider Operations Dashboard (KPIs, sparklines, region/incident breakdowns).                                |
| `/users`               | Users & RBAC table (manager+).                                                                                |
| `/audit`               | Audit Log (manager+).                                                                                         |

Everything except `/login` is gated by the `(authed)` route group. We use a
custom client-side `useAuth` hook (no NextAuth, no Auth.js) — because the
backend issues plain JWTs that the React client just stores and forwards.

---

## 2. Tech stack — exact versions

| Dependency                                              | Version  | Notes                                                             |
|---------------------------------------------------------|----------|-------------------------------------------------------------------|
| [`next`](https://www.npmjs.com/package/next)            | 15.1.6   | App Router, React Server Components, [`output: 'standalone'`](https://nextjs.org/docs/app/api-reference/next-config-js/output) |
| [`react`](https://www.npmjs.com/package/react)          | 19.0.0   | New hooks (`useActionState`, `use`); we still mostly use classics. |
| [`react-dom`](https://www.npmjs.com/package/react-dom)  | 19.0.0   |                                                                   |
| [`typescript`](https://www.typescriptlang.org/)         | 5.7.3    | strict mode (see [`tsconfig.json`](../../frontend/tsconfig.json)). |
| [`eslint`](https://eslint.org/) + `eslint-config-next`  | 9 / 15.1 | flat-config compatible.                                           |

Everything is in [`frontend/package.json`](../../frontend/package.json). The
dependency surface is intentionally tiny: **no UI kit, no CSS-in-JS framework,
no charting library, no state manager**. All visuals are hand-rolled inline
styles + a small `globals.css`. This is a deliberate choice for the demo —
fewer moving parts, no version churn. Resist the urge to introduce
shadcn/Tailwind/Tanstack until you have explicit team agreement.

---

## 3. Local setup — step by step

### 3.1 Prerequisites

1. [Node.js 22](https://nodejs.org/) (LTS) — same major version as the Docker
   base image (`node:22-alpine`).
2. npm 10+ (ships with Node 22).
3. A running backend. Either:
   - the full stack via `docker compose up` from the repo root, or
   - the backend alone via `dotnet run --project src/AppHost`, or
   - any reachable backend you point [`BACKEND_INTERNAL_URL`](#33-environment-variables) at.

### 3.2 Install dependencies

```bash
cd frontend
npm install --legacy-peer-deps
```

> The `--legacy-peer-deps` flag is required because React 19 + some transitive
> peer ranges still publish ranges that npm 10 considers a conflict. It is the
> same flag the [Dockerfile](../../frontend/Dockerfile) uses on line 7. Do not
> remove it without testing the Docker build too.

### 3.3 Environment variables

The frontend reads exactly **one** environment variable:

| Variable                | Where it is read                                                  | Default                  |
|-------------------------|-------------------------------------------------------------------|--------------------------|
| `BACKEND_INTERNAL_URL`  | [`next.config.mjs`](../../frontend/next.config.mjs) — used as the rewrite target for `/api/:path*` | `http://localhost:5000` |

That is it. No `NEXT_PUBLIC_*` keys, no Auth0 secrets, no API base URL the
browser needs to know about. The browser always hits `/api/...` on its own
origin; whether that hits NGINX → backend (compose) or Next.js dev server →
backend (Aspire) is invisible to the React code.

In Aspire, the AppHost sets `BACKEND_INTERNAL_URL` automatically (see
[`src/AppHost/Program.cs:50`](../../src/AppHost/Program.cs#L50)). In Docker
compose, it is set in [`docker-compose.yml:34`](../../docker-compose.yml#L34).

### 3.4 Run the dev server

```bash
npm run dev
# → http://localhost:3000
```

You should see the login screen. Sign in with the demo credentials from
[`README.md`](README.md). `/api/*` calls in the browser will be proxied to the
backend via the [Next rewrite rule](https://nextjs.org/docs/app/api-reference/next-config-js/rewrites)
in [`next.config.mjs`](../../frontend/next.config.mjs).

### 3.5 Standalone frontend dev (backend in Docker)

```bash
# terminal 1: bring up everything except the frontend
docker compose up postgres redis backend

# terminal 2: run Next.js against the dockerised backend
cd frontend
BACKEND_INTERNAL_URL=http://localhost:8080 npm run dev
```

This is the most ergonomic loop for UI-heavy work — full hot reload while the
expensive backend stays containerised.

### 3.6 Production build (smoke test)

```bash
npm run build         # → .next/standalone bundle
npm run start         # serves the built output on :3000
```

Use this before pushing anything that touches `next.config.mjs`, the
`output: 'standalone'` pipeline, or the Dockerfile — `npm run dev` and the
production build occasionally diverge on edge cases.

---

## 4. Folder layout — explained file by file

```
frontend/
├── Dockerfile                 # multi-stage Node 22 → next standalone (3 stages: deps, build, runner)
├── .dockerignore              # keeps node_modules / .next out of the build context
├── next.config.mjs            # output: 'standalone' + /api/* rewrite
├── next-env.d.ts              # generated by Next — don't edit
├── package.json               # see §2
├── package-lock.json          # commit changes; never edit by hand
├── tsconfig.json              # strict, paths alias `@/*` → `src/*`
└── src/
    ├── app/
    │   ├── globals.css        # CSS variables (theme tokens) + reset
    │   ├── layout.tsx         # root layout — fonts, body class `theme-dark`
    │   ├── page.tsx           # `/` — redirects to /dashboard or /login
    │   ├── login/
    │   │   └── page.tsx       # split-screen login form (uses useAuth().login)
    │   └── (authed)/
    │       ├── layout.tsx     # auth gate + Sidebar + scrollable <main>
    │       ├── dashboard/page.tsx
    │       ├── copilot/page.tsx
    │       ├── map/page.tsx
    │       ├── alerts/page.tsx
    │       ├── insights/page.tsx
    │       ├── users/page.tsx
    │       └── audit/page.tsx
    ├── components/
    │   ├── Sidebar.tsx        # left rail; nav defined in NAV[] const, sectioned by OPS/INSIGHTS/ADMIN
    │   ├── TopBar.tsx         # page-title strip used by some routes
    │   ├── NetworkMap.tsx     # SVG-rendered tower + region heatmap
    │   ├── Copilot.tsx        # chat panel (used embedded in dashboard + standalone in /copilot)
    │   └── UI.tsx             # shared primitives (Card, KpiTile, Pill, Spark, ...)
    └── lib/
        ├── api.ts             # the entire HTTP client surface (one fetch wrapper + ~10 typed methods)
        ├── auth.ts            # token persistence + useAuth() hook
        └── types.ts           # all DTOs that mirror the backend
```

### 4.1 The `(authed)` route group

`(authed)` is a [Next.js route group](https://nextjs.org/docs/app/building-your-application/routing/route-groups)
— the parentheses make it disappear from the URL. Its `layout.tsx`
([file](../../frontend/src/app/(authed)/layout.tsx)) is the **single
client-side auth gate**: if `useAuth()` reports no user once `ready` is true,
it pushes the user to `/login`. This is intentionally simple — there is no
middleware, no edge runtime, no server-side cookie validation. The JWT lives
in `localStorage` and is read on hydration.

That has consequences:

- **There is a brief loading state** ("⌁ initializing session…") on the first
  paint of any authed route. Do not try to render protected content on the
  server — `localStorage` is not available there.
- **A determined user can paste a stale token into devtools** and see the UI
  shell, but every API call will 401. The backend is the source of truth.
- **You can add new authed pages by dropping a file into
  [`(authed)/your-page/page.tsx`](../../frontend/src/app/(authed)/)** — no
  routing config needed.

### 4.2 The `lib/api.ts` HTTP client

[`api.ts`](../../frontend/src/lib/api.ts) is the *only* place in the frontend
that calls `fetch`. All UI code calls `api.<method>()`.

Key contract details:

- `API_BASE = "/api"` — same-origin, always. NGINX or the Next rewrite handles
  the proxy.
- Every request sets `Content-Type: application/json` and `cache: "no-store"`.
- The Bearer token is pulled from `localStorage["tp_access"]` *only on the
  browser* — server components must not call `api.*` directly.
- A non-OK response throws `ApiError(status, detail)`. The `detail` field
  comes from the [ASP.NET ProblemDetails](https://learn.microsoft.com/aspnet/core/web-api/handle-errors#problem-details)
  body produced by the backend's `GlobalExceptionHandler` / `CustomResults`.
- 204 responses (e.g. `ackAlert`) return `undefined`.

When you add a new endpoint:

1. Add a typed DTO to [`lib/types.ts`](../../frontend/src/lib/types.ts) — keep
   the field names byte-for-byte identical to the backend response.
2. Add a one-liner to the `api` object in [`lib/api.ts`](../../frontend/src/lib/api.ts).
3. Use it from a component. Do **not** call `fetch` directly — that bypasses
   the auth header and error normalisation.

### 4.3 The `lib/auth.ts` hook

[`auth.ts`](../../frontend/src/lib/auth.ts) is a tiny wrapper around three
`localStorage` keys (`tp_access`, `tp_refresh`, `tp_user`) plus a non-HttpOnly
cookie (`tp_access`) used by `app/page.tsx` for its server-side redirect
decision.

- `useAuth()` returns `{ user, ready, login, logout }`.
- `persistSession(loginResponse)` writes all three keys + the cookie. Called
  from `useAuth().login` after a successful `POST /api/auth/login`.
- `clearSession()` wipes them. Called from `useAuth().logout`.

> **Demo-grade caveat:** the cookie is intentionally not HttpOnly — that lets
> the server component at [`app/page.tsx`](../../frontend/src/app/page.tsx)
> decide where to redirect without an extra round-trip. In a real production
> deployment, switch to a server-issued HttpOnly + Secure + SameSite=Strict
> cookie and stop persisting the access token in `localStorage`. This is
> called out in the source comment on
> [`auth.ts:18-19`](../../frontend/src/lib/auth.ts#L18-L19).

### 4.4 Components

There are exactly **five** components today. None are general-purpose
"library" components — each maps to a specific UI concern. Resist the
temptation to create a `components/ui/` mega-folder; if a primitive fits in
[`UI.tsx`](../../frontend/src/components/UI.tsx) (Card, Pill, KpiTile, etc.),
add it there.

| Component                                                            | Used by                            | What to know                                                       |
|----------------------------------------------------------------------|------------------------------------|--------------------------------------------------------------------|
| [`Sidebar.tsx`](../../frontend/src/components/Sidebar.tsx)           | `(authed)/layout.tsx`              | Nav array (`NAV`) is the source of truth. Add a route → add a row. |
| [`TopBar.tsx`](../../frontend/src/components/TopBar.tsx)             | several pages                      | Title strip; takes a `title` and optional `subtitle`.              |
| [`NetworkMap.tsx`](../../frontend/src/components/NetworkMap.tsx)     | `/dashboard`, `/map`               | Pure SVG rendering — no map library. Coordinates come from API.    |
| [`Copilot.tsx`](../../frontend/src/components/Copilot.tsx)           | `/dashboard` (embedded), `/copilot`| Streams nothing today — single POST → CopilotAnswer DTO.           |
| [`UI.tsx`](../../frontend/src/components/UI.tsx)                     | everywhere                         | Card, KpiTile, Pill, Spark, etc. The "design system".              |

### 4.5 Styling

There is no Tailwind, no CSS-in-JS framework, no MUI. Two mechanisms only:

1. **Inline styles** for component-local visuals (the vast majority of the
   code). Pattern: `style={{ … }}` directly on JSX, occasionally hoisted to a
   module-scoped `const` for reuse.
2. **CSS custom properties** (the design tokens) defined in
   [`app/globals.css`](../../frontend/src/app/globals.css). Look there for
   `--bg`, `--ink`, `--accent`, `--mono`, etc. Use these instead of hard-coded
   colors so the dark theme stays coherent.

Fonts are loaded via the Google Fonts `<link>` in
[`app/layout.tsx`](../../frontend/src/app/layout.tsx) — Geist (sans) and
JetBrains Mono. The `theme-dark` class on `<body>` is currently the only
theme; light mode does not exist yet.

---

## 5. The Copilot UI — a worked example

The Copilot is the headline feature. End-to-end:

1. User types "why is Lekki degraded?" into the textarea inside
   [`Copilot.tsx`](../../frontend/src/components/Copilot.tsx).
2. The component calls `api.chat(query)` → [`lib/api.ts`](../../frontend/src/lib/api.ts#L46).
3. That fires `POST /api/chat` with the bearer token.
4. NGINX (or the Next rewrite) forwards to `Web.Api`, which routes to
   `Modules.Ai.Application.Copilot.AskCopilot.AskCopilotCommandHandler`.
5. The handler delegates to `ICopilotOrchestrator` — either
   `SemanticKernelOrchestrator` (Azure OpenAI) or `MockCopilotOrchestrator`
   (deterministic, still calls the real Network/Alerts modules).
6. The response (`CopilotAnswer`) comes back — `answer`, `confidence`,
   `skillTrace[]`, `attachments[]`, `provider`.
7. The component renders the answer plus the *skill trace* — the list of
   skill+function calls Semantic Kernel made under the hood, with durations.
   The skill trace is what makes the AI step "auditable" rather than opaque.

If you ever modify the chat UI, the contract you must preserve is:

- Always render the `provider` badge (Mock vs AzureOpenAi). Operators
  legitimately want to know whether an answer came from the real model.
- Always render the skill trace, even if empty. Hiding it erodes trust.
- Always render the `confidence` value. It is part of the product story.

---

## 6. Common workflows

### 6.1 Adding a new page

1. Create `src/app/(authed)/<slug>/page.tsx`. Export `default function Page()`.
2. Add a `NAV` entry in
   [`Sidebar.tsx`](../../frontend/src/components/Sidebar.tsx).
3. If the page needs RBAC gating *in the UI* (the backend already gates the
   data), check `useAuth().user.role` and render a friendly empty state for
   unauthorised roles. Do **not** rely on UI gating for security — it is
   purely cosmetic.

### 6.2 Calling a new backend endpoint

1. Confirm the backend endpoint shape via Swagger (<http://localhost/swagger>).
2. Add the response type to [`lib/types.ts`](../../frontend/src/lib/types.ts).
3. Add a method to the `api` object in
   [`lib/api.ts`](../../frontend/src/lib/api.ts).
4. Call it from your component. Wrap in `try { await api.x() } catch (e) { … }`
   and surface `e instanceof ApiError ? e.message : "…"` to the user.

### 6.3 Updating a DTO when the backend changes

DTO drift is the single most common source of UI bugs in this repo. The types
in [`lib/types.ts`](../../frontend/src/lib/types.ts) are **hand-written** —
there is no codegen. When the backend changes a field name or type:

1. Update the matching type in `types.ts`.
2. Run `npm run build` — TypeScript will tell you everywhere it is consumed.
3. Fix the call sites.
4. Cross-check by hitting the live endpoint in DevTools' Network tab.

### 6.4 Debugging a 401 / 403

- 401 → token missing or expired. Check `localStorage["tp_access"]` exists.
  Try `useAuth().logout()` then log in again to refresh.
- 403 → token is valid but role is insufficient. Check the user's `role` in
  the JWT (decode it at [jwt.io](https://jwt.io/)) against the policy on the
  endpoint. The role-to-policy mapping is in
  [`src/Web.Api/Program.cs:75-80`](../../src/Web.Api/Program.cs#L75-L80).

### 6.5 Debugging a CORS error in dev

Almost always means you bypassed the `/api` rewrite and called the backend
directly. The backend's CORS allow-list is exactly:

```
http://localhost:3000
http://localhost
http://127.0.0.1:3000
```

(see [`Program.cs:35-39`](../../src/Web.Api/Program.cs#L35-L39)). If you must
add an origin, add it there — but the right fix is almost always to call
`/api/*` on the same origin.

---

## 7. Testing

There are **no automated frontend tests** in the repo today. This is a
deliberate scope choice for the demo — the backend is comprehensively tested,
the frontend is verified by hand. If you add tests, the recommended stack is:

- [Vitest](https://vitest.dev/) for unit tests (faster ESM story than Jest).
- [Playwright](https://playwright.dev/) for end-to-end. The compose stack is
  ready-made for headless browser tests against `http://localhost`.

When you do hand-test, run through the **golden path** plus at least one edge
case per route:

| Page         | Golden path                                                   | Edge cases to hit                                               |
|--------------|---------------------------------------------------------------|-----------------------------------------------------------------|
| `/login`     | demo creds → `/dashboard`                                     | wrong password (red error), empty fields, slow network.         |
| `/dashboard` | KPIs render, map renders, Copilot answers a question.         | backend down (cards show error states).                         |
| `/copilot`   | Multi-turn chat works, skill trace renders.                   | very long answer, error response.                               |
| `/alerts`    | Severity filter works, ack flips state, list re-renders.      | ack as `viewer` should 403; list with zero alerts shows empty.  |
| `/users`     | Loads as manager+, 403 as engineer/viewer.                    | …                                                               |
| `/audit`     | Loads as manager+, paginates with `take` query.               | …                                                               |

---

## 8. Production build pipeline (at a glance)

The Dockerfile ([`frontend/Dockerfile`](../../frontend/Dockerfile)) is three
stages:

1. **`deps`** — `npm install --legacy-peer-deps` against `package-lock.json`.
2. **`build`** — `npm run build` → produces `.next/standalone` (a
   self-contained Node server) and `.next/static` (the static assets).
3. **`runner`** — copies `public/`, `.next/standalone`, `.next/static` into a
   fresh Node 22 image and runs `node server.js`.

The output is intentionally **not** the default Next runtime — it is the
[`output: 'standalone'`](https://nextjs.org/docs/app/api-reference/next-config-js/output)
mode, which strips out everything not needed at runtime. That keeps the
production image around ~150 MB.

Telemetry is disabled at build time (`NEXT_TELEMETRY_DISABLED=1`). Do not
re-enable it without team agreement.

---

## 9. Frequently asked questions

**Q: Why no UI library / Tailwind / state manager?**
A: Demo scope. The whole frontend is ~30 files; the friction of a design
system or state library outweighs the benefit at this size. If the surface
grows, a Tailwind + shadcn migration is the natural next step — but it should
be a separate, deliberate PR.

**Q: Why is the JWT in `localStorage` rather than an HttpOnly cookie?**
A: Demo scope, again — it makes the auth flow inspectable in DevTools. The
real production answer is HttpOnly + Secure + SameSite. Don't ship this
verbatim.

**Q: Why doesn't `/copilot` stream tokens?**
A: The backend currently returns the full `CopilotAnswer` object in one shot.
Streaming would require server-sent events through NGINX (already configured
for upgrade — see [`nginx.conf:64-66`](../../deploy/nginx/nginx.conf#L64-L66))
and a Semantic Kernel streaming completion. It's a known follow-up; not
demo-blocking.

**Q: Where do I put a new tower icon / svg?**
A: Inline SVG in the relevant component. We avoid `next/image` for icons
because they are tiny and inline SVGs theme-respond via `currentColor`.

**Q: I added a dependency and the Docker build fails.**
A: 95% of the time you forgot `--legacy-peer-deps`. The Dockerfile uses it on
line 7, but if your dep needs an even older peer range you may need to
either bump the dep or pin a transitive. Check `npm install --legacy-peer-deps`
locally first before suspecting Docker.

---

## 10. Quick reference

```bash
# install
cd frontend && npm install --legacy-peer-deps

# develop (against full docker stack)
docker compose up -d postgres redis backend
BACKEND_INTERNAL_URL=http://localhost:8080 npm run dev

# develop (Aspire — boots backend + Next together)
dotnet run --project src/AppHost

# production smoke test
npm run build && npm run start

# lint
npm run lint
```

That is everything you need to be productive in `frontend/`. The next stop is
either [`backend.md`](backend.md) (if your work touches API contracts) or
[`devops.md`](devops.md) (if your work touches Docker / NGINX / Aspire).
