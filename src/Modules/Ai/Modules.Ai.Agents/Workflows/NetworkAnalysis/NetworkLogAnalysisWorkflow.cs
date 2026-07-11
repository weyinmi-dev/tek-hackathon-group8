using Application.Abstractions.Pipeline;
using Microsoft.Agents.AI.Workflows;
using Modules.Ai.Application.Analysis;

namespace Modules.Ai.Agents.Workflows.NetworkAnalysis;

/// <summary>Workflow input: a run's parsed events, module-neutral (Phase 3 M12).</summary>
public sealed record AnalyzeNetworkBatchRequest(
    Guid IngestionRunId,
    IReadOnlyList<NetworkEventSnapshot> Events,
    string? McpFilePath);

/// <summary>
/// Stage-2 analysis (Phase 2 §7.2): classify each tower's events against the deterministic
/// <see cref="AnomalyThresholdPolicy"/>. A 40 dB signal drop is not a judgement call, so paying an
/// LLM to notice it is waste — the thresholds run first and own the outcome. This executor is a
/// faithful port of the former HeuristicNetworkBatchAnalyzer (group by tower, evaluate in time
/// order, topology null when empty), which is what keeps the pipeline's parity counts identical.
///
/// The §7.2 agent fan-out (IncidentAnalysis ∥ Topology ∥ Energy over the events the thresholds
/// cannot classify) is the enrichment layer that sits on top of this; it must not alter the
/// anomaly/optimization/topology counts the parity contract pins.
/// </summary>
public sealed partial class ThresholdAnalysisExecutor() : Executor("threshold-analysis")
{
    [MessageHandler]
    public ValueTask<AiAnalysisResult> HandleAsync(AnalyzeNetworkBatchRequest request, IWorkflowContext context)
    {
        if (request.Events.Count == 0)
        {
            return ValueTask.FromResult(AiAnalysisResult.Empty);
        }

        var anomalies = new List<DetectedAnomaly>();
        var optimizations = new List<ProposedOptimization>();
        var statusChanges = new List<TowerStatusChange>();
        var metricUpdates = new List<TowerMetricUpdate>();

        foreach (IGrouping<string, NetworkEventSnapshot> tower in
                 request.Events.GroupBy(e => e.TowerCode, StringComparer.OrdinalIgnoreCase))
        {
            AnomalyThresholdPolicy.EvaluateTower(
                tower.Key,
                [.. tower.OrderBy(e => e.OccurredAt)],
                anomalies,
                optimizations,
                statusChanges,
                metricUpdates);
        }

        TopologyDelta? topology = statusChanges.Count == 0 && metricUpdates.Count == 0
            ? null
            : new TopologyDelta(statusChanges, metricUpdates);

        return ValueTask.FromResult(new AiAnalysisResult(anomalies, optimizations, topology));
    }
}

/// <summary>
/// Builds NetworkLogAnalysisWorkflow — the MAF replacement for the Semantic Kernel batch analyzer.
/// </summary>
public sealed class NetworkLogAnalysisWorkflowBuilder
{
    public Workflow Build()
    {
        var threshold = new ThresholdAnalysisExecutor();
        return new WorkflowBuilder(threshold)
            .WithOutputFrom(threshold)
            .Build();
    }
}
