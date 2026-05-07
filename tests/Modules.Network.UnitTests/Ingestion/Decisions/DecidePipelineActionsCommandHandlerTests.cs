using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Domain.Ingestion;
using SharedKernel;
using Xunit;
using static Modules.Network.UnitTests.Ingestion.Decisions.DecisionFixtures;

namespace Modules.Network.UnitTests.Ingestion.Decisions;

public sealed class DecidePipelineActionsCommandHandlerTests
{
    [Fact]
    public async Task Handle_RunNotFound_ReturnsNotFound()
    {
        DecidePipelineActionsCommandHandler handler = NewHandler();

        var command = new DecidePipelineActionsCommand(Guid.NewGuid(), AiResult());

        Result<IReadOnlyList<PipelineAction>> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.RunNotFound");
    }

    [Fact]
    public async Task Handle_RunInWrongStage_ReturnsConflict()
    {
        IngestionRun run = NewRun();
        // run is in Pending; orchestrator should have transitioned to Deciding before calling.

        DecidePipelineActionsCommandHandler handler = NewHandler(run: run);

        Result<IReadOnlyList<PipelineAction>> result = await handler.Handle(
            new DecidePipelineActionsCommand(run.Id, AiResult()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.WrongStage");
        result.Error.Description.Should().Contain("Pending");
    }

    [Fact]
    public async Task Handle_RunInDeciding_DelegatesToEngineAndReturnsActions()
    {
        IngestionRun run = NewRun();
        WalkToDeciding(run);

        DetectedAnomaly anomaly = Anomaly();
        AiAnalysisResult ai = AiResult(anomalies: [anomaly]);

        DecidePipelineActionsCommandHandler handler = NewHandler(
            run: run,
            towers: Towers(Tower()));

        Result<IReadOnlyList<PipelineAction>> result = await handler.Handle(
            new DecidePipelineActionsCommand(run.Id, ai),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().BeOfType<CreateAlertAction>();
    }

    [Fact]
    public async Task Handle_PassesActiveAlertsToEngineForDedup()
    {
        IngestionRun run = NewRun();
        WalkToDeciding(run);

        DetectedAnomaly anomaly = Anomaly();
        string fingerprint = FingerprintFor(anomaly);
        Guid existingAlertId = Guid.NewGuid();

        DecidePipelineActionsCommandHandler handler = NewHandler(
            run: run,
            activeAlerts: [ActiveAlert(fingerprint, id: existingAlertId)],
            towers: Towers(Tower()));

        Result<IReadOnlyList<PipelineAction>> result = await handler.Handle(
            new DecidePipelineActionsCommand(run.Id, AiResult(anomalies: [anomaly])),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        UpdateAlertAction update = result.Value.OfType<UpdateAlertAction>().Should().ContainSingle().Subject;
        update.ExistingAlertId.Should().Be(existingAlertId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IngestionRun NewRun() =>
        IngestionRun.Start(
            contentHash: "deadbeef",
            fileName: "ops.csv",
            contentType: "text/csv",
            fileSizeBytes: 1024,
            submittedBy: "tester",
            startedAt: DateTimeOffset.UtcNow);

    private static void WalkToDeciding(IngestionRun run)
    {
        run.TransitionTo(IngestionStatus.Parsing);
        run.TransitionTo(IngestionStatus.Analyzing);
        run.TransitionTo(IngestionStatus.Deciding);
    }

    private static DecidePipelineActionsCommandHandler NewHandler(
        IngestionRun? run = null,
        IReadOnlyList<AlertSnapshot>? activeAlerts = null,
        IReadOnlyDictionary<string, TowerSnapshot>? towers = null) =>
        new(
            new FakeIngestionRunRepository(run),
            new DefaultDecisionEngine(new DecisionEngineOptions()),
            new FakeAlertSnapshotProvider(activeAlerts ?? []),
            new FakeTowerSnapshotProvider(towers ?? Towers()),
            NullLogger<DecidePipelineActionsCommandHandler>.Instance);

    private sealed class FakeIngestionRunRepository(IngestionRun? run) : IIngestionRunRepository
    {
        public Task<IngestionRun?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(run is not null && run.Id == id ? run : null);

        public Task<IngestionRun?> GetByContentHashAsync(string contentHash, CancellationToken ct = default) =>
            Task.FromResult<IngestionRun?>(null);

        public Task AddAsync(IngestionRun _, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddEventsAsync(IEnumerable<NetworkEvent> _, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<NetworkEvent>> ListEventsAsync(Guid _, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NetworkEvent>>([]);
    }

    private sealed class FakeAlertSnapshotProvider(IReadOnlyList<AlertSnapshot> alerts) : IAlertSnapshotProvider
    {
        public Task<IReadOnlyList<AlertSnapshot>> GetActiveAsync(CancellationToken _ = default) =>
            Task.FromResult(alerts);
    }

    private sealed class FakeTowerSnapshotProvider(IReadOnlyDictionary<string, TowerSnapshot> towers) : ITowerSnapshotProvider
    {
        public Task<IReadOnlyDictionary<string, TowerSnapshot>> GetCurrentAsync(CancellationToken _ = default) =>
            Task.FromResult(towers);
    }
}
