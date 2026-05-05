using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Analytics.Application.Pipeline;
using Modules.Analytics.Domain;
using Modules.Analytics.Domain.Ingestion;
using Modules.Network.Application.Ingestion.Pipeline;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Pipeline;

public sealed class PipelineCompletedDashboardHandlerTests
{
    private static PipelineCompletedNotification SampleNotification(Guid? runId = null) => new(
        IngestionRunId: runId ?? Guid.NewGuid(),
        ContentHash: "ABCDEF",
        FileName: "ops.csv",
        EventsParsed: 5,
        AnomaliesDetected: 2,
        AlertsCreated: 1,
        AlertsUpdated: 1,
        OptimizationsCreated: 1,
        TopologyChanged: true,
        CompletedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_NewRun_PersistsDashboardEntryWithAllCounts()
    {
        var dashboard = new InMemoryDashboard();
        PipelineCompletedDashboardHandler handler = NewHandler(dashboard);

        PipelineCompletedNotification notification = SampleNotification();
        await handler.Handle(notification, CancellationToken.None);

        IngestionDashboardEntry entry = dashboard.Entries.Should().ContainSingle().Subject;
        entry.IngestionRunId.Should().Be(notification.IngestionRunId);
        entry.FileName.Should().Be(notification.FileName);
        entry.EventsParsed.Should().Be(5);
        entry.AnomaliesDetected.Should().Be(2);
        entry.AlertsCreated.Should().Be(1);
        entry.AlertsUpdated.Should().Be(1);
        entry.OptimizationsCreated.Should().Be(1);
        entry.TopologyChanged.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DuplicateRun_IsIdempotentNoop()
    {
        var dashboard = new InMemoryDashboard();
        PipelineCompletedDashboardHandler handler = NewHandler(dashboard);

        Guid runId = Guid.NewGuid();
        await handler.Handle(SampleNotification(runId), CancellationToken.None);
        await handler.Handle(SampleNotification(runId), CancellationToken.None); // re-fire

        dashboard.Entries.Should().ContainSingle();
    }

    private static PipelineCompletedDashboardHandler NewHandler(InMemoryDashboard dashboard) =>
        new(
            dashboard,
            new InMemoryUow(),
            NullLogger<PipelineCompletedDashboardHandler>.Instance);

    private sealed class InMemoryDashboard : IIngestionDashboardRepository
    {
        public List<IngestionDashboardEntry> Entries { get; } = [];

        public Task AddAsync(IngestionDashboardEntry entry, CancellationToken _ = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsForRunAsync(Guid ingestionRunId, CancellationToken _ = default) =>
            Task.FromResult(Entries.Any(e => e.IngestionRunId == ingestionRunId));

        public Task<IReadOnlyList<IngestionDashboardEntry>> ListRecentAsync(int limit = 50, CancellationToken _ = default) =>
            Task.FromResult<IReadOnlyList<IngestionDashboardEntry>>(Entries);
    }

    private sealed class InMemoryUow : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken _ = default) => Task.FromResult(0);
    }
}
