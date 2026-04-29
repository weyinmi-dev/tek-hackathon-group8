# Business Problem and Solution

This document frames the operational context TelcoPilot was designed for, articulates the specific pain points it addresses, and describes the business impact and adoption pathway for an organisation like MTN Nigeria.

---

## The Business Problem

### Lagos Metro NOC: Scale and Complexity

MTN Nigeria operates one of the densest urban telecommunications networks in West Africa. The Lagos metropolitan area is served by approximately 1,200+ active base stations spread across distinct geographic and demographic zones — from the financial district of Victoria Island, through the dense residential corridors of Surulere and Festac, to the rapidly expanding coastal zones of Lekki Phase 1 and 2.

The NOC team responsible for this network faces a unique convergence of pressures:

**Infrastructure heterogeneity.** Lagos sites range from legacy 2G/3G towers erected in the early 2000s to newly commissioned 5G NSA sites. Each generation of equipment produces telemetry in different formats, exposed through different interfaces. An engineer diagnosing a regional slowdown must mentally combine RAN metrics, transport-layer backhaul statistics, power system alarms, and crowd-sourced signal reports — none of which live in the same system.

**External dependency exposure.** Backhaul fiber cuts driven by civil-works activity are the leading cause of outages in the Lekki and Victoria Island corridors. Power grid failures from DisCo (IKEDC/EKEDC) feeders are the primary driver of outages in Lagos West. These are external events that do not surface clearly in any single telemetry feed — they require correlation between tower alarms, area activity reports, and prior incident history to diagnose quickly.

**Operational tempo.** During peak hours, an on-call engineer may be processing 30–50 simultaneous alarm states across 15+ towers. The cognitive load of prioritising, correlating, and initiating remediation actions across this many simultaneous events — while coordinating field teams and updating management — is unsustainable at scale.

**Knowledge concentration.** Diagnostic expertise in a NOC is largely experiential. A senior engineer who has seen 11 fiber-cut incidents on the TG-LEK-A backhaul ring in 12 weeks develops a pattern recognition capability that a junior engineer simply does not have. When that senior engineer is off-shift, MTTR increases sharply. This knowledge is not systematically captured, indexed, or made queryable.

**Compliance and auditability.** Nigerian Communications Commission (NCC) and internal SLA frameworks require documented evidence of how incidents were detected, triaged, and resolved. Reconstructing this narrative from fragmented log files after the fact is time-consuming and error-prone.

### The Operational Gap

The specific gap TelcoPilot addresses is the absence of a unified intelligent reasoning layer between raw network telemetry and the engineer making a decision. The current state requires an engineer to:

1. Observe an alert in the network management system
2. Open a separate interface to pull tower metrics
3. Cross-reference with an incident history database to recall prior similar events
4. Consult a runbook document (if one exists and is up to date) for recommended actions
5. Manually compose a ticket with root cause, affected scope, and recommended remediation
6. Log the action in a separate audit system

Each step in this chain introduces latency. When the affected subscriber count for a single critical incident can exceed 14,000 (as with INC-2841 in the Lekki Phase 1 case), every additional minute of MTTR has a direct, quantifiable subscriber experience cost.

---

## The Solution: AI-Native NOC Platform

TelcoPilot collapses this multi-step diagnostic process into a single natural language interaction:

> **Engineer types**: *"Why is Lagos West slow?"*
>
> **TelcoPilot responds** (within seconds):
>
> **ROOT CAUSE**
> Backhaul fiber degradation on TG-LEK-A serving TWR-LEK-003 (Lekki Phase 1) — packet loss climbed from 2% to 60% in the last 12 minutes. Correlated with civil-works permit issued in the area at 16:50. Matches pattern from INC-2839 [OUTAGE-LEKKI-CORRIDOR-2025].
>
> **AFFECTED**
> - Lekki Phase 1 — 14,200 subscribers
> - Spillover to TWR-LEK-008 (Phase 2) at 88% load
> - 4G voice + data; no impact on 5G NSA
>
> **RECOMMENDED ACTIONS**
> 1. Dispatch field-team-3 to fiber junction LJ-7 (ETA <30 min)
> 2. Auto-shed traffic from LEK-003 → LEK-008, LEK-014
> 3. Open ticket with civil-works contractor — request immediate halt
>
> **CONFIDENCE**
> 92% — pattern matches 11 prior fiber-cut incidents this quarter

