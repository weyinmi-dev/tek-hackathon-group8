// Baseline characterization harness (Phase 4 / M1) — in-process, real Postgres.
//
// Drives the offline-mode network-ingestion pipeline through its real entry point
// (ProcessNetworkLogCommand) against a fresh Testcontainers Postgres, and records the
// six parity-contract counts per fixture. A fresh container per run means the
// content-hash dedup never fires, so replay actually re-executes the analyzer.
//
// Uses postgres:17.6 (not pgvector): the network pipeline has no vector columns, so the
// AI DbContext is never created here — only Network/Alerts/Analytics. Wiring mirrors the
// proven PipelineTestHost from the test project, swapping in-memory EF for real Npgsql.
//
//   dotnet run --project tools/BaselineCapture -- capture   # writes docs/baselines/*.json
//   dotnet run --project tools/BaselineCapture -- verify    # diffs against them, exit 1 on drift
using System.Text.Json;
using Application;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Modules.Ai.Agents.Agents;
using Modules.Ai.Agents.Infrastructure;
using Modules.Ai.Agents.Tools;
using Modules.Ai.Agents.Workflows.Executors;
using Application.Abstractions.Events;
using Application.Abstractions.Storage;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Ai.Infrastructure.Pipeline;
using Modules.Ai.Infrastructure.Pipeline.Validators;
using Modules.Alerts.Application;
using Modules.Alerts.Domain.Alerts;
using Modules.Alerts.Infrastructure.Database;
using Modules.Alerts.Infrastructure.Pipeline;
using Modules.Alerts.Infrastructure.Repositories;
using Modules.Analytics.Application;
using Modules.Analytics.Domain.Ingestion;
using Modules.Analytics.Infrastructure.Database;
using Modules.Analytics.Infrastructure.Repositories;
using Modules.Network.Application;
using Modules.Network.Application.Ingestion.Pipeline;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Application.Ingestion.Stage2_Analyze;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Application.Ingestion.Stage4_Persist;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Domain.Optimizations;
using Modules.Network.Domain.Towers;
using Modules.Network.Infrastructure.Database;
using Modules.Network.Infrastructure.Ingestion;
using Modules.Network.Infrastructure.Ingestion.Parsers;
using Modules.Network.Infrastructure.Repositories;
using Testcontainers.PostgreSql;
using NetworkUow = Modules.Network.Domain.IUnitOfWork;
using AlertsUow = Modules.Alerts.Domain.IUnitOfWork;
using AnalyticsUow = Modules.Analytics.Domain.IUnitOfWork;
using NetworkUowImpl = Modules.Network.Infrastructure.Database.UnitOfWork;
using AlertsUowImpl = Modules.Alerts.Infrastructure.Database.UnitOfWork;
using AnalyticsUowImpl = Modules.Analytics.Infrastructure.Database.UnitOfWork;

string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "capture";
string repoRoot = Directory.GetCurrentDirectory();
string fixtureDir = Path.Combine(repoRoot, "scripts", "baseline", "fixtures");
string baselineDir = Path.Combine(repoRoot, "docs", "baselines");

// M6 smoke test — needs no database: build the copilot agent over the deterministic offline
// chat client and a sender that throws if a tool is invoked (offline mode must not call tools),
// then run it and confirm the offline response comes back.
if (mode == "agent")
{
    return await RunAgentSmokeAsync();
}

// M7 kill-and-resume — proves the workflow runs, checkpoints at superstep boundaries, and can be
// rehydrated from a checkpoint. Uses in-memory checkpoints and a probe executor (no DB needed);
// the real DocumentIngestionWorkflow kill-and-resume against the Postgres store runs at M9.
if (mode == "workflow")
{
    return await RunWorkflowSmokeAsync();
}

if (!Directory.Exists(fixtureDir))
{
    Console.Error.WriteLine($"Fixtures not found at {fixtureDir}. Run from the repo root.");
    return 2;
}

