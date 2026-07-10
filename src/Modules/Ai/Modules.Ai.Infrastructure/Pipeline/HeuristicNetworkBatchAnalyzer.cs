using Modules.Network.Application.Ingestion.Stage2_Analyze;
using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Pipeline;

/// <summary>
/// Deterministic, dependency-free analyzer used when Azure OpenAI is not configured
/// (the same fallback shape <c>MockCopilotOrchestrator</c> uses for chat). Groups the
/// raw events per tower and delegates classification to the Network module's
/// <see cref="AnomalyThresholdPolicy"/> — the thresholds are business rules and live in
/// the Network module, not here. Same input always yields the same output.
///
/// This shell is slated for deletion (Phase 3 M12): once the MAF pipeline runs, the
/// threshold pre-filter calls <see cref="AnomalyThresholdPolicy"/> directly. Until then it
/// keeps offline mode working, and the characterization harness pins its output.
/// </summary>
internal sealed class HeuristicNetworkBatchAnalyzer : INetworkBatchAnalyzer
{
    public Task<Result<AiAnalysisResult>> AnalyzeAsync(
        Guid ingestionRunId,
        IReadOnlyList<NetworkEvent> events,
        string? mcpFilePath = null,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return Task.FromResult(Result.Success(AiAnalysisResult.Empty));
        }

        IEnumerable<IGrouping<string, NetworkEvent>> byTower = events
            .GroupBy(e => e.TowerCode, StringComparer.OrdinalIgnoreCase);

        var anomalies = new List<DetectedAnomaly>();
        var optimizations = new List<ProposedOptimization>();
        var statusChanges = new List<TowerStatusChange>();
        var metricUpdates = new List<TowerMetricUpdate>();

        foreach (IGrouping<string, NetworkEvent> tower in byTower)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AnomalyThresholdPolicy.EvaluateTower(
                tower.Key,
                [.. tower.OrderBy(e => e.OccurredAt)],
                anomalies, optimizations, statusChanges, metricUpdates);
        }

        TopologyDelta? topology = (statusChanges.Count == 0 && metricUpdates.Count == 0)
            ? null
            : new TopologyDelta(statusChanges, metricUpdates);

        return Task.FromResult(Result.Success(new AiAnalysisResult(anomalies, optimizations, topology)));
    }
}
