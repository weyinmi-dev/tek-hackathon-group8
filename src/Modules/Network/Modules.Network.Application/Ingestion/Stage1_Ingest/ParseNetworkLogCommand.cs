using Application.Abstractions.Messaging;
using Application.Abstractions.Pipeline;

namespace Modules.Network.Application.Ingestion.Stage1_Ingest;

/// <summary>
/// Stage 1 of the ingestion pipeline. Parses an in-memory log stream into
/// <see cref="Modules.Network.Domain.Ingestion.NetworkEvent"/> rows owned by the given
/// <paramref name="IngestionRunId"/> and persists them. Returns the number of events parsed.
///
/// Pre-condition: the IngestionRun must already exist and be in
/// <see cref="Modules.Network.Domain.Ingestion.IngestionStatus.Parsing"/>. The orchestrator
/// (PR 6) owns status transitions; this handler is intentionally not allowed to change them.
/// </summary>
public sealed record ParseNetworkLogCommand(
    Guid IngestionRunId,
    string ContentType,
    string FileName,
    Stream Content) : ICommand<int>, IIngestionPipelineRequest
{
    public string StageName => "Parse";
}
