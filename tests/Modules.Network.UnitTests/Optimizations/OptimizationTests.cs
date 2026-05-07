using FluentAssertions;
using Modules.Network.Domain.Optimizations;
using Xunit;

namespace Modules.Network.UnitTests.Optimizations;

public sealed class OptimizationTests
{
    private static readonly DateTimeOffset Ts = DateTimeOffset.Parse("2026-05-05T08:00:00Z");
    private static readonly Guid RunId = Guid.NewGuid();

    [Fact]
    public void Propose_HappyPath_PopulatesAllFieldsAndUppercasesTowerCode()
    {
        Optimization opt = Optimization.Propose(
            ingestionRunId: RunId,
            towerCode: "los-t-014",
            anomalyFingerprint: "fp-abc",
            type: OptimizationType.LoadBalance,
            estimatedImpact: 0.7m,
            rationale: "load over 90% sustained",
            proposedAt: Ts);

        opt.Id.Should().NotBe(Guid.Empty);
        opt.IngestionRunId.Should().Be(RunId);
        opt.TowerCode.Should().Be("LOS-T-014");
        opt.AnomalyFingerprint.Should().Be("fp-abc");
        opt.Type.Should().Be(OptimizationType.LoadBalance);
        opt.EstimatedImpact.Should().Be(0.7m);
        opt.Rationale.Should().Be("load over 90% sustained");
        opt.ProposedAt.Should().Be(Ts);
    }

    [Fact]
    public void Propose_AcceptsEmptyAnomalyFingerprint_ForOrphanRecommendations()
    {
        // Orphan recommendation = optimization not correlated to an in-batch anomaly.
        // The decision engine emits empty-string fingerprint for these on purpose.
        Optimization opt = Optimization.Propose(
            RunId, "LOS-T-014", anomalyFingerprint: "",
            OptimizationType.PowerAdjust, 0.4m, "preventive", Ts);

        opt.AnomalyFingerprint.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Propose_RejectsBlankTowerCode(string blank)
    {
        Action act = () => Optimization.Propose(
            RunId, blank, "fp", OptimizationType.LoadBalance, 0.5m, "x", Ts);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Propose_RejectsNullFingerprint()
    {
        Action act = () => Optimization.Propose(
            RunId, "LOS-T-014", anomalyFingerprint: null!,
            OptimizationType.LoadBalance, 0.5m, "x", Ts);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Propose_RejectsBlankRationale(string blank)
    {
        Action act = () => Optimization.Propose(
            RunId, "LOS-T-014", "fp", OptimizationType.LoadBalance, 0.5m, blank, Ts);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Propose_RejectsImpactOutOfRange(decimal badImpact)
    {
        Action act = () => Optimization.Propose(
            RunId, "LOS-T-014", "fp", OptimizationType.LoadBalance, badImpact, "x", Ts);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
