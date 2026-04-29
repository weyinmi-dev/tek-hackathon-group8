# Frontend Architecture

This document describes TelcoPilot's Next.js 15 frontend: the App Router structure, authentication gate, API client design, token management, theme system, component breakdown, state management, and responsive design approach.

---

## Next.js App Router Structure

TelcoPilot uses Next.js 15's App Router with a route-group structure that separates authenticated pages from the public login page.

```
frontend/src/
├── app/
│   ├── layout.tsx                   ← Root layout: <html>, theme class, CSS variables
│   ├── globals.css                  ← CSS variable definitions, animations, utility classes
│   ├── page.tsx                     ← Root redirect: / → /dashboard (if authed) or /login
│   ├── login/
│   │   └── page.tsx                 ← Split-screen login form (public, no layout wrapper)
│   └── (authed)/                    ← Route group: all pages requiring authentication
│       ├── layout.tsx               ← Authenticated layout: Sidebar + TopBar shell
│       ├── dashboard/page.tsx       ← Command Center: KPI strip + map + ticker + copilot
│       ├── copilot/page.tsx         ← Full-screen Copilot chat
│       ├── alerts/page.tsx          ← Severity-filtered alert feed
│       ├── map/page.tsx             ← Full-screen network map
│       ├── insights/page.tsx        ← Full metrics dashboard
│       ├── audit/page.tsx           ← Audit trail feed
│       ├── users/page.tsx           ← User management (manager+ only)
│       ├── documents/page.tsx       ← RAG document management
│       └── mcp/page.tsx             ← MCP plugin explorer
├── components/
│   ├── Copilot.tsx                  ← Chat UI, SkillTrace animation, FormattedAnswer
│   ├── NetworkMap.tsx               ← Canvas tower visualization
│   ├── RoleGate.tsx                 ← Conditional rendering based on role rank
│   ├── Sidebar.tsx                  ← Navigation sidebar with role-aware links
│   ├── ThemeToggle.tsx              ← Dark/light toggle via data-theme attribute
│   ├── TopBar.tsx                   ← Page title, subtitle, right-side slot
│   └── UI.tsx                       ← Design system primitives: Card, KPI, Btn, Pill, etc.
└── lib/
    ├── api.ts                       ← Unified typed API client
    ├── auth.ts                      ← Session persistence, useAuth hook
    ├── rbac.ts                      ← Role rank helpers, canManageTarget
    └── types.ts                     ← TypeScript mirrors of backend DTOs
```

### Route Group: (authed)

The `(authed)` route group applies the authenticated layout to all pages within it without affecting the URL structure. `/dashboard` resolves to `(authed)/dashboard/page.tsx` — the `(authed)` segment is invisible in the URL. This pattern means:
- The sidebar and top bar are rendered once in `(authed)/layout.tsx`, not in every page
- The root `page.tsx` redirects to `/dashboard` if a session cookie is present
- The login page is outside the group and renders with no shell

---

## Authentication Gate

TelcoPilot's auth gate is implemented client-side in `(authed)/layout.tsx` using the `useAuth()` hook from `lib/auth.ts`. On mount, it reads the session from `localStorage` and redirects to `/login` if no valid session is found.

```typescript
// (authed)/layout.tsx pattern
export default function AuthedLayout({ children }) {
  const { user, ready } = useAuth();

  useEffect(() => {
    if (ready && !user) {
      window.location.href = "/login";
    }
  }, [user, ready]);

  if (!ready || !user) return <LoadingSpinner />;
  return <SidebarShell>{children}</SidebarShell>;
}
```

**Why client-side auth?** In a Docker Compose deployment behind NGINX, all traffic goes to a single origin. The access token is stored in `localStorage` and injected by the `api.ts` `request()` function. A server-side redirect would require the token to be in a cookie readable by the Next.js server (the `tp_access` cookie is set on login for this purpose). For the demo environment, client-side auth is sufficient and simpler; production hardening would use `HttpOnly; Secure` cookies and middleware-based redirect.

---

## API Client Pattern (lib/api.ts)

The `api` object in `lib/api.ts` is TelcoPilot's single interface to the backend. All HTTP communication goes through the `request<T>()` function.

### Core Request Function

