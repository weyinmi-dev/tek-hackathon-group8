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

        // First pass: let each parser inspect the content type and file name.
        foreach (INetworkLogParser parser in _parsers)
        {
            if (parser.CanParse(contentType, fileName))
            {
                return Result.Success(parser);
            }
        }

        // Fallback: content-type from uploads can be unreliable (empty or
        // "application/octet-stream"). Try a conservative extension-based
        // match so common uploads (.csv, .json, .txt, .log) still work.
        try
        {
            string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            if (!string.IsNullOrEmpty(ext))
            {
                foreach (INetworkLogParser parser in _parsers)
                {
                    switch (parser.Format)
                    {
                        case "csv" when ext == ".csv":
                        case "json" when ext == ".json" || ext == ".jsonl":
                        case "txt" when ext == ".txt" || ext == ".tsv" || ext == ".log":
                        case "xlsx" when ext == ".xlsx":
                            return Result.Success(parser);
                    }
                }
            }
        }
        catch
        {
            // Ignore any IO/Path surprises and fall through to the generic error.
        }

        return Result.Failure<INetworkLogParser>(
            NetworkLogErrors.UnsupportedFormat(contentType, fileName));
    }
}
