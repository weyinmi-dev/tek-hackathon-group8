using Modules.Network.Application.Ingestion.Stage1_Ingest;
using SharedKernel;

namespace Modules.Network.Infrastructure.Ingestion;

internal sealed class NetworkLogParserRegistry(IEnumerable<INetworkLogParser> parsers) : INetworkLogParserRegistry
{
    private readonly IReadOnlyList<INetworkLogParser> _parsers = parsers.ToList();

    public Result<INetworkLogParser> Resolve(string contentType, string fileName)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(fileName);

        foreach (INetworkLogParser parser in _parsers)
        {
            if (parser.CanParse(contentType, fileName))
            {
                return Result.Success(parser);
            }
        }

        return Result.Failure<INetworkLogParser>(
            NetworkLogErrors.UnsupportedFormat(contentType, fileName));
    }
}
