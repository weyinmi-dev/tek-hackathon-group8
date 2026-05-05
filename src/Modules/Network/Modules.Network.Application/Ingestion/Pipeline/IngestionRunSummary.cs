using Modules.Network.Domain.Ingestion;

namespace Modules.Network.Application.Ingestion.Pipeline;

public sealed record IngestionRunSummary(
    Guid IngestionRunId,
    string ContentHash,
    IngestionStatus FinalStatus,
    int EventsParsed,
    int AnomaliesDetected,
    int AlertsCreated,
    int AlertsUpdated,
    int OptimizationsCreated,
    bool TopologyChanged,
    bool DeduplicatedFromPriorRun,
    IReadOnlyList<StageTiming> StageTimings,
    string? FailureReason);
