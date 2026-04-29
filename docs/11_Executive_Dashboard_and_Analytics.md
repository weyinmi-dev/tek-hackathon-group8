# Executive Dashboard and Analytics

This document explains every metric, chart, and data panel in TelcoPilot's analytics and dashboard surfaces — what each one measures, why it matters operationally, and how different roles consume it differently.

TelcoPilot has two analytics surfaces: the **Dashboard** (`/dashboard`) provides the real-time command center overview, and the **Insights** page (`/insights`) provides the full analytical breakdown. Both surfaces are accessible to all authenticated roles.

---

## The Command Center Dashboard

The Dashboard is designed around a single principle: *maximum situational awareness in minimum scan time*. A NOC engineer arriving at the start of their shift should be able to assess overall network health in under 10 seconds without reading a single log line.

### KPI Strip

The top of the Dashboard renders six KPI cards, each with a headline number and a sparkline trend. These are the six metrics that together define whether the Lagos metro network is healthy.

| KPI Card | What It Measures | Why It Matters |
|---|---|---|
| **Network Uptime %** | Percentage of tower-hours in the monitoring window that were in an operational (non-critical) state | The primary SLA metric. A drop here is the signal that triggers escalation |
| **Avg Latency (ms)** | Mean round-trip latency across all monitored Lagos metro towers in the current window | Subscriber experience quality indicator. Rising latency precedes congestion events |
| **Active Incidents** | Count of unacknowledged alerts at Warning or Critical severity | Operational queue depth. Drives shift staffing decisions |
| **Total Towers** | Count of towers currently in the monitoring scope | Context denominator for all percentage-based metrics |
| **Subscribers Affected** | Estimated subscriber count impacted by towers in Warning or Critical state | Business impact translation of network state — connects technical metrics to human cost |
| **Copilot Queries** | Count of AI Copilot queries in the current session or 24h window | Copilot adoption metric. High counts indicate active use; zero counts during an incident suggests engineers are not leveraging AI assistance |

### Sparklines

Each KPI card displays a 6-point sparkline — a miniaturized trend line showing the metric's movement over the most recent monitoring windows. Sparklines serve a critical function that the headline number alone cannot: they convey *direction*.

A headline uptime of 99.6% reads differently when the sparkline is flat (stable at 99.6%) versus when it shows a downward slope from 99.9% — the latter demands immediate investigation even if the absolute value is still acceptable. The 6-point window is chosen to give enough context for trend recognition without requiring detailed time-axis reading.

The Dashboard polls the `/api/metrics` endpoint every **30 seconds**, so sparklines refresh automatically during a shift without requiring a page reload.

---

## Insights Page: Full Analytics

The Insights page provides the deeper analytical context that managers, shift leads, and executives use for operational reviews, capacity planning, and SLA reporting.

### Network Latency Chart (p95, 24-Hour, Per-Region)

The latency chart plots **p95 latency** for each of the four major Lagos regions over the most recent 24-hour window. It renders three lines with distinct severity colouring:

- **Lagos West** — plotted in critical red. This region shows elevated p95 values indicating congestion or infrastructure stress.
- **Ikeja** — plotted in warning yellow. Intermittent spikes suggest partial congestion.
- **Victoria Island / Ikoyi** — plotted in green. Operating within acceptable parameters.

**Why p95, not average?**

Average latency is dominated by the fast, clean majority of requests. It can remain low even when 5% of traffic — potentially hundreds of thousands of subscriber-seconds — is experiencing severe delays. The 95th percentile captures the worst-case experience that a significant proportion of subscribers actually encounter. In SLA contexts, MTN's subscriber experience commitments are typically expressed in terms of the worst decile, not the mean. p95 is the operationally honest metric.

**Why per-region?**

Aggregating latency across all of Lagos masks the geographic distribution of problems. A global average of 45ms is not actionable. Knowing that Lagos West is at 78ms p95 while Victoria Island is at 28ms immediately focuses the diagnostic effort on the right region. This is the first question a NOC engineer asks when latency spikes: *where?*

### Regional Health Panel

The Regional Health panel renders a bar chart showing average signal strength percentage per region. Each bar is coloured by health status (green/yellow/red). This panel answers the question: *which regions are underperforming, and by how much?*

