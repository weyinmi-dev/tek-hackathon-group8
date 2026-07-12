# Phase 1 — AI Architecture Audit

**Repository:** TelcoPilot (DDD Modular Monolith, .NET 10)
**Scope:** the AI layer and everything it touches
**Status:** audit only. No code changed. No migration performed.
**Date:** 2026-07-10

---

## 1. Verdict

The AI layer is not "over-engineered orchestration." It is **three independent AI subsystems that were built at different times, never reconciled, and now overlap**:

1. **Semantic Kernel chat orchestration** — one god-prompt, one god-kernel, 23 auto-callable functions.
2. **A deterministic Stage-2 batch analyzer** — four sequential LLM calls behind a hand-rolled JSON invoker.
3. **An in-process MCP plugin registry** — a complete, working tool layer that **the LLM cannot see**, reachable only from a manual HTTP endpoint.

All three wrap the same handful of module APIs. Removing Semantic Kernel is the smaller half of this job. The larger half is collapsing three tool surfaces into one and getting AI work off the request thread.

Three findings materially change the migration plan and should be settled before Phase 2 is approved:

- **The test project does not compile.** There is currently no safety net for any refactor.
- **The shipped default configuration disables every Semantic Kernel path.** The code being migrated is the least-exercised code in the repository.
- **A runtime circular dependency exists between the AI module and the Network module**, invisible to the compiler, triggered on every document upload.

The good news, stated plainly because it shapes the migration: the domain layer is clean, the business modules are properly bounded, the API layer is thin, and an event bus already exists. The rot is confined to `Modules.Ai.Infrastructure` and the seams around it.

---

## 2. Method and confidence

Every claim below is tagged.

- **[VERIFIED]** — I read the code or ran the command this session. Cited with `file:line`.
- **[INFERRED]** — Follows from verified code plus documented external behavior I did not execute.

I built the solution (`dotnet build TelcoPilot.slnx`), traced the copilot and upload call chains end-to-end by reading every file on them, and dispatched three parallel sub-audits (Web.Api surface, SK/Pipeline internals, dead-code sweep). Where a sub-audit's conclusion mattered, I re-verified it myself; two of its claims corrected assumptions I had formed early, and both corrections are reflected below.

**Not verified this session:** runtime behavior against a live Azure OpenAI endpoint. No credentials are present in the repo. Everything about actual latency, token cost, and HTTP failures is therefore reasoning from code plus vendor limits, not observation.

---

## 3. Snapshot

| Metric | Value |
| --- | --- |
| C# files in `src/` | 414 |
| Lines of code in `src/` | 19,278 |
| Lines in `src/Modules/Ai/` | **7,697 (39.9%)** |
| Files importing `Microsoft.SemanticKernel` | 16 |
| `[KernelFunction]` methods exposed to the model | 23 |
| Kernel plugins registered | 7 (+ MCP filesystem tools) |
| `IHostedService` in the AI module | 5 |
| MediatR pipeline behaviors on every request | 5 |
| Commits touching `Modules/Ai` | 23 of 82 (28%) |
| **`src/` build** | **0 errors, 64 warnings** |
| **`tests/` build** | **10 errors — does not compile** |

Semantic Kernel version: `1.75.0`. Target framework: `net10.0`. No Microsoft Agent Framework or `Microsoft.Extensions.AI` package is present.

---

## 4. Findings

### 4.1 The test suite does not compile — CRITICAL [VERIFIED]

```
tests/…/Stage2_Analyze/AnalyzeNetworkBatchCommandHandlerTests.cs(116,116): error CS0535:
  'FakeAnalyzer' does not implement 'INetworkBatchAnalyzer.AnalyzeAsync(Guid, IReadOnlyList<NetworkEvent>, string?, CancellationToken)'
tests/…/Stage2_Analyze/SemanticKernelNetworkBatchAnalyzerTests.cs(160,104): error CS0535:
  'StubAnomalySkill' does not implement 'INetworkAnomalySkill.InvokeAsync(string, string?, CancellationToken)'
  … (5 errors total in the solution build, 10 when the test project is built alone)
```

A `string? rawContext` parameter was added to `INetworkBatchAnalyzer` and the four `INetwork*Skill` interfaces. The test doubles were never updated. `src/` builds clean; `tests/` has not compiled since that change.

The last commit touching `tests/` is `0c7410c` (2026-05-06), *"feat: enhance AI infrastructure with energy observations and update dependencies"* — the very commit that introduced `rawContext`.

