# API Documentation

This document provides full reference documentation for all 10 TelcoPilot API endpoints: method, path, authentication requirement, request body, response shape, error responses, and example usage.

The API is a RESTful HTTP API served by the ASP.NET Core 10 backend on internal port 8080, proxied through NGINX at the public root. In the Docker Compose deployment, all API calls are made to `http://localhost/api/*`.

**Interactive documentation**: Swagger UI is available at `http://localhost/swagger` in the Development environment.

---

## Authentication

All protected endpoints require a Bearer token in the `Authorization` header:

```
Authorization: Bearer <access_token>
```

Access tokens are JWTs signed with HMAC-SHA256. They expire after a configurable window (default: 60 minutes in development). Use the refresh endpoint to obtain a new token pair before expiry.

---

## Error Format

All error responses follow the RFC 7807 `ProblemDetails` format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid credentials.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

| HTTP Status | Meaning |
|---|---|
| 400 | Validation failure — request body invalid |
| 401 | Missing or invalid Bearer token |
| 403 | Valid token but insufficient role |
| 404 | Resource not found |
| 500 | Unhandled server error (see logs) |

---

## Endpoint Reference

---

### POST /api/auth/login

Authenticate a user with email and password. Returns an access token and a refresh token.

**Authentication**: Anonymous (no token required)

**Request Body**:

```json
{
  "email": "oluwaseun.a@telco.lag",
  "password": "Telco!2025"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `email` | string | yes | User email address |
| `password` | string | yes | Plaintext password (BCrypt comparison server-side) |

**Response — 200 OK**:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "expiresIn": 3600,
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "oluwaseun.a@telco.lag",
    "name": "Oluwaseun Adeyemi",
    "handle": "oluwaseun.a",
    "role": "engineer",
    "team": "NOC Shift A",
    "region": "Lagos West"
  }
}
```

**Error Responses**:

| Status | Condition |
|---|---|
| 400 | Missing email or password |
| 401 | Email not found or password does not match |

**Example**:

```bash
curl -s -X POST http://localhost/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"oluwaseun.a@telco.lag","password":"Telco!2025"}' \
  | jq .accessToken
```

---

### POST /api/auth/refresh

Exchange a valid refresh token for a new access token and a new refresh token. The previous refresh token is invalidated on use (rotation).

**Authentication**: Anonymous

**Request Body**:

```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4..."
}
```

**Response — 200 OK**: Same shape as `/api/auth/login` response.

**Error Responses**:

| Status | Condition |
|---|---|
| 401 | Refresh token not found, expired, or already used |

**Security note**: The refresh token stored in the database is a SHA-256 hash of the raw token returned to the client. The raw token is never stored. Replay of a used refresh token returns 401 and should be treated as a potential token theft event.

---

### GET /api/auth/me

Returns the authenticated user's profile decoded from the current access token.

**Authentication**: Bearer (any valid role)

**Response — 200 OK**:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "oluwaseun.a@telco.lag",
  "name": "Oluwaseun Adeyemi",
  "handle": "oluwaseun.a",
  "role": "engineer",
  "team": "NOC Shift A",
  "region": "Lagos West"
}
```

**Error Responses**:

| Status | Condition |
|---|---|
| 401 | Missing or expired token |

---

### GET /api/auth/users

Returns the list of all platform users. Restricted to Manager and Admin roles.

**Authentication**: Bearer — `RequireManager` policy (manager or admin)

**Query Parameters**: None

**Response — 200 OK**:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "oluwaseun.a@telco.lag",
    "name": "Oluwaseun Adeyemi",
    "handle": "oluwaseun.a",
    "role": "engineer",
    "team": "NOC Shift A",
    "region": "Lagos West",
    "isActive": true
  }
]
```

**Error Responses**:

| Status | Condition |
|---|---|
| 401 | Missing or expired token |
| 403 | Authenticated as engineer or viewer |

---

### POST /api/chat

Submit a natural language query to the AI Copilot. The backend routes the query through the Semantic Kernel orchestrator, invokes the appropriate skills (DiagnosticsSkill, OutageSkill, RecommendationSkill), and returns a structured answer with a skill trace.

**Authentication**: Bearer — `RequireEngineer` policy (engineer, manager, or admin)

**Request Body**:

