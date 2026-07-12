using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Application.Abstractions.Pipeline;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Application.Ingestion.Stage4_Persist;
using Modules.Network.Domain;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Domain.Towers;
using SharedKernel;
using Xunit;
using static Modules.Network.UnitTests.Ingestion.Decisions.DecisionFixtures;
using DomainTower = Modules.Network.Domain.Towers.Tower;

namespace Modules.Network.UnitTests.Ingestion.Stage4_Persist;

public sealed class ApplyPipelineActionsCommandHandlerTests
{
    [Fact]
    public async Task Handle_RunNotFound_ReturnsNotFound()
    {
        ApplyPipelineActionsCommandHandler handler = NewHandler();

        var command = new ApplyPipelineActionsCommand(Guid.NewGuid(), []);
        Result<PipelineActionCounts> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.RunNotFound");
    }

    [Fact]
    public async Task Handle_RunInWrongStage_ReturnsConflict()
    {
        IngestionRun run = NewRun(); // Pending — orchestrator should have moved to Persisting
        ApplyPipelineActionsCommandHandler handler = NewHandler(run: run);

        Result<PipelineActionCounts> result = await handler.Handle(
            new ApplyPipelineActionsCommand(run.Id, []), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.WrongStage");
    }

    [Fact]
    public async Task Handle_NoActions_ReturnsZeroCounts()
    {
        IngestionRun run = NewRun();
        WalkToPersisting(run);
        ApplyPipelineActionsCommandHandler handler = NewHandler(run: run);

        Result<PipelineActionCounts> result = await handler.Handle(
            new ApplyPipelineActionsCommand(run.Id, []), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new PipelineActionCounts(0, 0, 0, 0));
    }

    [Fact]
    public async Task Handle_CreateAlertAction_DispatchesToExecutorWithEnrichedRequest()
    {
        IngestionRun run = NewRun();
        WalkToPersisting(run);

        DetectedAnomaly anomaly = Anomaly(tower: "LOS-T-014");
        var action = new CreateAlertAction(FingerprintFor(anomaly), anomaly);

        var captured = new List<AlertActionRequest>();
        var executor = new FakeAlertExecutor(requests =>
        {
            captured.AddRange(requests);
            return Result.Success(new AlertActionsResult(AlertsCreated: 1, AlertsUpdated: 0));
        });

        ApplyPipelineActionsCommandHandler handler = NewHandler(
            run: run,
            towers: Towers(Tower("LOS-T-014", region: "Lagos West")),
            alertExecutor: executor);

        Result<PipelineActionCounts> result = await handler.Handle(
            new ApplyPipelineActionsCommand(run.Id, [action]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AlertsCreated.Should().Be(1);
        result.Value.AlertsUpdated.Should().Be(0);

        captured.Should().ContainSingle();
        captured[0].ExistingAlertId.Should().BeNull();
        captured[0].TowerCode.Should().Be("LOS-T-014");
        captured[0].Region.Should().Be("Lagos West");
        captured[0].AnomalyFingerprint.Should().Be(FingerprintFor(anomaly));
        captured[0].Title.Should().Contain("Signal drop");
    }

    [Fact]
    public async Task Handle_UpdateAlertAction_PassesExistingAlertId()
    {
        IngestionRun run = NewRun();
        WalkToPersisting(run);

        Guid existingId = Guid.NewGuid();
        DetectedAnomaly anomaly = Anomaly();
        var action = new UpdateAlertAction(FingerprintFor(anomaly), existingId, anomaly);

        var captured = new List<AlertActionRequest>();
        var executor = new FakeAlertExecutor(requests =>
        {
            captured.AddRange(requests);
            return Result.Success(new AlertActionsResult(AlertsCreated: 0, AlertsUpdated: 1));
        });

        ApplyPipelineActionsCommandHandler handler = NewHandler(
            run: run,
            towers: Towers(Tower()),
            alertExecutor: executor);

        Result<PipelineActionCounts> result = await handler.Handle(
            new ApplyPipelineActionsCommand(run.Id, [action]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AlertsUpdated.Should().Be(1);
        captured[0].ExistingAlertId.Should().Be(existingId);
    }

    [Fact]
    public async Task Handle_AlertExecutorFailure_PropagatesError()
    {
        IngestionRun run = NewRun();
        WalkToPersisting(run);

        var executor = new FakeAlertExecutor(_ =>
            Result.Failure<AlertActionsResult>(Error.Failure("Network.Ingestion.AlertWriteFailed", "boom")));

        ApplyPipelineActionsCommandHandler handler = NewHandler(
            run: run,
            towers: Towers(Tower()),
            alertExecutor: executor);

        DetectedAnomaly anomaly = Anomaly();
        Result<PipelineActionCounts> result = await handler.Handle(
            new ApplyPipelineActionsCommand(run.Id, [new CreateAlertAction(FingerprintFor(anomaly), anomaly)]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.AlertWriteFailed");
    }

    [Fact]
    public async Task Handle_UpdateTowerAction_AppliesViaTowerRepository()
    {
        IngestionRun run = NewRun();
        WalkToPersisting(run);

        DomainTower tower = DomainTower.Create(
            "LOS-T-014", "T-014", "Lagos West",
            6.5, 3.5, 0, 0,
            signalPct: 80, loadPct: 50, status: TowerStatus.Ok, issue: null);

        var fakeTowers = new FakeTowerRepository(tower);

        var statusChange = new TowerStatusChange("LOS-T-014", "ok", "critical", "outage");
        var metric = new TowerMetricUpdate("LOS-T-014", SignalPct: 30, LoadPct: 92, LatencyMs: null);
        var action = new UpdateTowerAction("LOS-T-014", statusChange, metric);

        ApplyPipelineActionsCommandHandler handler = NewHandler(
            run: run,
            towers: Towers(Tower("LOS-T-014")),
            towerRepo: fakeTowers);

        Result<PipelineActionCounts> result = await handler.Handle(
            new ApplyPipelineActionsCommand(run.Id, [action]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TowerUpdates.Should().Be(1);
        tower.SignalPct.Should().Be(30);
        tower.LoadPct.Should().Be(92);
        tower.Status.Should().Be(TowerStatus.Critical);
    }

    [Fact]
    public async Task Handle_OptimizationAction_DispatchesCreateOptimizationCommand()
    {
        IngestionRun run = NewRun();
        WalkToPersisting(run);

        ProposedOptimization optimization = Optimization(tower: "LOS-T-014", impact: 0.7m);
        var action = new CreateOptimizationAction("fingerprint-abc", optimization);

        var fakeSender = new FakeOptimizationSender();
        ApplyPipelineActionsCommandHandler handler = NewHandler(
            run: run, towers: Towers(Tower()), sender: fakeSender);

        Result<PipelineActionCounts> result = await handler.Handle(
            new ApplyPipelineActionsCommand(run.Id, [action]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OptimizationsCreated.Should().Be(1);

        CreateOptimizationCommand dispatched = fakeSender.DispatchedOptimizations.Should().ContainSingle().Subject;
        dispatched.IngestionRunId.Should().Be(run.Id);
        dispatched.TowerCode.Should().Be("LOS-T-014");
        dispatched.AnomalyFingerprint.Should().Be("fingerprint-abc");
        dispatched.EstimatedImpact.Should().Be(0.7m);
    }

    [Fact]
    public async Task Handle_FullScenario_AggregatesCounts()
    {
        IngestionRun run = NewRun();
        WalkToPersisting(run);

        DetectedAnomaly anomaly = Anomaly(tower: "LOS-T-014");
        DomainTower tower = DomainTower.Create(
            "LOS-T-014", "T-014", "Lagos West",
            6.5, 3.5, 0, 0, 80, 50, TowerStatus.Ok, null);

        var actions = new PipelineAction[]
        {
            new CreateAlertAction(FingerprintFor(anomaly), anomaly),
            new CreateOptimizationAction("fp", Optimization()),
            new UpdateTowerAction("LOS-T-014",
                new TowerStatusChange("LOS-T-014", "ok", "critical", "outage"),
                null)
        };

        var executor = new FakeAlertExecutor(_ =>
            Result.Success(new AlertActionsResult(AlertsCreated: 1, AlertsUpdated: 0)));

        ApplyPipelineActionsCommandHandler handler = NewHandler(
            run: run,
            towers: Towers(Tower("LOS-T-014", region: "Lagos West")),
            alertExecutor: executor,
            towerRepo: new FakeTowerRepository(tower));

        Result<PipelineActionCounts> result = await handler.Handle(
            new ApplyPipelineActionsCommand(run.Id, actions), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AlertsCreated.Should().Be(1);
        result.Value.OptimizationsCreated.Should().Be(1);
        result.Value.TowerUpdates.Should().Be(1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IngestionRun NewRun() =>
        IngestionRun.Start("hash", "f.csv", "text/csv", 100, "tester", DateTimeOffset.UtcNow);

    private static void WalkToPersisting(IngestionRun run)
    {
        run.TransitionTo(IngestionStatus.Parsing);
        run.TransitionTo(IngestionStatus.Analyzing);
        run.TransitionTo(IngestionStatus.Deciding);
        run.TransitionTo(IngestionStatus.Persisting);
    }

    private static ApplyPipelineActionsCommandHandler NewHandler(
        IngestionRun? run = null,
        IReadOnlyDictionary<string, TowerSnapshot>? towers = null,
        IAlertActionExecutor? alertExecutor = null,
        ITowerRepository? towerRepo = null,
        ISender? sender = null) =>
        new(
            new FakeRunRepo(run),
            new FakeTowerSnapshotProvider(towers ?? Towers()),
            towerRepo ?? new FakeTowerRepository(),
            alertExecutor ?? new FakeAlertExecutor(_ => Result.Success(new AlertActionsResult(0, 0))),
            sender ?? new FakeOptimizationSender(),
            new FakeUnitOfWork(),
            NullLogger<ApplyPipelineActionsCommandHandler>.Instance);

    private sealed class FakeRunRepo(IngestionRun? run) : IIngestionRunRepository
    {
        public Task<IngestionRun?> GetByIdAsync(Guid id, CancellationToken _ = default) =>
            Task.FromResult(run is not null && run.Id == id ? run : null);
        public Task<IngestionRun?> GetByContentHashAsync(string _, CancellationToken __ = default) =>
            Task.FromResult<IngestionRun?>(null);
        public Task AddAsync(IngestionRun _, CancellationToken __ = default) => Task.CompletedTask;
        public Task AddEventsAsync(IEnumerable<NetworkEvent> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task<IReadOnlyList<NetworkEvent>> ListEventsAsync(Guid _, CancellationToken __ = default) =>
            Task.FromResult<IReadOnlyList<NetworkEvent>>([]);
    }

    private sealed class FakeTowerSnapshotProvider(IReadOnlyDictionary<string, TowerSnapshot> towers) : ITowerSnapshotProvider
    {
        public Task<IReadOnlyDictionary<string, TowerSnapshot>> GetCurrentAsync(CancellationToken _ = default) =>
            Task.FromResult(towers);
    }

    private sealed class FakeTowerRepository(DomainTower? tower = null) : ITowerRepository
    {
        public Task<IReadOnlyList<DomainTower>> ListAsync(CancellationToken _ = default) =>
            Task.FromResult<IReadOnlyList<DomainTower>>(tower is null ? [] : [tower]);
        public Task<DomainTower?> GetByCodeAsync(string code, CancellationToken _ = default) =>
            Task.FromResult(tower is not null && tower.Code == code ? tower : null);
        public Task<IReadOnlyList<DomainTower>> ListByRegionAsync(string _, CancellationToken __ = default) =>
            Task.FromResult<IReadOnlyList<DomainTower>>([]);
        public Task AddRangeAsync(IEnumerable<DomainTower> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task<int> CountAsync(CancellationToken _ = default) => Task.FromResult(tower is null ? 0 : 1);
    }

    private sealed class FakeAlertExecutor(Func<IReadOnlyList<AlertActionRequest>, Result<AlertActionsResult>> respond) : IAlertActionExecutor
    {
        public Task<Result<AlertActionsResult>> ExecuteAsync(
            IReadOnlyList<AlertActionRequest> requests, CancellationToken _ = default) =>
            Task.FromResult(respond(requests));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken _ = default) => Task.FromResult(0);
    }

    /// <summary>
    /// Stub ISender that succeeds for CreateOptimizationCommand (returns a fresh Guid)
    /// and refuses anything else — Stage 4 should only dispatch CreateOptimizationCommand.
    /// </summary>
    private sealed class FakeOptimizationSender : ISender
    {
        public List<CreateOptimizationCommand> DispatchedOptimizations { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken _ = default)
        {
            if (request is CreateOptimizationCommand opt)
            {
                DispatchedOptimizations.Add(opt);
                return Task.FromResult((TResponse)(object)Result.Success(Guid.NewGuid()));
            }

            throw new InvalidOperationException(
                $"Stage 4 unexpectedly dispatched {request!.GetType().Name}");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
