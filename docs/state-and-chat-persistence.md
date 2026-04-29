# Frontend State Management + Persistent Chat History

> Implementation notes for the changes that eliminated frontend statelessness
> and introduced durable, multi-session chat history. Read alongside
> [`backend.md`](onboarding/backend.md) for module conventions.

---

## 1. Why this exists

Before this change:

- Every page did `useEffect → setState` on mount → refresh = full re-fetch + flash of empty UI.
- Auth tokens lived in `localStorage` but `useAuth` re-read `localStorage` per component → no reactivity, no cross-tab sync, no proactive refresh.
- `Copilot.tsx` held messages in component-local `useState` → refresh wiped the entire conversation.
- `ChatLog` existed as a flat audit row per Q+A — useful for the audit page, useless for resuming a session.

After this change:

- Single MobX-backed root store owns all client state worth surviving a refresh.
- Every conversation + message is persisted in Postgres (`ai.conversations`, `ai.messages`), keyed off the authenticated user.
- The Copilot UI hydrates from `localStorage` (active conversation id) on boot, then re-fetches that conversation's messages from the server.
- Token refresh runs proactively (60s before expiry) and reactively (single shared in-flight refresh on a 401).

---

## 2. Architecture

### 2.1 Backend — Conversations + Messages

```
ai.conversations
  ├── id (uuid, pk)
  ├── user_id (uuid, indexed with updated_at_utc)
  ├── actor_handle (varchar 64) -- cached from JWT for listings
  ├── title (varchar 120) -- auto-derived from first user message
  ├── created_at_utc, updated_at_utc, last_message_at_utc
  └── message_count (int)

ai.messages
  ├── id (uuid, pk)
  ├── conversation_id (uuid, fk → conversations, cascade)
  ├── role (int) -- 0 system | 1 user | 2 assistant | 3 tool
  ├── content (text)
  ├── metadata (text, JSON) -- assistant turns: provider, confidence, skill trace, attachments
  ├── prompt_tokens, completion_tokens (int, nullable)
  └── created_at_utc (indexed with conversation_id)
```

Schema is created idempotently by `Web.Api/Extensions/MigrationExtensions.cs` (the
existing per-statement DDL replay), so deploying this just means `docker compose
up -d --build` — no manual migration step. The named Docker volume from the
persistence work means the data survives container recreation.

#### Aggregate boundaries

- `Conversation` is the aggregate root. Owns its `Message` collection (cascade delete).
- `AppendMessage()` is the only mutation path — guarantees `MessageCount`, `LastMessageAtUtc`, and `UpdatedAtUtc` move together.
- The first user message auto-derives the title (truncated to 80 chars) so the sidebar is useful immediately.

#### Endpoints

| Method | Path                                  | Auth        | Purpose                                          |
|-------:|---------------------------------------|-------------|--------------------------------------------------|
| POST   | `/api/chat`                           | Bearer      | Ask the Copilot. Body: `{ query, conversationId? }`. Returns `CopilotAnswer` enriched with `conversationId`, `userMessageId`, `assistantMessageId`. |
| GET    | `/api/chat/conversations?take=50`     | Bearer      | Sidebar listing for the signed-in user.          |
| GET    | `/api/chat/conversations/{id}`        | Bearer      | Full message replay for session restore.         |
| PATCH  | `/api/chat/conversations/{id}`        | Bearer      | Rename. Body: `{ title }`.                       |
| DELETE | `/api/chat/conversations/{id}`        | Bearer      | Hard delete (cascades messages).                 |

Every conversation endpoint enforces ownership (`UserId` from `sub`/`NameIdentifier`
JWT claim) — a foreign id returns 404 (not 403) to avoid leaking existence.

#### `AskCopilot` handler flow

1. Resolve or create the `Conversation` for `(UserId, ConversationId?)`. Foreign / stale id silently starts a new conversation.
2. Append the user's message + `SaveChanges` **before** calling the orchestrator. If the AI call fails or times out, the question is already in history; the user can retry without retyping.
3. Run the orchestrator (Mock or Semantic Kernel — unchanged).
4. Append the assistant message with metadata JSON (`{ Provider, Confidence, SkillTrace, Attachments }`) so the UI can rehydrate the same answer card on session restore.
5. Continue writing the existing `ChatLog` audit row + analytics audit entry — backward compatible.
6. Return `CopilotAnswer with { ConversationId, UserMessageId, AssistantMessageId }` so the frontend can pin the active session.

### 2.2 Frontend — MobX root store

```
RootStore
  ├── AuthStore   (user, JWT pair, hydration, proactive refresh, cross-tab sync)
  ├── ChatStore   (conversations[], activeConversationId, messages[], optimistic send)
  └── UiStore     (sidebar collapsed, future UI prefs)
```

