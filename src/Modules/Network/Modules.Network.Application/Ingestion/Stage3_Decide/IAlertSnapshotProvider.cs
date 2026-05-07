namespace Modules.Network.Application.Ingestion.Stage3_Decide;

/// <summary>
/// Cross-module read port: what the decision engine needs to know about live alerts.
/// Defined here (the consumer module) so the Alerts module can adapt to it without
/// Network depending on Alerts.Domain. Implemented by Alerts.Infrastructure in PR 5.
/// </summary>
public interface IAlertSnapshotProvider
{
    Task<IReadOnlyList<AlertSnapshot>> GetActiveAsync(CancellationToken cancellationToken = default);
}