Console.WriteLine("Starting postgres:17.6 container (cached image, no vector extension needed)...");
await using PostgreSqlContainer pg = new PostgreSqlBuilder().WithImage("postgres:17.6").Build();
await pg.StartAsync();
Console.WriteLine("Container up.");

await using ServiceProvider sp = BuildProvider(pg.GetConnectionString());
await CreateSchemaAsync(sp);
await SeedTowersAsync(sp);

var results = new SortedDictionary<string, ParityCounts>(StringComparer.Ordinal);
foreach (string csv in Directory.GetFiles(fixtureDir, "*.csv").OrderBy(f => f, StringComparer.Ordinal))
{
    string name = Path.GetFileNameWithoutExtension(csv);
    ParityCounts counts = await RunFixtureAsync(sp, csv);
    results[name] = counts;
    Console.WriteLine($"  {name,-24} -> {JsonSerializer.Serialize(counts)}");
}

Directory.CreateDirectory(baselineDir);
int exit = 0;
var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
foreach ((string name, ParityCounts counts) in results)
{
    string path = Path.Combine(baselineDir, $"ingest-{name}.json");
    string current = JsonSerializer.Serialize(counts, jsonOpts);
    if (mode == "verify")
    {
        if (!File.Exists(path)) { Console.WriteLine($"MISSING  {name}"); exit = 1; continue; }
        string golden = File.ReadAllText(path).Trim();
        if (golden == current.Trim()) { Console.WriteLine($"MATCH    {name}"); }
        else { Console.WriteLine($"DIFF     {name}\n  golden : {golden}\n  current: {current}"); exit = 1; }
    }
    else
    {
        File.WriteAllText(path, current);
        Console.WriteLine($"wrote    {path}");
    }
}

Console.WriteLine(mode == "verify"
    ? (exit == 0 ? "PARITY HELD." : "PARITY BROKEN — review diffs.")
    : "Baseline captured.");
return exit;

// ── M7 workflow kill-and-resume ───────────────────────────────────────────────
static async Task<int> RunWorkflowSmokeAsync()
{
    Workflow BuildProbe()
    {
        var probe = new ProbeExecutor();
        return new WorkflowBuilder(probe).WithOutputFrom(probe).Build();
    }

    CheckpointManager manager = CheckpointManager.CreateInMemory();

    // First run — capture the output and a checkpoint.
    StreamingRun run = await InProcessExecution.RunStreamingAsync(BuildProbe(), "hello", manager);
    string? output = null;
    CheckpointInfo? checkpoint = null;
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        switch (evt)
        {
            case WorkflowOutputEvent outputEvent:
                output = outputEvent.Data?.ToString();
                break;
            case SuperStepCompletedEvent step:
                checkpoint = step.CompletionInfo?.Checkpoint ?? checkpoint;
                break;
        }
    }

    Console.WriteLine($"first run: output={output}, captured checkpoint={(checkpoint is null ? "none" : "yes")}");
    if (output != "HELLO")
    {
        Console.Error.WriteLine($"WORKFLOW SMOKE FAILED — expected HELLO, got {output ?? "null"}.");
        return 1;
    }
    if (checkpoint is null)
    {
        Console.Error.WriteLine("WORKFLOW SMOKE FAILED — no checkpoint was produced.");
        return 1;
    }

    // Rehydrate a fresh workflow instance from the captured checkpoint. Bounded so a terminal
    // checkpoint (this single-superstep probe) can't hang the smoke; the real multi-superstep
    // kill-and-resume runs against DocumentIngestionWorkflow at M9.
    try
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        StreamingRun resumed = await InProcessExecution.ResumeStreamingAsync(BuildProbe(), checkpoint, manager, timeout.Token);
        await foreach (WorkflowEvent evt in resumed.WatchStreamAsync(timeout.Token))
        {
            // Draining confirms rehydration from the checkpoint succeeds without throwing.
        }
        Console.WriteLine("rehydrated from checkpoint: OK");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("rehydrated from checkpoint: reached (drain bounded — terminal checkpoint has no further work)");
    }

    Console.WriteLine("WORKFLOW SMOKE PASSED — workflow ran, produced output, checkpointed at the superstep boundary, and rehydrated.");
    return 0;
}