This answer is not a static template. The Copilot invokes live data queries (DiagnosticsSkill, OutageSkill), retrieves relevant historical context from the knowledge base (KnowledgeSkill with pgvector similarity search), and applies runbook-backed recommendations (RecommendationSkill) — then synthesises them into a structured response via Azure OpenAI GPT-4o-mini using Semantic Kernel's automatic function calling.

### What Makes This Architecture Distinctive

**Real data, not scripted answers.** The AI Copilot's response is grounded in live tower metrics, active alert states, and indexed historical incident documents. If a new tower comes online or a new incident resolves, the next query reflects that state.

**Traceable reasoning.** Every query shows the SkillTrace — a real-time animation of which Semantic Kernel plugins were invoked, in what order, and with what latency. This builds engineer trust: the answer is not a black box, it is an auditable reasoning chain.

**Structured output format.** The system prompt enforces ROOT CAUSE / AFFECTED / RECOMMENDED ACTIONS / CONFIDENCE for every response. This is not cosmetic — it directly maps to the incident report format expected by NOC management and compliance systems.

**Dual-mode operation.** The platform runs fully in Mock mode without Azure OpenAI credentials. The MockCopilotOrchestrator hits the same real APIs (Network, Alerts) and the same RAG pipeline, producing the same structured answer format. This means TelcoPilot can be demonstrated, evaluated, and partially deployed before any AI subscription is procured.

---

## Business Impact

### Reduced Mean Time to Respond (MTTR)

For a single fiber-cut class incident, the current manual process from alert to first remediation action takes approximately 8–15 minutes in a well-staffed NOC. TelcoPilot's Copilot produces a root-cause hypothesis, affected scope, and concrete action list in under 5 seconds. Assuming a conservative reduction from 12 to 3 minutes of cognitive triage time per P1 incident, and 47 P1/P2 incidents per quarter (as seen in the Q1 data), the aggregate MTTR saving is approximately **423 engineer-minutes per quarter** on diagnostic activity alone — time that can be redirected to remediation.

### Democratised Diagnostic Expertise

Junior engineers using TelcoPilot have access to the same diagnostic playbooks and historical incident correlations as a senior engineer who has been on the NOC team for three years. The KnowledgeSkill retrieves relevant SOPs (SOP-FIBER-CUT-V3, SOP-POWER-FAILOVER-V2), prior incident writeups (INC-2841-WRITEUP, INC-2840-WRITEUP), and tower performance trends from the pgvector knowledge base. This institutional knowledge is no longer locked in individual heads — it is indexed, searchable, and surfaced in context.

### Proactive Outage Detection

TelcoPilot's alert system includes AI-attributed confidence scores and predicted failure windows. INC-2839 in the seeded dataset illustrates this: the system identifies TWR-LAG-W-031's thermal trend and load pattern as carrying an 87% probability of fault within 2 hours — before the tower enters a critical state. This predictive posture shifts some incidents from reactive triage to preventive maintenance scheduling.

### Compliance-Ready Audit Trail

Every Copilot query, alert acknowledgment, user role change, and admin action is recorded in the analytics.audit_entries table with actor, role, action, target, timestamp, and source IP. This provides an immediately queryable, tamper-evident audit trail that satisfies NCC incident documentation requirements and internal SLA review processes without any additional logging effort.

### Unified Operational Picture

TelcoPilot's Command Center dashboard aggregates KPIs (network uptime, average latency, active incidents, towers online, subscribers affected, Copilot queries), the network map, a live alert ticker, and the embedded Copilot in a single view. Management no longer needs to switch between systems to achieve situational awareness — the relevant signals are co-located and updated every 30 seconds.

