namespace Modules.Network.Application.Ingestion.Stage3_Decide;

/// <summary>
/// Read port for current tower state. Returns a dictionary keyed by tower code
/// (case-insensitive) so the decision engine's lookups are O(1).
/// </summary>
public interface ITowerSnapshotProvider
{
    Task<IReadOnlyDictionary<string, TowerSnapshot>> GetCurrentAsync(CancellationToken cancellationToken = default);
}