```typescript
async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  
  // Auto-set Content-Type unless body is FormData (multipart boundary)
  if (!(init.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  // Client-side: inject Bearer token from localStorage
  if (typeof window !== "undefined") {
    const token = window.localStorage.getItem("tp_access");
    if (token) headers.set("Authorization", `Bearer ${token}`);
  }

  const res = await fetch(`/api${path}`, { ...init, headers, cache: "no-store" });
  
  if (res.status === 204) return undefined as T;
  if (!res.ok) {
    const detail = (await res.json()).detail || res.statusText;
    throw new ApiError(res.status, detail);
  }
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
```

**Design decisions:**
- `cache: "no-store"` — NOC data is real-time; stale Next.js fetch cache is inappropriate
- `FormData` detection — document upload endpoint (`/api/documents/upload`) sends multipart; letting the browser set the content-type boundary automatically
- `ApiError` class — preserves the HTTP status code for conditional error handling (e.g., 401 triggers re-login, 403 shows permission error)
- All API paths are relative (`/api/...`) — NGINX routes these to the backend in compose; `next.config.mjs` rewrites handle the same in Aspire dev mode

### Typed API Surface

Every endpoint has a corresponding typed method:

```typescript
export const api = {
  login: (email, password) => request<LoginResponse>("/auth/login", { method: "POST", ... }),
  chat: (query) => request<CopilotAnswer>("/chat", { method: "POST", ... }),
  map: () => request<MapResponse>("/map"),
  alerts: (opts) => request<Alert[]>(`/alerts${qs}`),
  metrics: () => request<MetricsResponse>("/metrics"),
  users: () => request<UserListItem[]>("/auth/users"),
  // ... 20+ methods total
};
```

---

## Token Management (localStorage + Refresh Flow)

Session state is managed in `lib/auth.ts` with three `localStorage` keys and one cookie:

| Key | Type | Contents |
|---|---|---|
| `tp_access` | localStorage + cookie | JWT access token |
| `tp_refresh` | localStorage | Opaque refresh token |
| `tp_user` | localStorage | Serialised `AuthUser` object |

### Login Flow

```typescript
export function persistSession(login: LoginResponse) {
  localStorage.setItem("tp_access",  login.accessToken);
  localStorage.setItem("tp_refresh", login.refreshToken);
  localStorage.setItem("tp_user",    JSON.stringify(login.user));
  // Cookie for server-side redirect detection (non-HttpOnly for demo)
  document.cookie = `tp_access=${login.accessToken}; Path=/; SameSite=Lax`;
}
```

### useAuth Hook

The `useAuth()` hook reads the stored user on mount (`useEffect`) and exposes `login()`, `logout()`, and `user` state. It deliberately uses `useState` + `useEffect` rather than `useSyncExternalStore` to avoid hydration mismatches between the server render (no localStorage) and the client render.

```typescript
export function useAuth() {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    setUser(readUser());   // reads from localStorage
    setReady(true);
  }, []);

  return { user, ready, login, logout };
}
```

The `ready` flag prevents a flash where the layout thinks no user is logged in during the initial render before `useEffect` fires.

---

## Theme System

TelcoPilot uses a CSS custom property (variable) theme system with dark mode as the default. Theme switching is implemented by toggling a `data-theme` attribute on the `<html>` element.

### CSS Variable Architecture

```css
/* globals.css — default (dark) theme */
:root {
  --bg:        #0d0f13;   /* page background */
  --bg-1:      #12151b;   /* card background */
  --bg-2:      #161922;   /* elevated card */
  --bg-3:      #1c2030;   /* input / hover */
  --ink:       #e8eaf0;   /* primary text */
  --ink-2:     #9aa0b4;   /* secondary text */
  --ink-3:     #5a6282;   /* muted text */
  --line:      #1e2235;   /* dividers */
  --line-2:    #252a3a;   /* elevated dividers */
  --accent:    #5b8cff;   /* primary blue */
  --accent-dim:#1a2540;   /* accent background tint */
  --accent-line:#2a3d6a;  /* accent border */
  --ok:        #3dd68c;   /* success / ok status */
  --warn:      #f5a623;   /* warning status */
  --crit:      #ff5470;   /* critical status */
  --info:      #5b8cff;   /* info status */
  --mono:      'JetBrains Mono', monospace;
  --sans:      system-ui, sans-serif;
}

/* Light theme override */
[data-theme="light"] {
  --bg:        #f0f2f8;
  --bg-1:      #ffffff;
  /* ... all variables overridden */
}
```