```json
{
  "message": "Why is Lagos West slow right now?"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `message` | string | yes | Natural language query (max 1000 chars) |

**Response — 200 OK**:

```json
{
  "answer": "ROOT CAUSE: TWR-LW-003 is operating at 94% load with a fiber degradation event on the north aggregation link...\n\nAFFECTED TOWERS: TWR-LW-001, TWR-LW-003, TWR-LW-007\n\nRECOMMENDED ACTIONS:\n1. Dispatch field team to TWR-LW-003 for physical inspection\n2. Activate backup transport on LW-NORTH-AGG-02\n3. Monitor congestion on TWR-LW-001 — consider load rebalancing\n\nCONFIDENCE: High (87%)",
  "skillTrace": [
    { "skill": "DiagnosticsSkill", "input": "Lagos West", "durationMs": 43 },
    { "skill": "OutageSkill", "input": "Lagos West", "durationMs": 31 },
    { "skill": "RecommendationSkill", "input": "TWR-LW-003", "durationMs": 28 }
  ],
  "queryId": "b1e2f3a4-d5c6-7890-abcd-ef1234567890",
  "timestamp": "2026-04-28T10:32:15Z"
}
```

| Response Field | Description |
|---|---|
| `answer` | Formatted plain-text response from the orchestrator |
| `skillTrace` | Ordered list of Semantic Kernel skills invoked, with timing |
| `queryId` | UUID of the persisted ChatLog entry (links to audit trail) |
| `timestamp` | UTC timestamp of query processing |

**Error Responses**:

| Status | Condition |
|---|---|
| 400 | Empty or too-long message |
| 401 | Missing or expired token |
| 403 | Authenticated as viewer |
| 500 | AI provider unreachable (Azure OpenAI timeout or key invalid) |

**Example**:

```bash
curl -s -X POST http://localhost/api/chat \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"message":"Which region has the worst signal quality right now?"}' \
  | jq '{answer: .answer, skills: [.skillTrace[].skill]}'
```

---

### GET /api/metrics

Returns the full metrics payload used by the Dashboard KPI strip and Insights charts: KPI values, sparkline data, and regional health breakdown.

**Authentication**: Bearer (any valid role)

**Query Parameters**: None

**Response — 200 OK**:

```json
{
  "uptime": 99.847,
  "avgLatencyMs": 47.3,
  "activeIncidents": 3,
  "totalTowers": 15,
  "subscribersAffected": 42800,
  "copilotQueries": 12,
  "sparklines": {
    "uptime": [99.92, 99.90, 99.88, 99.85, 99.86, 99.847],
    "latency": [41.2, 43.5, 45.0, 46.8, 47.1, 47.3],
    "incidents": [1, 1, 2, 2, 3, 3]
  },
  "regionalHealth": [
    { "region": "Lagos West", "avgSignal": 61.4, "status": "critical" },
    { "region": "Ikeja", "avgSignal": 74.2, "status": "warn" },
    { "region": "Victoria Island", "avgSignal": 88.7, "status": "ok" },
    { "region": "Ikoyi", "avgSignal": 85.3, "status": "ok" }
  ]
}
```

**Caching**: This response is cached in Redis with a configurable TTL. Cache key: `metrics:summary`.

**Error Responses**:

| Status | Condition |
|---|---|
| 401 | Missing or expired token |

---

### GET /api/metrics/audit

Returns the paginated audit trail. Each entry records a platform action with actor, role, action type, target, and timestamp.

**Authentication**: Bearer — `RequireManager` policy (manager or admin)

**Query Parameters**:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `page` | int | 1 | Page number (1-indexed) |
| `pageSize` | int | 50 | Entries per page (max 200) |
| `actorHandle` | string | (none) | Filter by user handle |
| `action` | string | (none) | Filter by action type |

**Response — 200 OK**:

```json
{
  "totalCount": 148,
  "page": 1,
  "pageSize": 50,
  "entries": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "timestamp": "2026-04-28T09:15:22Z",
      "actorHandle": "oluwaseun.a",
      "actorRole": "engineer",
      "action": "copilot_query",
      "target": "Lagos West slow",
      "sourceIp": "172.20.0.5"
    }
  ]
}
```

**Error Responses**:

| Status | Condition |
|---|---|
| 401 | Missing or expired token |
| 403 | Authenticated as engineer or viewer |

---

### GET /api/alerts

Returns the alert feed, optionally filtered by severity. Alerts are ordered by severity (critical first) then by timestamp descending.

**Authentication**: Bearer (any valid role)

**Query Parameters**:

| Parameter | Type | Values | Description |
|---|---|---|---|
| `severity` | string | `critical`, `warning`, `info` | Filter to a specific severity level |

**Response — 200 OK**:

```json
[
  {
    "id": "inc-20260428-001",
    "towerId": "TWR-LW-003",
    "region": "Lagos West",
    "severity": "critical",
    "type": "fiber",
    "description": "North aggregation link degraded — 67% packet loss detected",
    "rootCause": "Fiber splice failure on LW-NORTH-AGG-02",
    "confidence": 0.87,
    "reportedAt": "2026-04-28T08:42:11Z",
    "acknowledgedAt": null,
    "acknowledgedBy": null
  }
]
```

| Field | Description |
|---|---|
| `severity` | `critical` / `warning` / `info` |
| `type` | Incident category (fiber, power, congestion, thermal, hardware, other) |
| `rootCause` | AI-attributed root cause hypothesis |
| `confidence` | AI confidence score (0.0–1.0) |
| `acknowledgedAt` | Null if unacknowledged |
| `acknowledgedBy` | Handle of acknowledging engineer (null if unacknowledged) |

**Error Responses**:

| Status | Condition |
|---|---|
| 401 | Missing or expired token |
| 400 | Invalid severity value |

**Example**:

```bash
curl -s http://localhost/api/alerts?severity=critical \
  -H "Authorization: Bearer $TOKEN" | jq '.[0].rootCause'
