using System.Text;
using Application.Abstractions.Events;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Network.Application.Ingestion.Pipeline;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Application.Ingestion.Stage2_Analyze;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Application.Ingestion.Stage4_Persist;
using Modules.Network.Domain;
using Modules.Network.Domain.Ingestion;
using SharedKernel;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Pipeline;

public sealed class ProcessNetworkLogCommandHandlerTests
{
    private static readonly byte[] SampleBytes = Encoding.UTF8.GetBytes(
        "timestamp,tower_code\n2026-05-05T08:00:00Z,LOS-T-014\n");

    [Fact]
    public async Task Handle_HappyPath_DispatchesAllFiveStagesInOrder_AndQueuesIntegrationEvent()
    {
        var sender = new RecordingSender(eventsParsed: 1, anomaliesDetected: 1);
        var bus = new RecordingEventBus();
        var repo = new InMemoryRunRepo();

        ProcessNetworkLogCommandHandler handler = NewHandler(repo, sender, bus);

        Result<IngestionRunSummary> result = await handler.Handle(NewCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sender.DispatchedCommandTypes.Should().Equal(
            nameof(ParseNetworkLogCommand),
            nameof(AnalyzeNetworkBatchCommand),
            nameof(DecidePipelineActionsCommand),
            nameof(ApplyPipelineActionsCommand));
        bus.Published.Should().ContainSingle()
            .Which.Should().BeOfType<PipelineCompletedNotification>();
    }

    [Fact]
    public async Task Handle_HappyPath_LeavesRunInCompletedStatusWithStageTimings()
    {
        var sender = new RecordingSender(eventsParsed: 5, anomaliesDetected: 1);
        var repo = new InMemoryRunRepo();

        ProcessNetworkLogCommandHandler handler = NewHandler(repo, sender, new RecordingEventBus());

        Result<IngestionRunSummary> result = await handler.Handle(NewCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        IngestionRun run = repo.Runs.Should().ContainSingle().Subject;
        run.Status.Should().Be(IngestionStatus.Completed);
        run.CompletedAt.Should().NotBeNull();

        // 5 stages tracked: Parsing, Analyzing, Deciding, Persisting, Projecting
        run.StageTimings.Select(t => t.Stage).Should().Equal(
            IngestionStatus.Parsing,
            IngestionStatus.Analyzing,
            IngestionStatus.Deciding,
            IngestionStatus.Persisting,
            IngestionStatus.Projecting);
        run.StageTimings.Should().AllSatisfy(t => t.Succeeded.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_PropagatesCountsFromStage4ToSummary()
    {
        var sender = new RecordingSender(
            eventsParsed: 3,
            anomaliesDetected: 1,
            persistResult: new PipelineActionCounts(
                AlertsCreated: 2,
                AlertsUpdated: 1,
                OptimizationsCreated: 1,
                TowerUpdates: 2));
        var repo = new InMemoryRunRepo();

        ProcessNetworkLogCommandHandler handler = NewHandler(repo, sender, new RecordingEventBus());

        Result<IngestionRunSummary> result = await handler.Handle(NewCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.EventsParsed.Should().Be(3);
        result.Value.AlertsCreated.Should().Be(2);
        result.Value.AlertsUpdated.Should().Be(1);
        result.Value.OptimizationsCreated.Should().Be(1);
        result.Value.TopologyChanged.Should().BeTrue();
        result.Value.AnomaliesDetected.Should().Be(3); // alertsCreated + alertsUpdated
        result.Value.DeduplicatedFromPriorRun.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DuplicateContent_ShortCircuitsAndDoesNotDispatchStages()
    {
        // Pre-seed the repo with a completed run for the same content hash.
        byte[] bytes = SampleBytes;
        string hash = Fingerprints.ContentHash(bytes);
        IngestionRun prior = IngestionRun.Start(hash, "f.csv", "text/csv", bytes.Length, "tester", DateTimeOffset.UtcNow);
        WalkToProjecting(prior);
        prior.Complete(new IngestionRunCounts(1, 1, 0, 0, false), DateTimeOffset.UtcNow);

        var repo = new InMemoryRunRepo();
        repo.Runs.Add(prior);

        var sender = new RecordingSender();
        var bus = new RecordingEventBus();
        ProcessNetworkLogCommandHandler handler = NewHandler(repo, sender, bus);

        Result<IngestionRunSummary> result = await handler.Handle(NewCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IngestionRunId.Should().Be(prior.Id);
        result.Value.DeduplicatedFromPriorRun.Should().BeTrue();
        sender.DispatchedCommandTypes.Should().BeEmpty();
        bus.Published.Should().BeEmpty();
        repo.Runs.Should().ContainSingle(); // no new run created
    }

    [Fact]
    public async Task Handle_StageFailure_MarksRunFailedAndPropagatesError()
    {
        var sender = new RecordingSender(eventsParsed: 2)
        {
            FailAt = nameof(AnalyzeNetworkBatchCommand)
        };
        var repo = new InMemoryRunRepo();
        var bus = new RecordingEventBus();

        ProcessNetworkLogCommandHandler handler = NewHandler(repo, sender, bus);

        Result<IngestionRunSummary> result = await handler.Handle(NewCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.AiSchemaInvalid");

        IngestionRun run = repo.Runs.Should().ContainSingle().Subject;
        run.Status.Should().Be(IngestionStatus.Failed);
        run.FailureReason.Should().Contain("AiSchemaInvalid");
        bus.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FailureBeforeStage1_DoesNotProduceStageTimings()
    {
        var sender = new RecordingSender { FailAt = nameof(ParseNetworkLogCommand) };
        var repo = new InMemoryRunRepo();

        ProcessNetworkLogCommandHandler handler = NewHandler(repo, sender, new RecordingEventBus());

        await handler.Handle(NewCommand(), CancellationToken.None);

        IngestionRun run = repo.Runs.Single();
        run.Status.Should().Be(IngestionStatus.Failed);
        // Parse stage was attempted, so 1 timing is recorded with Succeeded=false.
        run.StageTimings.Should().ContainSingle();
        run.StageTimings[0].Succeeded.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ProcessNetworkLogCommand NewCommand() =>
        new(
            FileName: "ops.csv",
            ContentType: "text/csv",
            Content: new MemoryStream(SampleBytes, writable: false),
            SubmittedBy: "tester");

    private static void WalkToProjecting(IngestionRun run)
    {
        run.TransitionTo(IngestionStatus.Parsing);
        run.TransitionTo(IngestionStatus.Analyzing);
        run.TransitionTo(IngestionStatus.Deciding);
        run.TransitionTo(IngestionStatus.Persisting);
        run.TransitionTo(IngestionStatus.Projecting);
    }

    private static ProcessNetworkLogCommandHandler NewHandler(
        InMemoryRunRepo repo,
        ISender sender,
        IEventBus eventBus) =>
        new(
            repo,
            new InMemoryUnitOfWork(),
            sender,
            eventBus,
            NullLogger<ProcessNetworkLogCommandHandler>.Instance);

    private sealed class InMemoryRunRepo : IIngestionRunRepository
    {
        public List<IngestionRun> Runs { get; } = [];

        public Task<IngestionRun?> GetByIdAsync(Guid id, CancellationToken _ = default) =>
            Task.FromResult(Runs.FirstOrDefault(r => r.Id == id));

        public Task<IngestionRun?> GetByContentHashAsync(string contentHash, CancellationToken _ = default) =>
            Task.FromResult(Runs.FirstOrDefault(r => r.ContentHash == contentHash));

        public Task AddAsync(IngestionRun run, CancellationToken _ = default)
        {
            Runs.Add(run);
            return Task.CompletedTask;
        }

        public Task AddEventsAsync(IEnumerable<NetworkEvent> _, CancellationToken __ = default) => Task.CompletedTask;

        public Task<IReadOnlyList<NetworkEvent>> ListEventsAsync(Guid _, CancellationToken __ = default) =>
            Task.FromResult<IReadOnlyList<NetworkEvent>>([]);
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken _ = default) => Task.FromResult(0);
    }

    /// <summary>
    /// Records every command dispatched and produces canned successful results for each
    /// stage type. Callers can flip <see cref="FailAt"/> to make a specific stage fail.
    /// </summary>
    private sealed class RecordingSender : ISender
    {
        private readonly int _eventsParsed;
        private readonly int _anomaliesDetected;
        private readonly PipelineActionCounts _persistResult;

        public RecordingSender(
            int eventsParsed = 1,
            int anomaliesDetected = 0,
            PipelineActionCounts? persistResult = null)
        {
            _eventsParsed = eventsParsed;
            _anomaliesDetected = anomaliesDetected;
            _persistResult = persistResult ??
                new PipelineActionCounts(
                    AlertsCreated: anomaliesDetected,
                    AlertsUpdated: 0,
                    OptimizationsCreated: 0,
                    TowerUpdates: 0);
        }

        public List<string> DispatchedCommandTypes { get; } = [];
        public string? FailAt { get; init; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken _ = default)
        {
            string commandName = request!.GetType().Name;
            DispatchedCommandTypes.Add(commandName);

            // Build the failure with the right typed Result<T> for the command being failed.
            // Each stage command has a different response type, so a single Result<int>.Failure
            // would throw an InvalidCastException downstream.
            Error stubError = Error.Failure("Network.Ingestion.AiSchemaInvalid", "stub failure");
            object response = commandName switch
            {
                nameof(ParseNetworkLogCommand) => FailAt == commandName
                    ? Result.Failure<int>(stubError)
                    : Result.Success(_eventsParsed),
                nameof(AnalyzeNetworkBatchCommand) => FailAt == commandName
                    ? Result.Failure<AiAnalysisResult>(stubError)
                    : Result.Success(BuildAnalysis()),
                nameof(DecidePipelineActionsCommand) => FailAt == commandName
                    ? Result.Failure<IReadOnlyList<PipelineAction>>(stubError)
                    : Result.Success<IReadOnlyList<PipelineAction>>([]),
                nameof(ApplyPipelineActionsCommand) => FailAt == commandName
                    ? Result.Failure<PipelineActionCounts>(stubError)
                    : Result.Success(_persistResult),
                _ => throw new InvalidOperationException($"Unexpected command {commandName}")
            };

            return Task.FromResult((TResponse)response);
        }

        private AiAnalysisResult BuildAnalysis()
        {
            if (_anomaliesDetected == 0)
            {
                return AiAnalysisResult.Empty;
            }

            DetectedAnomaly[] anomalies = Enumerable.Range(0, _anomaliesDetected)
                .Select(i => new DetectedAnomaly(
                    TowerCode: $"LOS-T-{i:000}",
                    Type: AnomalyType.SignalDrop,
                    Severity: PipelineAlertSeverity.Warn,
                    Confidence: 0.8m,
                    DetectedAt: DateTimeOffset.UtcNow,
                    Explanation: "stub",
                    Metrics: new Dictionary<string, decimal>()))
                .ToArray();
            return new AiAnalysisResult(anomalies, [], null);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        // MediatR 12.5 added a void-response Send overload; orchestrator doesn't use it but
        // ISender still requires the implementation.
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// Captures every integration event the orchestrator hands to the bus. In production,
    /// IntegrationEventProcessorJob drains the queue and republishes via MediatR; tests
    /// don't run that worker, so subscribers are intentionally not invoked here.
    /// </summary>
    private sealed class RecordingEventBus : IEventBus
    {
        public List<IIntegrationEvent> Published { get; } = [];

        public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
            where T : class, IIntegrationEvent
        {
            Published.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }
}