**Why this is finding #1:** the directive requires each migration PR to be *"small, compilable, testable, reversible."* Testable is currently impossible. Restoring the build is a precondition for Phase 4, not a task within it.

---

### 4.2 Dependency-rule violations [VERIFIED]

The directive forbids `Domain → AI`, `AI → Controllers`, `AI → Repositories`. The actual graph violates the spirit of all three, plus one the directive did not anticipate.

| # | Violation | Evidence |
| --- | --- | --- |
| 1 | **`Ai.Domain` depends on infrastructure** — the `Pgvector` package is a project reference; the aggregate imports it. | [`Modules.Ai.Domain.csproj:3`](../src/Modules/Ai/Modules.Ai.Domain/Modules.Ai.Domain.csproj#L3); [`KnowledgeChunk.cs:1`](../src/Modules/Ai/Modules.Ai.Domain/Knowledge/KnowledgeChunk.cs#L1) `using Pgvector;` |
| 2 | **`Ai.Infrastructure` reaches into another module's Application layer**, not its public `.Api`. 11 `using` sites. | `Modules.Ai.Infrastructure.csproj` → `Modules.Network.Application.csproj`; `using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;` ×11 |
| 3 | **`Web.Api` reaches into `Ai.Infrastructure` internals.** | [`GeoEnricher.cs:2`](../src/Web.Api/Endpoints/Geo/GeoEnricher.cs#L2) `using Modules.Ai.Infrastructure.Mcp.Osm;` — also `SeedExtensions.cs`, `MigrationExtensions.cs` |
| 4 | **`Web.Api` injects an AI *domain repository* directly into an endpoint.** | [`Documents.cs:141-143`](../src/Web.Api/Endpoints/Documents/Documents.cs#L141-L143) — `IManagedDocumentRepository documents` in the `GET /documents/{id}/download` lambda |
| 5 | **AI contracts live inside a business module.** `AiAnalysisResult`, `DetectedAnomaly`, `AnomalyType`, `TopologyDelta`, `INetworkBatchAnalyzer` — 188 LOC of AI vocabulary — sit in `Modules.Network.Application`. | `src/Modules/Network/Modules.Network.Application/Ingestion/Stage2_Analyze/` |
| 6 | **The Application layer is named after the vendor.** `ICopilotOrchestrator` lives in namespace `Modules.Ai.Application.SemanticKernel`. | [`ICopilotOrchestrator.cs:3`](../src/Modules/Ai/Modules.Ai.Application/SemanticKernel/ICopilotOrchestrator.cs#L3) |

Note the domain layers of the *business* modules (`Network`, `Alerts`, `Energy`, `Analytics`, `Identity`) are **clean** — zero AI or SK imports. The Domain-First principle holds everywhere except inside the AI module's own domain.

---

### 4.3 A runtime circular dependency — CRITICAL [VERIFIED]

The compiler cannot see this because it closes through MediatR's `ISender` and through a DI-resolved interface. It fires on **every document upload of a `.csv/.json/.jsonl/.xlsx/.txt/.log` file**.

```
DocumentIngestionService                       (Modules.Ai.Infrastructure)
  │  sender.Send(new ProcessNetworkLogCommand(…))            ← DocumentIngestionService.cs:144
  ▼
ProcessNetworkLogCommandHandler                (Modules.Network.Application)
  │  sender.Send(new AnalyzeNetworkBatchCommand(…))          ← ProcessNetworkLogCommandHandler.cs:108
  ▼
AnalyzeNetworkBatchCommandHandler              (Modules.Network.Application)
  │  analyzer.AnalyzeAsync(…)   [INetworkBatchAnalyzer]      ← AnalyzeNetworkBatchCommandHandler.cs:39
  ▼
SemanticKernelNetworkBatchAnalyzer             (Modules.Ai.Infrastructure)  ◀── cycle closed
```

`Ai.Infrastructure → Network.Application → Ai.Infrastructure`.

This is the single strongest argument for the event-driven redesign the directive asks for. The cycle exists *because* the upload path is synchronous: AI code has to call Network code to make the triggers fire, and Network code has to call back into AI code to do the analysis. Publishing `DocumentUploaded` and letting Network subscribe breaks it structurally, not by convention.

---

### 4.4 The document upload pathology [VERIFIED]

The HTTP endpoint is fine — it streams, it does not buffer, it is a thin MediatR shim ([`Documents.cs:74-75`](../src/Web.Api/Endpoints/Documents/Documents.cs#L74-L75)). Everything below it is the problem.

`POST /documents/upload` does not return until all of this completes:

| Step | What runs | Cost |
| --- | --- | --- |
| 1 | Save file to disk, insert `ManagedDocument`, `SaveChanges` | 1 write |
| 2 | `MarkInProgress`, `SaveChanges` | 1 write |
| 3 | Extract text (PdfPig for PDFs) | CPU |
| 4 | **`AiDocumentValidator`** — an LLM call to ask "is this document relevant?" | **1 LLM call** |
| 5 | Chunk (600 chars, 80 overlap) + **embed all chunks in one request** | **1 embeddings call** |
| 6 | Upsert document + chunks, `SaveChanges` ×2 | 2 writes |
| 7 | **Network pipeline**: parse → analyze → decide → persist → project | **4 LLM calls** (up to 8 with retry) |
| 8 | 7 further `SaveChanges` across stage transitions | 7 writes |

**Totals per upload, in Azure mode: 5–9 LLM round trips, 1 embeddings round trip, ≥11 `SaveChangesAsync` across two DbContexts (`AiDbContext`, `NetworkDbContext`) with no shared transaction.** All inside the request. [VERIFIED by reading; counts derived from the call chain, not measured at runtime.]

The code says so out loud, twice:

> `// Synchronously ingest so the demo flow is "upload → searchable" without needing a background worker.`
> — [`UploadDocumentCommandHandler.cs:47-48`](../src/Modules/Ai/Modules.Ai.Application/Documents/UploadDocument/UploadDocumentCommandHandler.cs#L47-L48)

> `// Runs synchronously so the demo flow is "upload → all triggers fired" without needing a background worker.`
> — [`DocumentIngestionService.cs:118-119`](../src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Ingestion/DocumentIngestionService.cs#L118-L119)

This is not accidental complexity. It was a deliberate hackathon trade that is now load-bearing. There is **no retry, no resumability, no cancellation, no progress reporting** — a failure at step 7 leaves a document that is RAG-indexed but has fired none of its downstream triggers, and the only recovery is the manual `POST /documents/{id}/reindex` endpoint.

#### 4.4.1 The unbounded embeddings request — a latent hard failure

[`RagIndexer.cs:35-36`](../src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Indexing/RagIndexer.cs#L35-L36) sends **every chunk of a document in a single `GenerateBatchAsync` call**. [`AzureOpenAiEmbeddingGenerator.cs:42`](../src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Embeddings/AzureOpenAiEmbeddingGenerator.cs#L42) forwards that list to the API with **no partitioning**. `RecursiveTextChunker` has **no cap on chunk count**. [all VERIFIED]

`DocumentsOptions.MaxUploadBytes = 25 MB`, `ChunkSize = 600`, `ChunkOverlap = 80` → stride 520 → a 25 MB text document yields roughly **48,000 chunks in one request**.

Azure OpenAI's embeddings endpoint accepts a maximum of 2,048 inputs per request. **[INFERRED — vendor limit, not executed here.]** Any document whose extracted text exceeds roughly 1 MB should therefore fail with HTTP 400, be caught by `DocumentIngestionService`'s handler, and be marked `Failed`.

This has almost certainly never been observed, because — see 4.6 — the shipped default never calls Azure at all. `HashingEmbeddingGenerator` is in-process and has no limit. The bug is invisible in every environment the team has run.

---

### 4.5 Three tool surfaces over the same data [VERIFIED]

| Surface | Reaches data via | Visible to the LLM? |
| --- | --- | --- |
| **SK Skills** — `DiagnosticsSkill`, `OutageSkill`, `EnergySkill`, `OsmSkill`, `KnowledgeSkill`, `RecommendationSkill` | module `.Api` interfaces directly | ✅ yes, 23 functions |
| **`Tools/` MediatR queries** — surfaced by `InternalToolsSkill` | `ISender` → query handler → repository | ✅ yes, same kernel |
| **MCP Plugins** — `NetworkMcpPlugin`, `AlertsMcpPlugin`, `EnergyMcpPlugin`, `OsmMcpPlugin`, `McpInvoker`, `McpPluginRegistry` | module `.Api` interfaces directly | ❌ **no** |

The MCP registry is a complete, functioning, DI-registered tool layer that the model **cannot call**. Its only consumer is a hand-written HTTP endpoint, [`Mcp.cs:44`](../src/Web.Api/Endpoints/Mcp/Mcp.cs#L44), where a controller injects `IMcpInvoker` and orchestrates AI directly — itself a violation of the Thin API principle.

Meanwhile the two surfaces the model *can* see overlap with each other:

- `get_network_metrics` (`InternalToolsSkill` → MediatR) **vs** `get_region_metrics` (`DiagnosticsSkill` → `INetworkApi`) — same per-tower signal/load/status for a region.
- `get_outages` (`InternalToolsSkill`) **vs** `get_active_outages` + `get_outages_in_region` (`OutageSkill`) — same incidents.

The model is handed two ways to answer the same question and must guess. Every wrong guess is an extra round trip.

**`InternalToolsSkill` is the correct pattern.** [`InternalToolsSkill.cs:21`](../src/Modules/Ai/Modules.Ai.Infrastructure/SemanticKernel/Skills/InternalToolsSkill.cs#L21) — `InternalToolsSkill(ISender sender)`, every `[KernelFunction]` a thin shim over a MediatR query, so tool calls ride the same logging/validation/exception pipeline as any use case. It is exactly the "Tools wrap application services, never repositories" rule the directive asks for. **Phase 2 should generalize this one and delete the other two.**

**A consequence worth stating explicitly.** `InternalToolsSkill` is registered only inside the `useAzure` branch ([`DependencyInjection.cs:134`](../src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L134)), and it is the *sole* sender of the four `Tools/` MediatR queries. So in the shipped default configuration (§4.6), those four query handlers are registered by MediatR assembly scanning and **nothing ever sends them**. Combined with `MockCopilotOrchestrator` calling `INetworkApi`/`IAlertsApi`/`IRagRetriever` directly rather than through any tool: **in the default configuration, the number of tools the LLM can call is zero.** The entire tool architecture — all three surfaces — is inert unless Azure credentials are supplied.

---

### 4.6 The shipped default disables every SK path — CRITICAL [VERIFIED]

[`appsettings.json`](../src/Web.Api/appsettings.json) ships:

```json
"Ai": { "Provider": "Mock", "AzureOpenAi": { "Endpoint": "", "ApiKey": "", "Deployment": "gpt-4o-mini" } }
```

`AiOptions.Provider` also defaults to `"Mock"` in code ([`AiOptions.cs:8`](../src/Modules/Ai/Modules.Ai.Infrastructure/SemanticKernel/AiOptions.cs#L8)). The `useAzure` gate at [`DependencyInjection.cs:112-115`](../src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L112-L115) requires a non-empty endpoint **and** key.

When it is false — the default — none of these are ever registered:

- `SemanticKernelOrchestrator` → replaced by `MockCopilotOrchestrator`
- `SemanticKernelNetworkBatchAnalyzer` → replaced by `HeuristicNetworkBatchAnalyzer`
- `AiDocumentValidator` → replaced by `MockDocumentValidator`
- All four `SemanticKernelNetwork*Skill` classes
- `AiAnalysisResultValidator` — and therefore its three nested child validators
- The `Kernel` itself

Additionally, `EmbeddingDeployment` **does not appear in `appsettings.json` at all**, and the embedding gate requires it ([`DependencyInjection.cs:206-210`](../src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L206-L210)). So even *with* Azure chat credentials configured, RAG silently falls back to `HashingEmbeddingGenerator` — **token-overlap hashing, not semantic embeddings**. The pgvector column is real; what goes into it, by default, is a hash.

Two consequences for the migration:

1. **The SK code being replaced is the least-exercised code in the repo.** Behavior parity cannot be assumed from "the demo works."
2. **The two providers behave differently, not just slower/faster.** `MockCopilotOrchestrator` performs deterministic RAG retrieval itself ([`MockCopilotOrchestrator.cs:35`](../src/Modules/Ai/Modules.Ai.Infrastructure/SemanticKernel/MockCopilotOrchestrator.cs#L35), `topK: 4`). `SemanticKernelOrchestrator` performs **no retrieval** — it merely hopes the model chooses to call `search_knowledge`. The offline path and the production path are different algorithms behind one interface.

---

### 4.7 Memory is write-only [VERIFIED]

`AskCopilotCommand` carries a `ConversationId`. `AskCopilotCommandHandler` resolves the full `Conversation` aggregate with its `Message` list ([`AskCopilotCommandHandler.cs:44`](../src/Modules/Ai/Modules.Ai.Application/Copilot/AskCopilot/AskCopilotCommandHandler.cs#L44)).

It then calls:

```csharp
answer = await orchestrator.AskAsync(request.Query, request.ActorRole, cancellationToken);
```
— [`AskCopilotCommandHandler.cs:50`](../src/Modules/Ai/Modules.Ai.Application/Copilot/AskCopilot/AskCopilotCommandHandler.cs#L50)

The conversation is discarded. Downstream, `SemanticKernelOrchestrator` builds `ChatHistory history = new(systemPrompt); history.AddUserMessage(query);` ([`:124-125`](../src/Modules/Ai/Modules.Ai.Infrastructure/SemanticKernel/SemanticKernelOrchestrator.cs#L124-L125)) — a fresh history every turn.

**Multi-turn conversation does not work.** History is persisted, rendered in the UI, and never sent to the model. `ICopilotOrchestrator.AskAsync(string, string, CancellationToken)` has no parameter that could carry it. This is a contract change, and it is the natural seam for MAF `AgentThread` / sessions.

Separately, the handler contains a documented workaround for an unexplained EF Core `DbUpdateConcurrencyException`, detaching the aggregate before `SaveChanges` and writing scalars via a raw `ExecuteUpdate` ([`:128-152`](../src/Modules/Ai/Modules.Ai.Application/Copilot/AskCopilot/AskCopilotCommandHandler.cs#L128-L152)). The comment states the root cause was never found. That is an unresolved defect sitting directly on the path Phase 2 will rewrite.

---

### 4.8 Dead code — CONFIRMED [VERIFIED, by reference count]

| Item | Refs | Status |
| --- | --- | --- |
| `SemanticKernelOrchestrator.MockAnswer` (17-line canned answer) | 1 — its own declaration | **Dead.** Superseded by `MockCopilotOrchestrator`, never deleted. |
| `OneDriveDocumentStorageProvider` | 2 — own file + a **commented-out** DI line ([`DependencyInjection.cs:324`](../src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L324)) | **Dead.** A real `HttpClient` implementation, unlike the `Placeholder*` stubs, but never registered. |
| `ExternalApiPlugin` (abstract, 27 LOC) | 2 — own file only | **Dead.** Zero subclasses. Speculative generalization. |
| `ExternalMcpServerPlugin` (abstract, 28 LOC) | 2 — own file only | **Dead.** Zero subclasses. Speculative generalization. |
| `McpPluginKind.ExternalApi`, `McpPluginKind.ExternalMcpServer` | set only by the two dead adapters above | **Dead enum members.** Only `Internal` is ever used. |
| `IChatLogRepository.CountAsync` / `ChatLogRepository.CountAsync` | 2 — interface declaration + implementation | **Dead method.** Zero callers. |
| `PromptExecutionSettings` singleton (`Temperature = 0.7`, `json_object`) registered into the kernel's service container at [`DependencyInjection.cs:146-150`](../src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L146-L150) | Resolved by nothing | **Dead config.** Both live call paths pass their own settings explicitly. |
| The entire MCP plugin subsystem, *as an AI capability* | Registered, invoked only from an HTTP endpoint | **Dead to the LLM.** See 4.5. |

**Redundant, not dead:** `ChatLog` + `ChatLogRepository` write a flat audit row on every copilot turn, duplicating `Conversation`/`Message`. The code comment calls it *"kept for backward compat with the audit page."* It is a second source of truth for the same event — and **write-only**: the repository exposes only `AddAsync` and the dead `CountAsync`, so no code in this repository ever reads a `ChatLog` row back.

**Dormant, not dead:** the whole `if (useAzure)` block ([`DependencyInjection.cs:122-176`](../src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L122-L176)) — every SK skill, the four Stage-2 skills, `AiAnalysisResultValidator`, the `Kernel` itself. Never registered in the shipped configuration (§4.6). This code is not deletable, but it is also not exercised. Phase 5 must distinguish *dead* (delete) from *dormant* (migrate, then verify).

---

### 4.9 Redundant abstractions and configuration drift [VERIFIED]

**Three `PromptExecutionSettings`, three temperatures, no single owner:**

| Where | Temperature | Format |
| --- | --- | --- |
| `DependencyInjection.cs:146-150` (registered, unused) | 0.7 | `json_object` |
| `SemanticKernelOrchestrator.cs:132-136` (copilot) | 0.2 | — |
| `KernelJsonInvoker.cs:15-19` (batch) | **0.9** | `json_object` |

`Temperature = 0.9` is used for **structured JSON extraction of network anomalies** — the task least tolerant of sampling variance in the system. That is very likely backwards.

**`KernelJsonInvoker`** ([`:29`](../src/Modules/Ai/Modules.Ai.Infrastructure/Pipeline/Skills/KernelJsonInvoker.cs#L29)) calls `KernelFunctionFactory.CreateFromPrompt(prompt)` on **every invocation**, rebuilding the prompt function from a constant string each time rather than caching it. It also catches `Exception` and folds it into `Result.Failure` ([`:56-59`](../src/Modules/Ai/Modules.Ai.Infrastructure/Pipeline/Skills/KernelJsonInvoker.cs#L56-L59)) — swallowing `OperationCanceledException` along with everything else, so cancellation is reported as a model failure.

**Speculative single-implementation interfaces** (one impl, one consumer): `IChunker`, `IDocumentSyncService`, `IDocumentTextExtractor`, `IRagIndexer`. Harmless individually; collectively they are the "unnecessary abstraction" the directive calls out. They should survive Phase 2 only where a second implementation is actually planned.

**Business logic inside AI infrastructure:** [`RecommendationSkill.cs`](../src/Modules/Ai/Modules.Ai.Infrastructure/SemanticKernel/Skills/RecommendationSkill.cs) has **zero constructor dependencies**. It is a hardcoded `switch` mapping root-cause class → three NOC actions. Its own comment concedes *"Real production code would back this with a YAML runbook store."* This is MTN operational policy living in the AI vendor adapter. Worse, the same playbook text is duplicated in `MockCopilotOrchestrator.SuggestActionsFor` ([`:137-141`](../src/Modules/Ai/Modules.Ai.Infrastructure/SemanticKernel/MockCopilotOrchestrator.cs#L137-L141)).

---

### 4.10 Performance bottlenecks [VERIFIED unless noted]

1. **Repeated embedding generation on a 5-minute loop.** `EnergyKnowledgeIndexer.IndexAsync` unconditionally rebuilds a knowledge document for **every site plus up to 200 anomalies**, then re-chunks and re-embeds all of them ([`EnergyKnowledgeIndexer.cs:31-52`](../src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Indexing/EnergyKnowledgeIndexer.cs#L31-L52)). No content hash. No dirty check. `EnergyKnowledgeIndexerService` runs it every 5 minutes forever ([`:21`](../src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Indexing/EnergyKnowledgeIndexerService.cs#L21)) — **~288 full re-embedding sweeps per day** of almost entirely unchanged text.

2. **No embedding cache anywhere.** `ICacheService` exists and is used — but only by `CachedOsmClient` and `SiteGeoLookup`. Every RAG query re-embeds the query string from scratch. Ask the same question twice, pay twice.

3. **Stage 2 runs four independent LLM calls sequentially.** [`SemanticKernelNetworkBatchAnalyzer.cs:64-85`](../src/Modules/Ai/Modules.Ai.Infrastructure/Pipeline/SemanticKernelNetworkBatchAnalyzer.cs#L64-L85) — anomaly, then optimization, then topology, then energy. Each `await`s the previous. They share no data. Each re-sends the **same** `eventsJson` and `rawContext`, so the input token cost is paid 4×. With `MaxAttempts = 2`, worst case is 8 sequential calls.

4. **`RagIndexer.IndexBatchAsync` calls `SaveChangesAsync` once per document** ([`:74-86`](../src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Indexing/RagIndexer.cs#L74-L86)) rather than once per batch.

5. **Every request pays 5 MediatR pipeline behaviors**, including on internal tool dispatches from `InternalToolsSkill` — so a single copilot turn that calls 3 tools runs the behavior stack 4 times.

6. **The copilot re-sends the full ~86-line system prompt plus all accumulated tool output on every auto-invoke round trip** (single growing `ChatHistory`). SK's default `MaximumAutoInvokeAttempts` bounds this at 128 model round trips. **[INFERRED — SK default, not executed.]**

7. **Five hosted services do work at boot**, plus synchronous seeding on the startup thread. [`SeedExtensions.cs:43-53`](../src/Web.Api/Extensions/SeedExtensions.cs#L43-L53) awaits `KnowledgeCorpusSeeder.SeedAsync` → `EnergyKnowledgeIndexer.IndexAsync` → `LocalDocumentSeeder.SeedAsync` before the app serves traffic — i.e. **RAG indexing and embedding run during startup**. `GeoCacheWarmer` then walks every tower calling OSM with a 90 s per-site timeout. `FileMcpClient` spawns `npx @modelcontextprotocol/server-filesystem` — a **Node.js subprocess dependency inside a .NET service** — on every boot, including Mock mode where its tools are never attached to any kernel.

8. **The boot seed runs twice.** `LocalDocumentSeeder.SeedAsync()` is invoked from [`SeedExtensions.cs:53`](../src/Web.Api/Extensions/SeedExtensions.cs#L53) *and* from [`LocalDocumentSeederService.cs:21`](../src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Seed/LocalDocumentSeederService.cs#L21) (a `BackgroundService` with no startup delay). Likewise `EnergyKnowledgeIndexer.IndexAsync()` runs at [`SeedExtensions.cs:50`](../src/Web.Api/Extensions/SeedExtensions.cs#L50) and again 45 s later from `EnergyKnowledgeIndexerService`. Two components own the same startup responsibility and neither knows about the other.

9. **`DocumentSyncService.SyncAllAsync` is fire-and-forget** — `_ = Task.Run(…, CancellationToken.None)` ([`:12-17`](../src/Modules/Ai/Modules.Ai.Infrastructure/Rag/Ingestion/DocumentSyncService.cs#L12-L17)). It creates its own DI scope, ignores the caller's cancellation token, returns immediately, and drops any exception on the floor. The `async Task` method contains no `await`. This is the only fire-and-forget in the codebase and it is in the AI module.

The irony is worth naming: the one place the code *does* run AI work off the request thread is the one place it should not — an unobserved, uncancellable, unretryable `Task.Run`. Meanwhile the upload path, which genuinely needs backgrounding, blocks.

---

### 4.11 The god-prompt and the god-kernel [VERIFIED]

One system prompt, **86 lines** ([`SemanticKernelOrchestrator.cs:37-122`](../src/Modules/Ai/Modules.Ai.Infrastructure/SemanticKernel/SemanticKernelOrchestrator.cs#L37-L122)), describing 7 plugins, encoding tool-selection policy in prose ("RULE: Call search_knowledge for ANY 'why'…"), response formatting, and markdown constraints for a specific frontend renderer.

One `Kernel`, **23 auto-callable functions** plus MCP filesystem tools, built per-request scope ([`DependencyInjection.cs:138-163`](../src/Modules/Ai/Modules.Ai.Infrastructure/DependencyInjection.cs#L138-L163)).

This is the single-agent architecture the directive is replacing. It is also brittle in a specific way worth recording: the orchestrator post-processes the model's prose with two regexes to strip malformed markdown bold ([`SanitizeMarkdown`, `:216-222`](../src/Modules/Ai/Modules.Ai.Infrastructure/SemanticKernel/SemanticKernelOrchestrator.cs#L216-L222)) and recovers a confidence score by **string-scanning for `%`** ([`ExtractConfidence`, `:224-245`](../src/Modules/Ai/Modules.Ai.Infrastructure/SemanticKernel/SemanticKernelOrchestrator.cs#L224-L245)), defaulting to `0.85` when parsing fails. Structured output is being recovered from prose. MAF's typed responses eliminate this whole class of code.

**Suspected defect, flagged not asserted:** the skill trace shown in the UI is reconstructed by scanning `ChatHistory` for `msg.Role == AuthorRole.Tool && msg.AuthorName is { } fn` ([`:146-155`](../src/Modules/Ai/Modules.Ai.Infrastructure/SemanticKernel/SemanticKernelOrchestrator.cs#L146-L155)). If SK does not populate `AuthorName` on tool messages, `trace` is always empty and the code falls through to a **synthetic** `("IntentParser", "parseQuery")` entry at `:159`. The "animated agent panel" would then be showing fabricated telemetry on every request. **I did not verify SK 1.75's `AuthorName` behavior — this needs a runtime check, not a code read.**

---

## 5. What is already right

This matters as much as the defect list, because Phase 2 should preserve it rather than rebuild it.

- **The domain layer is clean.** Every business module's `Domain` project references only `SharedKernel`. Zero AI imports. The Domain-First principle already holds.
- **Module boundaries are real.** Cross-module reads go through `.Api` interfaces (`INetworkApi`, `IAlertsApi`, `IEnergyApi`, `IAnalyticsApi`). The two violations in §4.2 are the exceptions, not the rule.
- **The API layer is thin.** 34 of 44 route handlers are pure `sender.Send(…).Match(…)`. Uploads stream (`file.OpenReadStream()`), no `byte[]` materialization. No `async void`, no `.Wait()`, no sync-over-async anywhere in `Web.Api`.
- **An event bus already exists.** `IEventBus` → `InMemoryMessageQueue` (Channel) → `IntegrationEventProcessorJob` (`BackgroundService`) → MediatR `IPublisher`. Stage 5 of the network pipeline already uses it correctly. **This is the foundation for the event-driven upload redesign** — it needs durability (an outbox) for the retry/resumability the directive requires, but the seam is there.
- **`InternalToolsSkill` is the target tool pattern**, already implemented.
- **`ManagedDocument` already models the state machine** the async pipeline needs: `MarkInProgress` / `MarkIndexed` / `MarkFailed` / `MarkRejected`, plus a `Version` for idempotency. The domain is ready for async ingestion; only the plumbing is synchronous.
- **The network pipeline is content-hash idempotent** (`Fingerprints.ContentHash`), with per-stage timings recorded on `IngestionRun`. Re-processing is already safe.

---

## 6. Migration risk register

| Risk | Severity | Basis |
| --- | --- | --- |
| No compilable test suite | **High** | §4.1 |
| SK paths never exercised in default config; no behavioral baseline to preserve | **High** | §4.6 |
| Mock and Azure orchestrators use different retrieval algorithms — "parity" is ill-defined | **High** | §4.6 |
| Unresolved EF concurrency defect sits on the code Phase 2 rewrites | Medium | §4.7 |
| `ICopilotOrchestrator` contract cannot carry conversation state; changing it touches API + persistence | Medium | §4.7 |
| The AI↔Network cycle means AI and Network must be migrated together, not independently | Medium | §4.3 |
| `Pgvector` in `Ai.Domain` means fixing the domain requires an EF mapping change + migration | Low | §4.2 |
| Node.js (`npx`) runtime dependency via `FileMcpClient` | Low | §4.10 |

---

## 7. What I did not verify

Stated explicitly so nothing here is mistaken for observation:

- **No runtime execution against Azure OpenAI.** No credentials in the repo. All latency, token-cost, and HTTP-failure claims are reasoning from code plus vendor limits.
- **The 2,048-input embeddings cap** (§4.4.1) is Azure's documented limit, not something I triggered.
- **SK's `MaximumAutoInvokeAttempts = 128`** (§4.10) is the framework default, not measured.
- **The `AuthorName` skill-trace defect** (§4.11) is a code-read suspicion. It requires a runtime check.
- **I did not run the test suite** — it does not compile.
- **I did not audit** `Identity`, `Analytics`, or the `frontend/` directory beyond their AI seams.

---

## 8. Recommended Phase 2 scope

Not a design — a scope proposal, for approval before I produce the design.

**Phase 2 should specify:**

1. A single `/AI` bounded context, with `Ai.Domain` stripped of `Pgvector` and the vector concern pushed to `Ai.Infrastructure`.
2. **One** tool surface, generalizing the `InternalToolsSkill` pattern: MAF Tools wrapping MediatR commands/queries. The SK Skills and the MCP plugin registry both collapse into it. `RecommendationSkill`'s runbook moves to the domain or a runbook store — it is business policy, not AI.
3. Agent decomposition of the 86-line god-prompt. The overlapping tools (§4.5) tell you where the seams are: Topology, Incident, RootCause, Recommendation, Knowledge, Document.
4. `DocumentUploaded` as a durable integration event, with the existing `IEventBus` extended by an outbox. The upload endpoint returns after step 1 of §4.4. Everything from step 3 onward becomes a MAF Workflow with retry, resumability, cancellation, and progress reporting against the `ManagedDocument` state machine that already exists.
5. The AI↔Network cycle broken by inversion: Network subscribes to `DocumentUploaded`; AI never dispatches `ProcessNetworkLogCommand`. `INetworkBatchAnalyzer` and its contracts move out of `Network.Application`.
6. A memory strategy that separates Conversation (MAF `AgentThread`), Knowledge (pgvector), Operational (module `.Api` reads), and Workflow State — with `ICopilotOrchestrator` replaced by a contract that can actually carry a thread.

**Two things should happen before Phase 2 design work begins, and neither is a rewrite:**

- **Fix the test project** (5 test doubles, mechanical). Without it, no migration PR can satisfy "compilable, testable, reversible."
- **Decide the parity baseline.** Given §4.6, "preserve existing behavior" currently means "preserve `MockCopilotOrchestrator`'s behavior," because that is what runs. If the intent is to preserve the *Azure* behavior, someone must first run it and capture what it does.

---

## 9. Approval gate

Phase 1 is complete. No code has been changed.

Awaiting approval to proceed to **Phase 2 — Design the new architecture** (folder structure, module boundaries, dependency graph, agent/workflow/tool/memory architecture, document processing architecture, event flow, with the rationale for every decision).