```

---

### POST /api/alerts/{id}/ack

Acknowledge an alert by its ID. Records the acknowledging engineer's handle and timestamp. Once acknowledged, the alert is removed from the active incident count.

**Authentication**: Bearer — `RequireEngineer` policy (engineer, manager, or admin)

**Path Parameters**:

| Parameter | Type | Description |
|---|---|---|
| `id` | string | Alert ID (e.g., `inc-20260428-001`) |

**Request Body**: None required. The acknowledging user is derived from the Bearer token claims.

**Response — 200 OK**:

```json
{
  "id": "inc-20260428-001",
  "acknowledgedAt": "2026-04-28T10:45:00Z",
  "acknowledgedBy": "oluwaseun.a"
}
```

**Error Responses**:

| Status | Condition |
|---|---|
| 401 | Missing or expired token |
| 403 | Authenticated as viewer |
| 404 | Alert ID not found |
| 409 | Alert already acknowledged |

**Side effect**: This action records an `alert_acknowledged` entry in the audit trail with the engineer's handle, role, alert ID, and timestamp.

---

### GET /api/map

Returns the full tower map payload: tower list with coordinates, status, and signal data, plus the regional aggregates used by the Best Signal Zones panel.

**Authentication**: Bearer (any valid role)

**Response — 200 OK**:

```json
{
  "towers": [
    {
      "id": "TWR-LW-001",
      "name": "Lagos West Alpha",
      "region": "Lagos West",
      "lat": 6.4550,
      "lng": 3.3841,
      "status": "ok",
      "signal": 81.3,
      "load": 67.2,
      "lastUpdated": "2026-04-28T10:30:00Z"
    }
  ],
  "regionHealth": [
    { "region": "Lagos West", "avgSignal": 61.4, "towerCount": 4, "status": "critical" },
    { "region": "Victoria Island", "avgSignal": 88.7, "towerCount": 3, "status": "ok" }
  ]
}
```

| Tower Status | Condition |
|---|---|
| `critical` | Signal < 40% OR load > 90% OR active incident |
| `warn` | Signal 40–65% OR load 70–90% |
| `ok` | Signal > 65% AND load < 70% AND no active incident |

**Caching**: This response is cached in Redis with a 15-second TTL. Cache key: `map:lagos`.

**Error Responses**:

| Status | Condition |
|---|---|
| 401 | Missing or expired token |

---

## Swagger / OpenAPI

In the `Development` environment, Swagger UI is served at:

```
http://localhost/swagger
```

The OpenAPI specification is generated automatically from the ASP.NET Core endpoint metadata and XML documentation comments. All request and response types are documented in the spec.

---

## Cross-References

- Authentication flow: [09_Authentication_and_Security.md](09_Authentication_and_Security.md)
- Role requirements and policies: [10_User_Roles_and_RBAC.md](10_User_Roles_and_RBAC.md)
- Backend handler implementation: [04_Backend_Architecture.md](04_Backend_Architecture.md)
