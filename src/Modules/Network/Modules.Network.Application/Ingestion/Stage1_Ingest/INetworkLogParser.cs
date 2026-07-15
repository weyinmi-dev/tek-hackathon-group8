using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Application.Ingestion.Stage1_Ingest;

/// <summary>
/// Per-format strategy for turning an ingested log file into <see cref="NetworkEvent"/>
/// rows owned by the given <paramref name="ingestionRunId"/>. Implementations live in
/// Network.Infrastructure (they pull third-party file-format libraries); the registry
/// routes incoming files to the right one based on content type + file name.
/// </summary>
public interface INetworkLogParser
{
    /// <summary>
    /// Stable identifier used for diagnostics and registry lookups (e.g. "csv", "json", "xlsx", "txt").
    /// </summary>
    string Format { get; }

    /// <summary>
    /// True if this parser claims responsibility for the given file. The registry calls
    /// every parser in turn and returns the first match — order is determined by registration.
    /// </summary>
    bool CanParse(string contentType, string fileName);

    /// <summary>
    /// Strict parse: any malformed row fails the whole stage with a row-scoped error.
    /// Lenient mode is intentionally not supported in PR 2; bad data should surface
    /// loudly rather than slip silently into the AI stage.
    ///
    /// Row-oriented parsers return <see cref="NetworkLogParseResult.FromEvents"/>. Parsers for
    /// document-shaped feeds additionally return the canonical snapshot they decoded, which
    /// Stage 1 persists and Stage 3 plans the synchronisation from.
    /// </summary>
    Task<Result<NetworkLogParseResult>> ParseAsync(
        Guid ingestionRunId,
        Stream content,
        CancellationToken cancellationToken = default);
}