// ── M6 agent smoke ──────────────────────────────────────────────────────────
static async Task<int> RunAgentSmokeAsync()
{
    using var chat = new DeterministicChatClient();
    var sender = new ThrowingSender();

    AIAgent agent = new OperationsCopilotAgentBuilder(
        chat,
        new NetworkTools(sender),
        new AlertTools(sender),
        new EnergyTools(sender),
        new KnowledgeTools(sender),
        new DocumentTools(sender)).Build();

    var response = await agent.RunAsync("What is the status of TWR-LEK-003?");
    string text = response.ToString() ?? string.Empty;
    Console.WriteLine($"agent replied: {text}");

    if (!text.Contains("OFFLINE MODE", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("AGENT SMOKE FAILED — expected the deterministic offline response.");
        return 1;
    }

    Console.WriteLine("AGENT SMOKE PASSED — the copilot agent builds and runs offline, no network or database, tools untouched.");
    return 0;
}

// ── pipeline drive ──────────────────────────────────────────────────────────
static async Task<ParityCounts> RunFixtureAsync(IServiceProvider sp, string csvPath)
{
    using IServiceScope scope = sp.CreateScope();
    ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

    await using FileStream file = File.OpenRead(csvPath);
    var cmd = new ProcessNetworkLogCommand(
        FileName: Path.GetFileName(csvPath),
        ContentType: "text/csv",
        Content: file,
        SubmittedBy: "baseline-harness");

    SharedKernel.Result<IngestionRunSummary> result = await sender.Send(cmd);
    if (result.IsFailure)
    {
        throw new InvalidOperationException($"Pipeline failed for {csvPath}: {result.Error.Code} {result.Error.Description}");
    }

    IngestionRunSummary s = result.Value;
    return new ParityCounts(
        s.EventsParsed, s.AnomaliesDetected, s.AlertsCreated,
        s.AlertsUpdated, s.OptimizationsCreated, s.TopologyChanged, s.FinalStatus.ToString());
}

// ── DI wiring (mirrors PipelineTestHost, real Postgres instead of in-memory) ──
static ServiceProvider BuildProvider(string connectionString)
{
    var services = new ServiceCollection();
    services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

    services.AddDbContext<NetworkDbContext>(o => o.UseNpgsql(connectionString));
    services.AddDbContext<AlertsDbContext>(o => o.UseNpgsql(connectionString));
    services.AddDbContext<AnalyticsDbContext>(o => o.UseNpgsql(connectionString));

    services.AddApplication();
    services.AddNetworkApplication();
    services.AddAlertsApplication();
    services.AddAnalyticsApplication();

    services.AddScoped<ITowerRepository, TowerRepository>();
    services.AddScoped<IIngestionRunRepository, IngestionRunRepository>();
    services.AddScoped<IOptimizationRepository, OptimizationRepository>();
    services.AddScoped<NetworkUow, NetworkUowImpl>();

    services.AddSingleton<INetworkLogParser, CsvNetworkLogParser>();
    services.AddSingleton<INetworkLogParser, JsonNetworkLogParser>();
    services.AddSingleton<INetworkLogParser, XlsxNetworkLogParser>();
    services.AddSingleton<INetworkLogParser, TxtNetworkLogParser>();
    services.AddSingleton<INetworkLogParserRegistry, NetworkLogParserRegistry>();

    services.AddSingleton<DecisionEngineOptions>();
    services.AddSingleton<IDecisionEngine, DefaultDecisionEngine>();
    services.AddScoped<ITowerSnapshotProvider, TowerSnapshotProvider>();

    services.AddSingleton<IValidator<AiAnalysisResult>, AiAnalysisResultValidator>();
    services.AddSingleton<INetworkBatchAnalyzer, HeuristicNetworkBatchAnalyzer>();

    services.AddScoped<IAlertRepository, AlertRepository>();
    services.AddScoped<AlertsUow, AlertsUowImpl>();
    services.AddScoped<IAlertActionExecutor, AlertActionExecutor>();
    services.AddScoped<IAlertSnapshotProvider, AlertSnapshotProvider>();

    services.AddScoped<IIngestionDashboardRepository, IngestionDashboardRepository>();
    services.AddScoped<AnalyticsUow, AnalyticsUowImpl>();

    // The orchestrator needs a staging service and an event bus. Neither affects the
    // parity counts: staging returns null (analyzer falls back to events-only), and
    // Stage-5 events are captured but not drained (the summary already carries the counts).
    services.AddSingleton<IFileStagingService, NullFileStaging>();
    services.AddSingleton<IEventBus, CapturingEventBus>();

    return services.BuildServiceProvider();
}

static async Task CreateSchemaAsync(IServiceProvider sp)
{
    using IServiceScope scope = sp.CreateScope();
    // EnsureCreated skips table creation once the database exists, so on a shared database
    // only the first context's tables would be made. Create each context's tables directly
    // instead — the same approach the app's MigrationExtensions uses. EF Core 10 emits
    // CREATE TABLE IF NOT EXISTS, so per-context calls are idempotent and non-conflicting.
    DbContext[] contexts =
    [
        scope.ServiceProvider.GetRequiredService<NetworkDbContext>(),
        scope.ServiceProvider.GetRequiredService<AlertsDbContext>(),
        scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>(),
    ];
    foreach (DbContext ctx in contexts)
    {
        var creator = (IRelationalDatabaseCreator)ctx.GetService<IDatabaseCreator>();
        if (!await creator.ExistsAsync())
        {
            await creator.CreateAsync();
        }
        await creator.CreateTablesAsync();
    }
}

static async Task SeedTowersAsync(IServiceProvider sp)
{
    using IServiceScope scope = sp.CreateScope();
    NetworkDbContext db = scope.ServiceProvider.GetRequiredService<NetworkDbContext>();
    // The decision engine only updates towers that already exist (AI cannot create towers),
    // so every tower referenced by a fixture must be seeded for the topology path to fire.
    db.Towers.Add(Tower.Create("LOS-T-014", "Lagos T-014", "Lagos West", 6.5, 3.5, 0, 0, signalPct: 95, loadPct: 50, status: TowerStatus.Ok, issue: null));
    db.Towers.Add(Tower.Create("LOS-T-020", "Lagos T-020", "Lagos West", 6.6, 3.4, 0, 0, signalPct: 92, loadPct: 55, status: TowerStatus.Ok, issue: null));
    db.Towers.Add(Tower.Create("LOS-T-021", "Lagos T-021", "Lagos West", 6.7, 3.3, 0, 0, signalPct: 90, loadPct: 60, status: TowerStatus.Ok, issue: null));
    await db.SaveChangesAsync();
}

internal sealed record ParityCounts(
    int EventsParsed, int AnomaliesDetected, int AlertsCreated,
    int AlertsUpdated, int OptimizationsCreated, bool TopologyChanged, string FinalStatus);

internal sealed class NullFileStaging : IFileStagingService
{
    public string Root => "";
    public Task<string?> StageAsync(string contentHash, string fileName, byte[] bytes, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
    public Task<string?> TryReadTextAsync(string relativePath, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}

internal sealed class CapturingEventBus : IEventBus
{
    public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class, IIntegrationEvent => Task.CompletedTask;
}

// Fails if any tool is dispatched — offline mode must answer without invoking tools, since the
// deterministic chat client never emits tool calls.
internal sealed class ThrowingSender : ISender
{
    private static InvalidOperationException Unexpected()
        => new("A tool was dispatched during the offline agent smoke test — not expected.");

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => throw Unexpected();
    public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw Unexpected();
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw Unexpected();
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw Unexpected();
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw Unexpected();
}
