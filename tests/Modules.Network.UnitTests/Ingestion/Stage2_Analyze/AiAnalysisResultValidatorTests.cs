using FluentAssertions;
using FluentValidation.Results;
using Modules.Ai.Infrastructure.Pipeline.Validators;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Stage2_Analyze;

public sealed class AiAnalysisResultValidatorTests
{
    private readonly AiAnalysisResultValidator _validator = new();

    private static DetectedAnomaly ValidAnomaly() => new(
        TowerCode: "LOS-T-014",
        Type: AnomalyType.SignalDrop,
        Severity: PipelineAlertSeverity.Warn,
        Confidence: 0.8m,
        DetectedAt: DateTimeOffset.Parse("2026-05-05T08:00:00Z"),
        Explanation: "evidence",
        Metrics: new Dictionary<string, decimal>());

    private static ProposedOptimization ValidOptimization() => new(
        TowerCode: "LOS-T-014",
        Type: OptimizationType.LoadBalance,
        EstimatedImpact: 0.5m,
        Rationale: "evidence");

    [Fact]
    public void Empty_IsValid()
    {
        ValidationResult result = _validator.Validate(AiAnalysisResult.Empty);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NullAnomalyArray_IsRejected()
    {
        var bad = new AiAnalysisResult(null!, [], null);

        ValidationResult result = _validator.Validate(bad);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("anomalies", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NullOptimizationArray_IsRejected()
    {
        var bad = new AiAnalysisResult([], null!, null);

        ValidationResult result = _validator.Validate(bad);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("optimizations", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Anomaly_BlankTowerCode_IsRejected()
    {
        DetectedAnomaly bad = ValidAnomaly() with { TowerCode = "" };
        var input = new AiAnalysisResult([bad], [], null);

        ValidationResult result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("towerCode", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(2)]
    public void Anomaly_ConfidenceOutOfRange_IsRejected(decimal badConfidence)
    {
        DetectedAnomaly bad = ValidAnomaly() with { Confidence = badConfidence };
        var input = new AiAnalysisResult([bad], [], null);

        ValidationResult result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("confidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Anomaly_DefaultDetectedAt_IsRejected()
    {
        DetectedAnomaly bad = ValidAnomaly() with { DetectedAt = default };
        var input = new AiAnalysisResult([bad], [], null);

        ValidationResult result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("detectedAt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Anomaly_BlankExplanation_IsRejected()
    {
        DetectedAnomaly bad = ValidAnomaly() with { Explanation = "" };
        var input = new AiAnalysisResult([bad], [], null);

        ValidationResult result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("explanation", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.5)]
    public void Optimization_ImpactOutOfRange_IsRejected(decimal badImpact)
    {
        ProposedOptimization bad = ValidOptimization() with { EstimatedImpact = badImpact };
        var input = new AiAnalysisResult([], [bad], null);

        ValidationResult result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("estimatedImpact", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Topology_BlankTowerCodeInStatusChange_IsRejected()
    {
        var topology = new TopologyDelta(
            StatusChanges: [new TowerStatusChange("", "ok", "critical", null)],
            MetricUpdates: []);
        var input = new AiAnalysisResult([], [], topology);

        ValidationResult result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Topology_OutOfRangeMetric_IsRejected()
    {
        var topology = new TopologyDelta(
            StatusChanges: [],
            MetricUpdates: [new TowerMetricUpdate("LOS-T-014", SignalPct: 150, LoadPct: null, LatencyMs: null)]);
        var input = new AiAnalysisResult([], [], topology);

        ValidationResult result = _validator.Validate(input);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FullValid_PassesAllRules()
    {
        var input = new AiAnalysisResult(
            [ValidAnomaly()],
            [ValidOptimization()],
            new TopologyDelta(
                StatusChanges: [new TowerStatusChange("LOS-T-014", "ok", "critical", "outage")],
                MetricUpdates: [new TowerMetricUpdate("LOS-T-014", 50, 90, 120)]));

        ValidationResult result = _validator.Validate(input);

        result.IsValid.Should().BeTrue();
    }
}
