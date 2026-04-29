# Project Demo Guide

This document is the operational playbook for demonstrating TelcoPilot to judges, stakeholders, or any audience. It covers prerequisites, startup, five demo scenarios with step-by-step scripts, demo tips, and reset instructions.

---

## Prerequisites

Before the demo, verify the following:

| Requirement | Check |
|---|---|
| Docker Desktop is running | Whale icon visible in system tray; `docker ps` returns no error |
| Port 80 is free | No other service (IIS, another Docker project) bound to port 80 |
| `.env` file exists in project root | Copied from `.env.example`; `JWT_SECRET` is 32+ characters |
| Docker images are pre-built | Run `docker compose build` before the demo — don't build live |
| Browser is in dark mode | The UI is dark-first; a bright browser UI jarring against the dark app looks bad |

---

## Quick Start

```bash
# From the project root
docker compose up --build

# Wait for "Now listening on: http://[::]:8080" in the backend logs
# Then open:
http://localhost
```

Allow approximately 60–90 seconds for the first startup (PostgreSQL initialisation + backend seed data). On subsequent starts without `-v`, the database already has data and startup takes ~15 seconds.

The application is ready when:
- `http://localhost` shows the TelcoPilot login page
- `http://localhost/health` returns `{"status":"Healthy"}`

---

## Demo Accounts

| Account | Role | Password | Best used for |
|---|---|---|---|
| oluwaseun.a@telco.lag | Engineer | Telco!2025 | Copilot queries, alert acknowledgment, map interaction |
| amaka.o@telco.lag | Manager | Telco!2025 | Audit trail, user management |
| tunde.b@telco.lag | Admin | Telco!2025 | Full platform, user admin |
| kemi.a@telco.lag | Viewer | Telco!2025 | Read-only access, RBAC demo |

**Recommendation**: Start the demo logged in as the Engineer account (`oluwaseun.a`). Switch to the Manager account (`amaka.o`) for the governance scenario. This tells the role story naturally.

---

## Demo Scenario 1: "Why is Lagos West slow?" — Copilot Diagnosis

**Role**: Engineer (`oluwaseun.a@telco.lag`)

**Narrative**: "Our engineer starts their shift. The dashboard shows elevated latency in Lagos West. Instead of opening five different monitoring tools, they ask TelcoPilot directly."

**Steps**:

1. Log in as `oluwaseun.a@telco.lag`
2. Point to the Dashboard KPI strip — specifically the **Avg Latency** card.
   - *Say*: "Notice the sparkline — latency has been trending upward over the last six periods."
3. Navigate to the **Copilot** page via the sidebar.
4. Click the suggested query chip: **"Why is Lagos West slow?"**
   - This populates the input field. Pause here briefly.
   - *Say*: "The engineer didn't need to remember a command syntax or navigate to a specific tool. They asked the network."
5. Press Enter or click Send.
6. Point to the **skill trace animation** as it runs.
   - *Say*: "You can see TelcoPilot invoking three AI skills in sequence — DiagnosticsSkill is pulling live tower metrics, OutageSkill is checking the incident feed, RecommendationSkill is generating the action plan."
7. When the answer appears, point to the highlighted elements.
   - *Say*: "The answer names the specific tower code, gives a root cause with confidence percentage, lists affected towers, and gives prioritised recommended actions — in plain English, in under 3 seconds."

**What to emphasise**: The speed, the specificity of the answer (not a generic "check your network"), and the skill trace (proves this is AI reasoning, not a canned response).

---

## Demo Scenario 2: Map → Copilot Integration — Diagnose from the Map

**Role**: Engineer (`oluwaseun.a@telco.lag`)

**Narrative**: "A visual thinker might prefer to find the problem spatially first. The map integrates directly with the Copilot."

**Steps**:

1. Navigate to the **Map** page via the sidebar.
2. Point to the map overview.
   - *Say*: "These are the 15 monitored towers across the Lagos metro. Red diamonds are critical, yellow are warning. Notice the pulse ring on the critical towers — that's a real-time visual alert."
3. Hover over a **red (critical) tower** — TWR-LW-003 in Lagos West is a good choice.
   - *Say*: "Hovering shows the real-time status: signal at 61%, load at 94%, active fiber incident."
4. Click the tower to open the detail panel.
5. Click the **Diagnose** button.
   - *Say*: "One click sends this tower directly to the Copilot for AI diagnosis."
6. The Copilot opens with the query pre-populated: `"Diagnose tower TWR-LW-003"`.
7. Submit and show the result.
8. Also point to the **Best Signal Zones** panel on the right side of the Map page.
   - *Say*: "For field teams or subscribers asking 'where's the best signal?', TelcoPilot highlights the top-performing regions automatically — Victoria Island and Ikoyi are currently in the green."

**What to emphasise**: The two-click path from visual observation to AI diagnosis, and the Best Signal Zones panel as an answer to the connectivity recommendation theme.

---

## Demo Scenario 3: Alerts — Severity Feed and Acknowledgment

**Role**: Engineer (`oluwaseun.a@telco.lag`)

**Narrative**: "Network events generate alerts. TelcoPilot doesn't just show them — it attributes a root cause and lets the engineer act."

**Steps**:

1. Navigate to the **Alerts** page via the sidebar.
2. Point to the severity filter bar.
   - *Say*: "The engineer can filter to critical only — no noise from informational alerts during an active incident."
3. Click the **Critical** filter.
4. Point to the first critical alert row.
   - *Say*: "Each alert includes an AI-attributed root cause — fiber degradation on the north aggregation link — with a confidence score. This is not a static rule-based categorisation; the AI reasoned over the incident characteristics."
5. Click the **Acknowledge** button on the alert.
6. Watch the alert move to the acknowledged section.
   - *Say*: "Acknowledgment is timestamped and attributed to this engineer. That record flows immediately into the audit trail."
7. Navigate back to the Dashboard.
   - *Say*: "The Active Incidents count has dropped by one — the KPI reflects the acknowledgment in real time."

**What to emphasise**: AI root cause attribution, the operational workflow of acknowledgment, and the immediate audit trail recording.

---

## Demo Scenario 4: Manager View — Audit Trail and User Management

**Role**: Switch to Manager (`amaka.o@telco.lag`)

**Narrative**: "The shift lead needs governance visibility. They can see exactly what their engineers did, when, and why."

**Steps**:

1. Log out of the Engineer account.
2. Log in as `amaka.o@telco.lag` (Manager).
3. Notice the sidebar now shows **Audit** and **Users** — items that were not visible as an Engineer.
   - *Say*: "Role-based navigation — engineers never see governance pages they have no business accessing."
4. Navigate to the **Audit** page.
5. Point to the recent entries.
   - *Say*: "Here is the alert acknowledgment we just performed — Oluwaseun's handle, the engineer role, the alert ID, timestamp, and source IP. This is the compliance record."
6. Point to the Copilot query entries.
   - *Say*: "Every Copilot query is recorded — not just what the engineer asked, but when and from where. A post-incident review can reconstruct the entire diagnostic sequence."
7. Navigate to the **Users** page.
8. Point to the user list.
   - *Say*: "The Manager can see the full team — roles, assignment, active status. In the full implementation, they can create new accounts, change roles, and deactivate leavers — full identity lifecycle."

**What to emphasise**: Separation of duties (engineers can't see their own audit records), the compliance value of timestamped records, and the manager-only user management capability.

---

## Demo Scenario 5: RBAC in Action — Viewer Account Restrictions

**Role**: Switch to Viewer (`kemi.a@telco.lag`)

**Narrative**: "An executive needs visibility, not write access. RBAC enforces this at both the UI and API layers."

**Steps**:

1. Log in as `kemi.a@telco.lag` (Viewer).
2. Navigate to the **Dashboard** — fully accessible.
   - *Say*: "The executive can see the full KPI strip and network status."
3. Navigate to the **Alerts** page.
   - Point to the Acknowledge buttons.
   - *Say*: "Notice there are no Acknowledge buttons. The Viewer role cannot modify incident state."
4. Try to navigate to the **Copilot** page.
   - *Say*: "The sidebar shows the page exists, but accessing it returns an Access Denied — viewers don't query the AI."
5. Try to navigate to `/audit` directly in the address bar.
   - *Say*: "Even navigating directly to the audit URL is blocked at the page level. And if they called the API directly, they'd receive a 403. The enforcement is dual-layer."

**What to emphasise**: RBAC is not just UI cosmetics — it is enforced at the API layer. The Viewer role is a deliberate, useful role (executives need dashboards without write access).

---

## Demo Tips

- **Pre-load the browser tab** on the Dashboard before presenting. The 30-second polling means the data is always fresh without needing a manual reload during the demo.
- **Use the Engineer account first** for Scenarios 1–3. The narrative is more natural starting from operations before switching to governance.
- **If Azure OpenAI is not configured** (`AI_PROVIDER=Mock`), the Copilot responses are from the MockCopilotOrchestrator — they are realistic, structured, and named with real tower codes. Tell judges: "We've built this to work with the mock provider for demo stability, and the Azure OpenAI integration is a configuration change."
- **Keep Swagger open** in a separate tab: `http://localhost/swagger`. If a judge asks "show me the API", switch to this tab and demonstrate the live `/api/chat` endpoint with the "Try it out" button.
- **If the demo environment is slow**, the Copilot skill trace animation is visible for longer — this is actually helpful for narration.

---

## Reset Instructions

To fully reset the environment (clear all data and rebuild from seed):

```bash
# Stop and remove all containers AND volumes (this deletes the database)
docker compose down -v

# Rebuild and restart
docker compose up --build
```

The `-v` flag is the important one — it removes the named volumes (`telcopilot-pg-data`, `telcopilot-redis-data`). Without it, `docker compose down` stops containers but preserves the database, so audit trail entries and acknowledged alerts from the demo will still be present.

After `up --build`, allow 60–90 seconds for full initialisation before navigating to the application.

---

## Cross-References

- All demo account details: [10_User_Roles_and_RBAC.md](10_User_Roles_and_RBAC.md)
- UI/UX walkthrough and page descriptions: [16_UI_UX_and_User_Flows.md](16_UI_UX_and_User_Flows.md)
- Setup and local development guide: [20_Setup_and_Local_Development.md](20_Setup_and_Local_Development.md)
