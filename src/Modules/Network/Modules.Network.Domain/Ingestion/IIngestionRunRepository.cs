namespace Modules.Network.Domain.Ingestion;

public interface IIngestionRunRepository
{
    Task<IngestionRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whole-file idempotency lookup. Returning a non-null run means the same
    /// content has already been ingested and the orchestrator should short-circuit
    /// without dispatching the stages.
    /// </summary>
    Task<IngestionRun?> GetByContentHashAsync(string contentHash, CancellationToken cancellationToken = default);

    Task AddAsync(IngestionRun run, CancellationToken cancellationToken = default);

    Task AddEventsAsync(IEnumerable<NetworkEvent> events, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NetworkEvent>> ListEventsAsync(Guid ingestionRunId, CancellationToken cancellationToken = default);
}
