using SharedKernel;

namespace Modules.Network.Application.Ingestion.Stage1_Ingest;

public interface INetworkLogParserRegistry
{
    /// <summary>
    /// Picks the first registered parser whose <see cref="INetworkLogParser.CanParse"/> returns
    /// true for the given inputs. Returns a NotFound failure when no parser matches.
    /// </summary>
    Result<INetworkLogParser> Resolve(string contentType, string fileName);
}
