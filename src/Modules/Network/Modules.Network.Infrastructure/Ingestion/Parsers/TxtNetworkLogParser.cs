using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Infrastructure.Ingestion.Parsers;

/// <summary>
/// Tab-delimited variant of the CSV parser. Same canonical headers, same field
/// validators — only the cell separator differs.
/// </summary>
internal sealed class TxtNetworkLogParser : INetworkLogParser
{
    public string Format => "txt";

    public bool CanParse(string contentType, string fileName) =>
        contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals("text/tab-separated-values", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase);

    public Task<Result<IReadOnlyList<NetworkEvent>>> ParseAsync(
        Guid ingestionRunId,
        Stream content,
        CancellationToken cancellationToken = default) =>
        DelimitedRowParser.ParseAsync(ingestionRunId, content, delimiter: "\t", cancellationToken);
}