Every component references CSS variables exclusively — no hardcoded colours. This means dark/light switching is a single attribute change on `<html>` that cascades through the entire UI.

### ThemeToggle Component

```typescript
export function ThemeToggle() {
  const [dark, setDark] = useState(true);
  
  function toggle() {
    const next = !dark;
    setDark(next);
    document.documentElement.setAttribute("data-theme", next ? "dark" : "light");
    localStorage.setItem("tp_theme", next ? "dark" : "light");
  }
  
  return <button onClick={toggle}>{dark ? "☀" : "◑"}</button>;
}
```

---

## Component Breakdown per Page

### Dashboard (Command Center) — `dashboard/page.tsx`

A 2×2 CSS Grid layout:
- **Row 1 (full width)**: 6 `KPI` cards with inline sparkline SVGs
- **Row 2, Col 1**: `NetworkMap` canvas + live alert ticker (CSS marquee animation)
- **Row 2, Col 2**: Embedded `Copilot` (same component as the full-page version, `embedded` prop reduces padding)

Data loading: `Promise.all([api.metrics(), api.map(), api.alerts()])` on mount, then `setInterval(..., 30_000)` for polling refresh.

### Copilot — `copilot/page.tsx`

Full-screen `Copilot` component. Accepts `?q=` URL parameter for deep-linking (e.g., clicking a tower on the map opens Copilot with a pre-populated query).

### Alerts — `alerts/page.tsx`

Severity filter tabs (All / Critical / Warn / Info) implemented as controlled state. Alert cards show severity pill, incident code, tower code, AI-attributed cause, subscriber count, confidence badge, and an "Acknowledge" button (visible to Engineer+).

### Map — `map/page.tsx`

Full-screen `NetworkMap` canvas with a right-side panel showing:
- Selected tower detail (signal, load, status, issue)
- Region health breakdown table
- Best Signal Zones panel (top 3 towers by signal percentage)
- Diagnose / Dispatch / History action buttons (wired to Copilot and alert navigation)

### Insights — `insights/page.tsx`

Full `MetricsResponse` rendering:
- KPI cards with sparklines
- Latency chart (simulated p95 / 24h per region)
- Regional health signal bars
- SLA compliance donut
- Incident type distribution bar chart
- Copilot top queries panel

### Audit — `audit/page.tsx`

Paginated audit entry table from `api.audit(100)`. Columns: timestamp, actor, role, action, target, IP. Color-coded by role (engineer/manager/admin/system).

### Users — `users/page.tsx`

User table + RBAC matrix. Gated behind `RoleGate` requiring Manager rank. Shows all users with actions (change role, activate/deactivate, delete) conditioned on `canManageTarget()` from `lib/rbac.ts`.

---

## State Management Approach

TelcoPilot uses React hooks exclusively for state management. There is no Redux, Zustand, or Context API usage for application state. The decision is based on the following reasoning:

- **Page-local state**: each page component owns its own data state (`useState` + `useEffect` fetch). Pages are independent and do not share state.
- **Auth state**: the `useAuth()` hook encapsulates all authentication state. Components that need the current user call `useAuth()` directly.
- **No global store needed**: the application's data dependencies are all fetched fresh per page load. There is no complex cross-page state synchronisation requirement.
- **30-second polling**: the dashboard uses `setInterval` to refresh all data every 30 seconds. This is simple, reliable, and sufficient for NOC operations where sub-second real-time is not required for the dashboard (the Copilot provides on-demand freshness).

---

## Responsive Design Approach

TelcoPilot is designed primarily for desktop NOC workstations (1920×1080 or wider). The layout uses CSS Grid and Flexbox throughout, with pixel values calibrated for the NOC context:

- Sidebar is fixed-width (220px), not collapsible — NOC environments do not require mobile-first navigation
- The dashboard grid (`1.4fr 1fr`) fills the viewport at any desktop resolution
- Card components use `min-height: 0` with `flex: 1` to fill available space responsively
- Font sizes are slightly smaller than consumer UI conventions (9.5–13.5px for compact data density) — intentional for information-dense NOC displays
- The `calc(100vh - 67px)` dashboard grid height fills exactly the viewport below the TopBar

For mobile/tablet contexts, the design degrades gracefully — content scrolls vertically and the sidebar collapses. However, the primary target is the 1080p+ NOC workstation and the hackathon judging environment.