Single instance per page session, constructed inside `<StoreProvider>` (a Client
Component) via `useState`'s initializer so HMR / StrictMode double-invokes don't
multiply hydration effects. Stores reach React via `mobx-react-lite`'s
`observer()` HOC.

#### Why MobX?

- Lower-friction than Redux Toolkit; no action types or reducers for what is
  largely "set this value, derive that one".
- More structured than ad-hoc Context + persistence — `makeAutoObservable` +
  `autorun` give us reactive derivations for free.
- Plays well with the App Router: stores are entirely client-side, no SSR
  hydration mismatches because we gate every dependent UI on a `hasHydrated`
  flag.

#### Persistence strategy

Hand-rolled `lib/stores/persistence.ts` — three primitives (`persist`,
`hydrate`, `onCrossTabChange`) — instead of `mobx-persist-store` so we can be
explicit about what each store persists and avoid the extra dependency.

| Store     | Persisted in localStorage                            | NOT persisted (always re-fetched) |
|-----------|------------------------------------------------------|------------------------------------|
| AuthStore | `user`, `accessToken`, `refreshToken`, both expiries | n/a                                |
| ChatStore | `activeConversationId`, top-25 conversation summaries | message bodies (re-fetched from server on hydration) |
| UiStore   | sidebar collapsed flags                              | n/a                                |

We deliberately do **not** persist message bodies. Reasons:
- They can be large and arbitrary user content — localStorage has a ~5 MB cap.
- The server is the source of truth; persisting both invites divergence.
- Hydration latency for one conversation is ~50ms — well below the
  perception threshold.

Cross-tab sync uses the `storage` event: logging out in one tab triggers a
storage write, the other tab's `AuthStore` mirrors it, the `AuthedLayout`
observer fires and bounces to `/login`.

#### Auth refresh

The `AuthStore` schedules a `setTimeout` to call `/auth/refresh` 60 seconds
before the access token expires. Refresh is also reactive: the API client's
fetch wrapper intercepts a 401, calls `auth.refresh()` once (subsequent
concurrent 401s wait on the same in-flight promise), and replays the original
request once on success. If the refresh itself returns 401, the user is logged
out cleanly.

#### Hydration flow on page load

```
window load
  → StoreProvider mounts (client only)
  → RootStore() constructor runs:
      AuthStore: hydrate from tp_auth_v1 → hasHydrated=true → schedule refresh
      ChatStore: hydrate activeConversationId + recent list → hasHydrated=true
                 autorun: when auth.isAuthenticated, load conversations from server
                 autorun: when activeConversationId set, load that conversation's messages
  → AuthedLayout observer renders Sidebar + page
  → Copilot observer renders messages from chat.messages (system banner + restored)
```

The whole sequence completes in <300ms on a warm cache; the user sees their
last conversation already on screen by the time the page finishes painting.

#### Optimistic send

`ChatStore.ask(query)`:
1. Append a user message with `pending: true` and a temp UUID.
2. Set `sending = true`, populate `pendingTrace` so the trace panel renders immediately.
3. POST `/api/chat`.
4. On success: replace temp id with the server's `userMessageId`, append the assistant message, bump or insert the conversation in the sidebar list.
5. On failure: mark the optimistic message with `error`, append a synthetic assistant card with the error.

Sidebar bumps happen client-side (no extra round-trip) and converge with the
server-truth on the next list load.

### 2.3 Affected files

**Backend**
- `src/Modules/Ai/Modules.Ai.Domain/Conversations/`
  - `Conversation.cs`, `Message.cs`, `MessageRole.cs`, `IConversationRepository.cs` (new)
  - `ChatLog.cs` (unchanged — kept for audit backward compat)
- `src/Modules/Ai/Modules.Ai.Infrastructure/`
  - `Database/AiDbContext.cs` — `Conversations`/`Messages` DbSets
  - `Database/Configurations/ConversationConfiguration.cs`, `MessageConfiguration.cs` (new)
  - `Repositories/ConversationRepository.cs` (new)
  - `DependencyInjection.cs` — register `IConversationRepository`
- `src/Modules/Ai/Modules.Ai.Application/Copilot/`
  - `AskCopilot/AskCopilotCommand.cs` — added `UserId`, `ConversationId`; `CopilotAnswer` now carries the three persistence ids
  - `AskCopilot/AskCopilotCommandHandler.cs` — full rewrite, persists conversation + messages
  - `Conversations/` — list/get/delete/rename queries and handlers (new)
- `src/Web.Api/Endpoints/Chat/`
  - `Ask.cs` — extracts `UserId` from JWT, accepts `conversationId` in body
  - `Conversations.cs` (new) — list / get / patch / delete endpoints

