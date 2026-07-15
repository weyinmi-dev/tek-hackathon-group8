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
        IReadOnlyDictionary<string, TowerSnapshot> currentTowers,

        /// <summary>
        /// The snapshot immediately preceding each of these, by site code. Passed in rather than
        /// fetched so the planner stays pure — and it is needed, because the anomaly rules that
        /// matter most are about *change*: fuel that fell while the generator was off is theft, and
        /// you cannot see that in a single reading.
        /// </summary>
        IReadOnlyDictionary<string, SiteSnapshotPayload> previousBySite);
}
