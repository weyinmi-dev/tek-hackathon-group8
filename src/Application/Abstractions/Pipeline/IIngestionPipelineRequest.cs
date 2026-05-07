namespace Application.Abstractions.Pipeline;

/// <summary>
/// Marker for any MediatR request that participates in the deterministic network-ops
/// ingestion pipeline. Picked up by <c>PipelineStageTracingBehavior</c>, which logs a
/// scoped entry/exit for every stage transition keyed by <see cref="IngestionRunId"/>.
/// </summary>
public interface IIngestionPipelineRequest
{
    Guid IngestionRunId { get; }
    string StageName { get; }
}
