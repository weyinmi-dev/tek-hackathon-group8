using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Network.Application.Ingestion.Stage2_Analyze;
using Application.Abstractions.Pipeline;
using Modules.Network.Domain.Ingestion;
using SharedKernel;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Stage2_Analyze;

public sealed class AnalyzeNetworkBatchCommandHandlerTests
{
    [Fact]
    public async Task Handle_RunNotFound_ReturnsNotFound()
    {
        AnalyzeNetworkBatchCommandHandler handler = NewHandler();

        Result<AiAnalysisResult> result = await handler.Handle(
            new AnalyzeNetworkBatchCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.RunNotFound");
    }

    [Fact]
    public async Task Handle_RunInWrongStage_ReturnsConflict()
    {
        IngestionRun run = NewRun(); // Pending; orchestrator should have moved to Analyzing

        AnalyzeNetworkBatchCommandHandler handler = NewHandler(run: run);

        Result<AiAnalysisResult> result = await handler.Handle(
            new AnalyzeNetworkBatchCommand(run.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.WrongStage");
    }

    [Fact]
    public async Task Handle_RunInAnalyzing_LoadsEventsAndCallsAnalyzer()
    {
        IngestionRun run = NewRun();
        WalkToAnalyzing(run);
        // The analyzer contract is module-neutral now (M12): the handler projects its NetworkEvent
        // entities into NetworkEventSnapshot before calling out, so that projection is asserted here.
        var capturedEvents = new List<IReadOnlyList<NetworkEventSnapshot>>();
        var fakeAnalyzer = new FakeAnalyzer((id, events) =>
        {
            capturedEvents.Add(events);
            return Result.Success(new AiAnalysisResult([], [], null));
        });

        IReadOnlyList<NetworkEvent> events = [
            NetworkEvent.Create(run.Id, DateTimeOffset.Parse("2026-05-05T08:00:00Z"),
                "LOS-T-014", 90, 50, 20, "OK", null)
        ];

        AnalyzeNetworkBatchCommandHandler handler = NewHandler(run: run, events: events, analyzer: fakeAnalyzer);

        Result<AiAnalysisResult> result = await handler.Handle(
            new AnalyzeNetworkBatchCommand(run.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedEvents.Should().ContainSingle();
        capturedEvents[0].Should().HaveCount(1);
        capturedEvents[0][0].TowerCode.Should().Be("LOS-T-014");
    }

    [Fact]
    public async Task Handle_AnalyzerFailure_PropagatesError()
    {
        IngestionRun run = NewRun();
        WalkToAnalyzing(run);
        var fakeAnalyzer = new FakeAnalyzer((_, _) =>
            Result.Failure<AiAnalysisResult>(Error.Failure("Network.Ingestion.AiSchemaInvalid", "bad")));

        AnalyzeNetworkBatchCommandHandler handler = NewHandler(run: run, analyzer: fakeAnalyzer);

        Result<AiAnalysisResult> result = await handler.Handle(
            new AnalyzeNetworkBatchCommand(run.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.AiSchemaInvalid");
    }

    private static IngestionRun NewRun() =>
        IngestionRun.Start("hash", "f.csv", "text/csv", 100, "tester", DateTimeOffset.UtcNow);

    private static void WalkToAnalyzing(IngestionRun run)
    {
        run.TransitionTo(IngestionStatus.Parsing);
        run.TransitionTo(IngestionStatus.Analyzing);
    }

    private static AnalyzeNetworkBatchCommandHandler NewHandler(
        IngestionRun? run = null,
        IReadOnlyList<NetworkEvent>? events = null,
        INetworkBatchAnalyzer? analyzer = null) =>
        new(
            new FakeRepo(run, events ?? []),
            analyzer ?? new FakeAnalyzer((_, _) => Result.Success(new AiAnalysisResult([], [], null))),
            NullLogger<AnalyzeNetworkBatchCommandHandler>.Instance);

    private sealed class FakeRepo(IngestionRun? run, IReadOnlyList<NetworkEvent> events) : IIngestionRunRepository
    {
        public Task<IngestionRun?> GetByIdAsync(Guid id, CancellationToken _ = default) =>
            Task.FromResult(run is not null && run.Id == id ? run : null);

        public Task<IngestionRun?> GetByContentHashAsync(string _, CancellationToken __ = default) =>
            Task.FromResult<IngestionRun?>(null);

        public Task AddAsync(IngestionRun _, CancellationToken __ = default) => Task.CompletedTask;
        public Task AddEventsAsync(IEnumerable<NetworkEvent> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task<IReadOnlyList<NetworkEvent>> ListEventsAsync(Guid _, CancellationToken __ = default) =>
            Task.FromResult(events);
    }

    private sealed class FakeAnalyzer(Func<Guid, IReadOnlyList<NetworkEventSnapshot>, Result<AiAnalysisResult>> respond)
        : INetworkBatchAnalyzer
    {
        public Task<Result<AiAnalysisResult>> AnalyzeAsync(
            Guid ingestionRunId,
            IReadOnlyList<NetworkEventSnapshot> events,
            string? mcpFilePath = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(respond(ingestionRunId, events));
    }
}
