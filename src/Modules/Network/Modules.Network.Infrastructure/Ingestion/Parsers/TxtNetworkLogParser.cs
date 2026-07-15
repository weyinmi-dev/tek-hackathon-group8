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
        contentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("text/tab-separated-values", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase);

    public async Task<Result<NetworkLogParseResult>> ParseAsync(
        Guid ingestionRunId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        const string delimiter = "\t";
        return await DelimitedRowParser.ParseAsync(ingestionRunId, content, delimiter, cancellationToken);
    }
}
