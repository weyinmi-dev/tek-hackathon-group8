using FluentAssertions;
using Modules.Network.Domain.Ingestion;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion;

public sealed class IngestionRunTests
{
    private static IngestionRun NewRun() =>
        IngestionRun.Start(
            contentHash: "deadbeef",
            fileName: "ops.csv",
            contentType: "text/csv",
            fileSizeBytes: 1024,
            submittedBy: "tester@telcopilot",
            startedAt: DateTimeOffset.Parse("2026-05-05T08:00:00Z"));

    [Fact]
    public void Start_InitializesPendingStatusAndAssignsId()
    {
        IngestionRun run = NewRun();

        run.Id.Should().NotBe(Guid.Empty);
        run.Status.Should().Be(IngestionStatus.Pending);
        run.CompletedAt.Should().BeNull();
        run.FailureReason.Should().BeNull();
        run.StageTimings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_RejectsBlankRequiredFields(string blank)
    {
        Action act = () => IngestionRun.Start(
            contentHash: blank,
            fileName: "f.csv",
            contentType: "text/csv",
            fileSizeBytes: 0,
            submittedBy: "u",
            startedAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TransitionTo_WalksHappyPath()
    {
        IngestionRun run = NewRun();

        run.TransitionTo(IngestionStatus.Parsing);
        run.TransitionTo(IngestionStatus.Analyzing);
        run.TransitionTo(IngestionStatus.Deciding);
        run.TransitionTo(IngestionStatus.Persisting);
        run.TransitionTo(IngestionStatus.Projecting);

        run.Status.Should().Be(IngestionStatus.Projecting);
    }

    [Fact]
    public void TransitionTo_RejectsSkippingStages()
    {
        IngestionRun run = NewRun();

        Action act = () => run.TransitionTo(IngestionStatus.Analyzing);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pending*Analyzing*");
    }

    [Fact]
    public void TransitionTo_RejectsBackwardMoves()
    {
        IngestionRun run = NewRun();
        run.TransitionTo(IngestionStatus.Parsing);
        run.TransitionTo(IngestionStatus.Analyzing);

        Action act = () => run.TransitionTo(IngestionStatus.Parsing);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fail_AllowedFromAnyNonTerminalStatus()
    {
        IngestionRun run = NewRun();
        run.TransitionTo(IngestionStatus.Parsing);

        run.Fail("upstream parse error", DateTimeOffset.Parse("2026-05-05T08:01:00Z"));

        run.Status.Should().Be(IngestionStatus.Failed);
        run.FailureReason.Should().Be("upstream parse error");
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_RejectedAfterCompletion()
    {
        IngestionRun run = WalkToProjecting();
        run.Complete(
            new IngestionRunCounts(0, 0, 0, 0, TopologyChanged: false),
            DateTimeOffset.Parse("2026-05-05T08:05:00Z"));

        Action act = () => run.Fail("late error", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_OnlyAllowedFromProjecting()
    {
        IngestionRun run = NewRun();
        run.TransitionTo(IngestionStatus.Parsing);

        Action act = () => run.Complete(
            new IngestionRunCounts(0, 0, 0, 0, TopologyChanged: false),
            DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_RecordsCountsAndFinalStatus()
    {
        IngestionRun run = WalkToProjecting();

        var counts = new IngestionRunCounts(
            AnomaliesDetected: 3,
            AlertsCreated: 1,
            AlertsUpdated: 2,
            OptimizationsCreated: 1,
            TopologyChanged: true);

        run.Complete(counts, DateTimeOffset.Parse("2026-05-05T08:05:00Z"));

        run.Status.Should().Be(IngestionStatus.Completed);
        run.AnomaliesDetected.Should().Be(3);
        run.AlertsCreated.Should().Be(1);
        run.AlertsUpdated.Should().Be(2);
        run.OptimizationsCreated.Should().Be(1);
        run.TopologyChanged.Should().BeTrue();
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordStageTiming_AppendsInOrder()
    {
        IngestionRun run = NewRun();

        var t1 = new StageTiming(IngestionStatus.Parsing,
            DateTimeOffset.Parse("2026-05-05T08:00:00Z"),
            DateTimeOffset.Parse("2026-05-05T08:00:01Z"),
            Succeeded: true, FailureReason: null);
        var t2 = new StageTiming(IngestionStatus.Analyzing,
            DateTimeOffset.Parse("2026-05-05T08:00:01Z"),
            DateTimeOffset.Parse("2026-05-05T08:00:03Z"),
            Succeeded: true, FailureReason: null);

        run.RecordStageTiming(t1);
        run.RecordStageTiming(t2);

        run.StageTimings.Should().HaveCount(2);
        run.StageTimings[0].Should().Be(t1);
        run.StageTimings[1].Should().Be(t2);
        run.StageTimings[1].Elapsed.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RecordParsedCount_RejectsNegative()
    {
        IngestionRun run = NewRun();

        Action act = () => run.RecordParsedCount(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static IngestionRun WalkToProjecting()
    {
        IngestionRun run = NewRun();
        run.TransitionTo(IngestionStatus.Parsing);
        run.TransitionTo(IngestionStatus.Analyzing);
        run.TransitionTo(IngestionStatus.Deciding);
        run.TransitionTo(IngestionStatus.Persisting);
        run.TransitionTo(IngestionStatus.Projecting);
        return run;
    }
}