**Frontend**
- `frontend/package.json` — added `mobx`, `mobx-react-lite`
- `frontend/src/lib/stores/` (new)
  - `persistence.ts` — `persist`/`hydrate`/`onCrossTabChange`
  - `AuthStore.ts`, `ChatStore.ts`, `UiStore.ts`, `RootStore.ts`, `StoreProvider.tsx`
- `frontend/src/lib/api.ts` — `configureApi` for token + refresh injection; 401 auto-refresh; conversation endpoints
- `frontend/src/lib/auth.ts` — compatibility shim now delegating to the store
- `frontend/src/lib/types.ts` — `ConversationSummary`, `ConversationDetail`, `ConversationMessage`, etc.
- `frontend/src/app/layout.tsx` — wraps tree in `<StoreProvider>`
- `frontend/src/app/(authed)/layout.tsx` — observer over `AuthStore`
- `frontend/src/components/Sidebar.tsx` — observer
- `frontend/src/components/Copilot.tsx` — full rewrite, store-driven
- `frontend/src/components/ConversationsSidebar.tsx` (new)
- `frontend/src/app/(authed)/copilot/page.tsx` — embeds the sessions sidebar

---

## 3. Database migration details

There is no manual migration step. Schema creation is idempotent per the
project's existing `EnsureCreatedAsync` + per-statement DDL replay path
(`Web.Api/Extensions/MigrationExtensions.cs`). On first boot after this change:

1. Postgres connects, AiDbContext runs `CreateTablesAsync()`.
2. The first attempt may fail with `duplicate_table` for the existing
   `chat_logs` / `knowledge_*` / `managed_documents` tables — handled.
3. The retry loop runs each `CREATE TABLE` statement individually, swallowing
   duplicate-object errors per statement.
4. The two new tables (`conversations`, `messages`) get created; everything
   else is left alone.

If you want a clean slate (development only):

```bash
docker compose down -v   # wipes the postgres volume — see persistence docs
docker compose up -d --build
```

When you eventually move to real EF migrations:

```bash
dotnet ef migrations add AddConversationsAndMessages \
  --project src/Modules/Ai/Modules.Ai.Infrastructure \
  --startup-project src/Web.Api \
  -- --context AiDbContext
```

---

## 4. Aspire considerations

Everything routes through the existing Aspire AppHost wiring:

- `WithReference(db)` injects the `telcopilot` Postgres connection string into
  Web.Api unchanged.
- `ContainerLifetime.Persistent` + `WithDataVolume("telcopilot-pg-data-aspire")`
  means conversations survive AppHost restarts.
- No new Aspire resources required.
- Health checks (Npgsql + Redis) already cover the Postgres dependency the
  conversations table relies on.

If you ever switch between docker-compose and Aspire on the same machine, note
that they use distinct named volumes (see `docs/instructions-2.md` § Persistence).
Conversations persist independently in each.

---

## 5. Future scalability hooks

- **Streaming responses**: the assistant message is currently appended in one
  shot. To stream, change `AskCopilotCommandHandler` to write the user message
  immediately, return the assistant `Message.Id` as a placeholder, then push
  tokens to the client over SSE (`/api/chat/stream/{messageId}`) and PATCH the
  message body as tokens arrive. The `ChatStore.ask()` optimistic flow already
  reconciles by id, so the UI side is a small refactor.
- **Multi-agent**: add a new store slice (e.g. `AgentsStore`) to `RootStore`,
  alongside `chat`. The shared `configureApi` hook and the `observer()` pattern
  scale uniformly.
- **MCP plugin chat**: a future per-plugin chat just becomes a new conversation
  category (e.g. an additional column on `conversations`), no schema redesign.
- **Pagination/virtualization**: conversation listing already supports `?take=`;
  the messages query loads everything at once today (sufficient for the demo).
  When a conversation crosses ~200 messages, switch the message query to a
  paged version returning the most recent N + a cursor.
- **Token usage**: `Message` already carries `prompt_tokens`/`completion_tokens`
  columns. Wire the orchestrator to populate them when the SK call returns
  usage info.

---

## 6. Things deliberately NOT done

- **No IndexedDB.** `localStorage` is enough for tokens + active id + small
  recent list. Switching to IndexedDB would only make sense if we started
  caching message bodies offline.
- **No Service Worker / offline mode.** Out of scope for a NOC tool that
  requires live data.
- **No optimistic conversation rename.** We round-trip the rename so the title
  in the sidebar matches what every other tab will see on next load.
- **Did not delete `ChatLog`.** It still backs the audit page and any
  cross-module analytics. Conversations + ChatLog cost ~doubled writes per
  query, fine for the demo; can be unified later.
