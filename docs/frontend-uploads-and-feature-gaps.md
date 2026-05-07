# Frontend uploads, OneDrive, and feature gaps after the recent backend work

A walkthrough of where uploaded files actually land, what the **Knowledge** page does (and doesn't) accept, the current state of the OneDrive path, and concrete frontend features worth adding now that Stage-4/Stage-5 of the ingestion pipeline have shipped.

---

## 1. What local folder is the app reading from when files are uploaded?

There are **two** different upload flows. They go to different endpoints and land in different places.

### A. Knowledge document uploads (SOPs, runbooks, incident reports, etc.)

These are written to disk by [LocalDocumentStorageProvider](src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Storage/Providers/LocalDocumentStorageProvider.cs).

| Run mode | Default path | Override |
| --- | --- | --- |
| .NET Aspire (`dotnet run --project src/AppHost`) | `./.telcopilot/documents` next to the AppHost working directory | `Ai:Documents:LocalRoot` user-secret / config value |
| Docker Compose | `/var/telcopilot/documents` inside the API container, mapped to the named volume `telcopilot-doc-store` (so it survives container restarts) | `AI_DOCUMENTS_LOCAL_ROOT` env var (see [.env.example:32](.env.example#L32) and [docker-compose.yml:92](docker-compose.yml#L92)) |

The path is wired up in:
- [DocumentsOptions.cs:16](src/Modules/Ai/Modules.Ai.Application/Rag/Documents/DocumentsOptions.cs#L16) — defaults
- [AppHost/Program.cs:70-71](src/AppHost/Program.cs#L70-L71) — Aspire env-var injection
- [LocalDocumentStorageProvider.cs:54-69](src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Storage/Providers/LocalDocumentStorageProvider.cs#L54-L69) — the actual `File.Create` + path-traversal guard

Each saved file gets a storage key of the form `{guid:N}-{sanitisedFileName}`, so the original filename survives but you cannot escape the configured root.

### B. Network log uploads (csv / json / xlsx / txt)

These go to [POST /api/network/ingest](src/Web.Api/Endpoints/Network/NetworkIngestion.cs) and are **not persisted as files**. The orchestrator buffers the bytes, hashes them, runs the deterministic 5-stage pipeline, and stores only the resulting `IngestionRun` row plus its derived alerts / optimizations. Re-uploading the same file is short-circuited via the content hash — see [ProcessNetworkLogCommandHandler.cs:33-40](src/Modules/Network/Modules.Network.Application/Ingestion/Pipeline/ProcessNetworkLogCommandHandler.cs#L33-L40).

So: **knowledge docs hit a folder on disk; network logs do not.**

---

## 2. Is the Knowledge page where I can upload the log files?

**No.** The page at [/documents](frontend/src/app/(authed)/documents/page.tsx) (top-bar title is "Knowledge") is the RAG corpus manager. It's intended for **text / markdown SOPs and runbooks** that the Copilot retrieves against — engineering SOPs, incident reports, outage summaries, network diagnostic notes, tower performance reports, alert history. PDF / Office support is pluggable but the default extractor today is text/markdown.

Concretely the page lets you:
- Browse the indexed corpus and its chunk / embedding status.
- Upload a local document (manager+) — multipart POST to [/api/documents/upload](src/Web.Api/Endpoints/Documents/Documents.cs#L48-L86).
- Link a cloud-stored document (manager+) — JSON POST to [/api/documents/link](src/Web.Api/Endpoints/Documents/Documents.cs#L89-L116).
- Re-index (manager+) or delete (admin) an existing entry.

The pipeline panel on the right of [documents/page.tsx:143-152](frontend/src/app/(authed)/documents/page.tsx#L143-L152) describes the RAG pipeline (Source → Ingest → Extract → Chunk → Embed → pgvector). That is **not** the network-ingestion pipeline.

### So where do log files go?

Network operations logs (the csv/json/xlsx/txt files that drive anomalies, alerts, optimizations and topology updates) are processed by the **Network ingestion** endpoint at [POST /api/network/ingest](src/Web.Api/Endpoints/Network/NetworkIngestion.cs). It requires the `Engineer` role and runs the 5-stage pipeline (Parse → Analyze → Decide → Persist → Project).

There is **no frontend UI for this endpoint yet** — see the gap list in §4.

---

## 3. How do I use the OneDrive method to upload files to the application?

Today: you can't actually pull bytes from OneDrive — the provider is a deliberate **placeholder**.

### What is wired

- [OneDriveDocumentStorageProvider](src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Storage/Providers/PlaceholderCloudStorageProvider.cs#L37-L40) is registered as `IDocumentStorageProvider` for `DocumentSource.OneDrive` in [DependencyInjection.cs:288](src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L288).
- It extends [PlaceholderCloudStorageProvider](src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Storage/Providers/PlaceholderCloudStorageProvider.cs), whose `SaveAsync` / `OpenReadAsync` / `DeleteAsync` all throw a clear `InvalidOperationException("OneDrive document storage provider is registered as a placeholder. Wire a live SDK adapter ...")`.
- The [DocumentStorageRegistry](src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Storage/DocumentStorageRegistry.cs) flags placeholders as **not available**, which is what feeds the "placeholder" pill on the Storage Providers panel of the Knowledge page.

### What you can do today

In the Knowledge page, click **"+ Link cloud"** (manager+ only). The modal calls [api.linkDocument](frontend/src/lib/api.ts#L252-L267) → [POST /api/documents/link](src/Web.Api/Endpoints/Documents/Documents.cs#L89-L116) with:
- `source`: `"OneDrive"`
- `storageKey`: the OneDrive item ID you intend to register
- `title`, `fileName`, `category`, optional `region`, `tags`, `externalReference`

The link **registers** the document in the index (status `Pending`). When [LinkCloudDocumentCommandHandler](src/Modules/Ai/Modules.Ai.Application/Documents/LinkCloudDocument/LinkCloudDocumentCommandHandler.cs#L26-L31) checks `storage.IsAvailable(OneDrive)`, the placeholder fails the availability check and the link is **rejected** with `Document.ProviderUnavailable`.

### What you'd need to do to make it actually work

1. Add the Microsoft Graph SDK (`Microsoft.Graph` + `Azure.Identity`) reference to `Modules.Ai.Infrastructure`.
2. Replace `OneDriveDocumentStorageProvider` with a real implementation of `IDocumentStorageProvider`:
   - `SaveAsync` — upload bytes to a configured Drive folder (returns the Graph drive-item ID as `storageKey`).
   - `OpenReadAsync` — `GraphServiceClient.Drives[driveId].Items[storageKey].Content.GetAsync()`.
   - `DeleteAsync`, `ExistsAsync` — likewise via Graph.
3. Register the real provider in [DependencyInjection.AddDocumentPipeline](src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L274-L299) instead of the placeholder. The registry will pick it up and start reporting `IsAvailable(OneDrive) == true`, the modal's dropdown will drop the "(placeholder)" suffix, and `LinkCloudDocumentCommandHandler` will let the document through to ingestion.
4. Configure auth — you'll likely add an `Ai:Documents:OneDrive` options block (tenant ID, client ID, secret or managed identity, target drive ID).

The architecture is intentionally pluggable — swapping in Graph for OneDrive (or `Google.Apis.Drive.v3` for GoogleDrive, or `Azure.Storage.Blobs` for AzureBlob) requires **no changes to the ingestion pipeline or the document handlers**, only the provider class and DI line.

If "use OneDrive to upload" really means "let me drag a file in the browser, send it to OneDrive and ingest it from there", then the upload modal would also need a **drag-to-cloud** flow that picks the destination provider and POSTs to a new `/documents/upload-to-cloud?source=OneDrive` endpoint instead of the local one. None of that exists today.

---

## 4. Frontend features worth adding based on the recent backend updates

The last five commits added a lot of backend surface that the UI doesn't expose at all yet:

- `bd384eb` — `IngestionDashboardEntry` analytics read-model + `PipelineCompletedNotification` event.
- `f069e8b` — Stage-4 alert deduplication (creates vs updates are now tracked separately).
- `413140f` — `Optimization` aggregate + `CreateOptimizationCommand` (per-tower proposed actions: `LoadBalance`, `PowerAdjust`, `RouteReconfigure`, `AntennaRetune`, `CapacityExpansion`).
- `6fe8bea` — defer-missing-column migration handling (no UI impact, but it means schema evolution is safer).

Concrete frontend features that would actually surface this work:

### A. Network log upload page (highest leverage — no UI exists)

A new route, e.g. `/(authed)/ingest`, that:
- Drag-and-drop uploads a `.csv` / `.json` / `.xlsx` / `.txt` file to `POST /api/network/ingest` (Engineer+).
- Renders the returned `IngestionRunSummary` immediately: events parsed, anomalies detected, alerts created vs updated, optimizations proposed, topology changed, dedup short-circuit indicator, per-stage timings.
- Surfaces the dedup case ("This file was already ingested as run X on date Y") rather than silently returning the prior summary.

This is the missing twin of the Knowledge page — same page shape, but for ops logs instead of SOPs.

### B. Pipeline Activity dashboard

A panel (or sub-page) backed by a new `GET /api/analytics/ingestion-runs?take=N` endpoint that reads from `IIngestionDashboardRepository.ListRecentAsync`. For each run show file name, content-hash short, completed-at, and the five derived counts (events parsed / anomalies / alerts created / alerts updated / optimizations / topology). This is the operator-visible mirror of `PipelineCompletedNotification`.

A nice add: a tiny live ticker on the existing **Command Center** dashboard that flashes "Pipeline run completed: 3 alerts, 2 optimizations" using SSE / polling — naturally maps to the new integration event.

### C. Optimizations page

`Optimization` is a brand-new aggregate ([Optimization.cs](src/Modules/Network/Modules.Network.Domain/Optimizations/Optimization.cs)) with a `Type`, `EstimatedImpact ∈ [0,1]`, `Rationale`, `TowerCode`, and `AnomalyFingerprint`. A page at `/(authed)/optimizations` could:
- Group recent proposals by tower.
- Render impact as a 0–100% bar with type chips.
- Link each row to its motivating alert via `AnomalyFingerprint` (when non-empty).
- Offer an "Apply" action for a future executor (currently the orchestrator only persists the proposal — there's no human-approval flow yet, which is itself a backend gap worth surfacing in the UI as `Pending` status).

There's a related existing page at [/optimize](frontend/src/app/(authed)/optimize) that does diesel/solar projections — keep those distinct or unify under one Optimizations hub.

### D. Alerts page: dedup signal

The Stage-4 dedup work means the API now distinguishes `AlertsCreated` from `AlertsUpdated`. Today the [/alerts](frontend/src/app/(authed)/alerts) page treats every alert the same. Worth showing:
- A small "updated 3 mins ago via run #abc12" badge on alerts that were touched by a recent ingestion vs ones that were created.
- Counts on the run-summary card distinguishing the two so operators can see "this file mostly confirmed what we already had" vs "this file surfaced new anomalies".

### E. Per-run pipeline timeline

`IngestionRun.StageTimings` is already produced by the orchestrator. A drill-down page at `/(authed)/ingest/{runId}` could render a Gantt-style strip showing Parse / Analyze / Decide / Persist / Project durations, success per stage, and the failure reason if any. Helps debug slow / partially-failed runs without going to the logs.

### F. Toast / notification stream on pipeline completion

`PipelineCompletedNotification` is dispatched on the in-memory event bus today — the frontend has no awareness of it. The lightweight version is to poll `GET /api/analytics/ingestion-runs?since=...` every 10–15s on the Command Center; the durable version is to add a SignalR hub that fans out the event to subscribed clients. Either way the UX is the same: a toast saying "New ingestion run: 5 alerts, 2 optimizations" with a link to the run page.

### Suggested ordering

1. **Network log upload page** — biggest leverage, the backend endpoint has shipped and there's no way to invoke it from the UI today.
2. **Optimizations page** — exposes a domain concept that otherwise lives only in the database.
3. **Pipeline Activity panel + dedup signal on Alerts** — these can ride together since both rely on a `recent ingestion runs` API query.
4. **Per-run timeline + completion toasts** — polish, once the basics are visible.

---

## TL;DR

- **Local folder for knowledge uploads:** `./.telcopilot/documents` (Aspire) or `/var/telcopilot/documents` → volume `telcopilot-doc-store` (Docker). Override with `Ai:Documents:LocalRoot`.
- **Knowledge page is for SOPs/runbooks, not log files.** Network logs go to `POST /api/network/ingest`, which has **no frontend yet**.
- **OneDrive is a placeholder** that throws on use. Linking a OneDrive document fails until a `Microsoft.Graph` SDK adapter is wired in [DependencyInjection.AddDocumentPipeline](src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L274-L299).
- **Highest-value frontend gaps right now:** a Network log upload page, an Optimizations page, and a Pipeline Activity / dedup-aware Alerts view. All three map directly onto code that landed in the last few commits.