The per-region signal breakdown supports three operational uses:

1. **Field dispatch prioritisation**: Engineers can identify which regions need physical inspection first.
2. **Subscriber communication**: Customer experience teams can use regional health to proactively communicate to subscribers in affected areas.
3. **Capacity planning**: Persistently low signal in a region indicates coverage gaps requiring infrastructure investment.

### Incident Type Distribution

The incident type bar chart breaks down the current or recent incident count by root cause category:

| Incident Type | Description |
|---|---|
| **Fiber** | Physical cable cuts or damage — typically from construction activity |
| **Power** | Generator failures, grid outages, or fuel shortages at tower sites |
| **Congestion** | Traffic demand exceeding capacity — a software/config resolvable issue |
| **Thermal** | Equipment overheating — common during peak-heat seasons in Lagos |
| **Hardware** | Radio unit failures, antenna damage |
| **Other** | Uncategorised or multi-factor incidents |

Understanding incident type distribution is essential for resource planning. If 60% of incidents in a given week are power-related, the field team needs fuel delivery contracts, not additional RF engineers.

### SLA Compliance Donut

The SLA compliance panel renders a donut chart showing **current SLA attainment vs target**:

- **Current attainment**: 99.847%
- **SLA target**: 99.95%
- **Gap**: 0.103 percentage points

This gap appears small in absolute terms. Operationally, it is not small.

At 99.95% uptime (the target), the allowed downtime per month is approximately **21.9 minutes**. At 99.847% (current), the actual downtime is approximately **45.7 minutes per month** — more than double the target. For a network serving millions of Lagos subscribers, each minute of additional downtime represents a concrete number of failed calls, dropped connections, and degraded mobile data sessions.

The donut chart is coloured to make the gap visually immediate: the green arc represents attainment, the red arc represents the shortfall against target. A manager reviewing this panel before a board meeting can instantly translate the visual into an operational narrative.

### Copilot Top Queries Panel

The Copilot top queries panel shows the most frequently asked natural language questions in the monitoring window. In production, this is populated from the `chat_logs` table in the `ai` schema, joined with query counts. For the demo, representative queries are displayed.

This panel serves a governance function: it gives managers visibility into what their engineers are investigating. Clustered queries about a specific region or tower code are an early warning signal of an emerging incident even before it formally escalates to a critical alert.

### Audit Trail

The audit trail is accessible via `/audit` (manager+ only) and surfaced as a time-ordered table of every significant platform action.

| Audit Field | Description |
|---|---|
| **Timestamp** | UTC timestamp of the action |
| **Actor** | User handle (e.g., `oluwaseun.a`) |
| **Role** | Role at time of action |
| **Action** | Verb describing the operation (e.g., `copilot_query`, `alert_acknowledged`, `user_created`) |
| **Target** | The entity affected (tower code, alert ID, user email) |
| **Source IP** | IP address of the client at time of action |

**Compliance value**: Post-incident reviews in regulated industries require demonstrating that operators followed procedures. The audit trail provides a timestamped, role-attributed record that can be exported for regulatory reporting.

**Governance value**: Managers can review whether engineers acknowledged alerts within SLA window, which queries were asked during an incident, and whether any administrative changes coincided with a degradation event.

---

## How Different Roles Use the Analytics Surface

| Role | Primary Use | Key Panels |
|---|---|---|
| **Viewer / Executive** | SLA compliance reporting, board-level briefings | Uptime KPI, SLA donut, regional health bar chart |
| **Engineer** | Shift situational awareness, incident priority | Active incidents KPI, latency chart (which region?), incident type distribution |
| **Manager / Shift Lead** | Operational review, SLA gap analysis, team audit | All KPIs, SLA donut, audit trail, Copilot query panel |
| **Admin** | Platform health, usage monitoring | Copilot queries count, audit trail (admin actions) |

---

## Cross-References

- Metrics API endpoint: [12_API_Documentation.md](12_API_Documentation.md)
- Analytics module database schema: [08_Database_and_Data_Flow.md](08_Database_and_Data_Flow.md)
- Role access to audit trail: [10_User_Roles_and_RBAC.md](10_User_Roles_and_RBAC.md)
