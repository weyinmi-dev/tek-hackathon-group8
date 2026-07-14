using Application.Abstractions.Pipeline;

namespace Modules.Network.Application.Ingestion.Stage3_Decide;

/// <summary>
/// Plans the synchronisation actions for a batch of reported site snapshots. The snapshot
/// counterpart of <see cref="IDecisionEngine"/>; both are pure and both feed the same Stage-4
/// executor.
/// </summary>
public interface ISiteSnapshotPlanner
{
    IReadOnlyList<PipelineAction> Plan(
        IReadOnlyList<SiteSnapshotPayload> snapshots,
        IReadOnlyList<AlertSnapshot> activeAlerts,
        IReadOnlyDictionary<string, TowerSnapshot> currentTowers);
}