---

## Real-World Adoption Angle: How MTN Nigeria Would Deploy TelcoPilot

### Phase 1: Docker Compose on On-Premise Infrastructure (Weeks 1–4)

MTN Nigeria's NOC infrastructure already runs containerised workloads. TelcoPilot's Docker Compose stack deploys as a single `docker compose up --build` from the repository root, exposing a single port (80) behind NGINX. The backend connects to any existing PostgreSQL 17 instance and Redis cluster via environment variables. The AI module defaults to Mock mode, so Phase 1 delivers the full dashboard, alert management, user management, and audit trail without any Azure OpenAI spend.

### Phase 2: Azure OpenAI Integration and Knowledge Base Seeding (Weeks 4–8)

The switch from Mock to Azure OpenAI requires setting three environment variables (`Ai__Provider=AzureOpenAi`, `Ai__AzureOpenAi__Endpoint`, `Ai__AzureOpenAi__ApiKey`, `Ai__AzureOpenAi__Deployment`) and restarting the backend container. No code changes, no redeployment of other services. The knowledge base is pre-seeded with representative incident reports, SOPs, and tower performance history. The RAG pipeline uses pgvector's cosine distance operator for similarity search — the same PostgreSQL instance already running for operational data.

MTN Nigeria's incident management team can ingest existing documentation — Confluence SOPs, post-incident writeups, tower health reports — through the Documents module, which supports local upload, Google Drive, OneDrive, SharePoint, and Azure Blob Storage as document sources.

### Phase 3: Azure Container Apps or AKS (Months 2–3)

The modular monolith architecture is stateless (JWT, no server-side session) and container-native. Migration from Docker Compose to Azure Container Apps requires updating the connection strings and environment variables, pointing the Azure Container Apps ingress to replace NGINX, and configuring Azure Database for PostgreSQL Flexible Server and Azure Cache for Redis as managed PaaS services. No architectural changes are required.

For MTN Nigeria's scale, Azure Kubernetes Service (AKS) with horizontal pod autoscaling is the natural endpoint. The single backend container can be scaled horizontally — the stateless JWT and Redis-backed caching mean any pod can serve any request.

### Phase 4: OSS/BSS Integration (Months 3–6)

The cross-module API contract pattern (INetworkApi, IAlertsApi) provides the integration seam for connecting TelcoPilot to MTN Nigeria's existing OSS/BSS systems. Instead of the current seeded data, the NetworkApi implementation would query a live telemetry ingestion pipeline. The Alerts module would subscribe to the existing alarm management system via the IEventBus integration event pattern already wired in the infrastructure layer.

---

## ROI Narrative

TelcoPilot is not a reporting dashboard. It is a force multiplier for NOC engineers. The return on investment case rests on three pillars:

**Operational efficiency.** If TelcoPilot reduces average diagnostic triage time by 8 minutes per P1/P2 incident and the NOC handles 200 such incidents per quarter, that is 1,600 engineer-minutes — approximately 27 engineer-hours — freed per quarter for remediation rather than diagnosis. At NOC engineer burdened cost rates, this is a meaningful direct saving.

**Subscriber experience.** Each minute of unmitigated subscriber impact on a critical incident affects tens of thousands of subscribers and generates NCC regulatory exposure. Reducing MTTR from detection to first remediation action by 50% directly reduces the subscriber-minutes of degraded experience per incident.

**Knowledge retention.** The platform captures diagnostic reasoning in a queryable knowledge base as a byproduct of normal operation. Every incident that passes through TelcoPilot — annotated with root cause, affected scope, and recommended actions — enriches the RAG corpus for future queries. This compounds in value: the more incidents TelcoPilot sees, the better its historical context retrieval becomes.

**Compliance cost reduction.** The automated audit trail eliminates the manual effort of reconstructing incident timelines for NCC filings and SLA reviews. For an organisation handling 200+ incidents per quarter, this is a significant reduction in compliance overhead.
