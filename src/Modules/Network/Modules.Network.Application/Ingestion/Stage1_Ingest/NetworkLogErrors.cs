using SharedKernel;

namespace Modules.Network.Application.Ingestion.Stage1_Ingest;

/// <summary>
/// Centralised error codes for Stage 1. Keeps the codes consistent across the
/// CSV/JSON/XLSX/TXT parsers so callers can switch on them without grepping.
/// </summary>
public static class NetworkLogErrors
{
    public static Error UnsupportedFormat(string contentType, string fileName) =>
        Error.NotFound(
            "Network.Ingestion.UnsupportedFormat",
            $"No parser registered for content type '{contentType}' / file '{fileName}'.");

    public static Error EmptyFile() =>
        Error.Problem(
            "Network.Ingestion.EmptyFile",
            "Uploaded file contained no rows.");

    public static Error MissingColumn(string column) =>
        Error.Problem(
            "Network.Ingestion.MissingColumn",
            $"Required column '{column}' is missing from the file header.");

    public static Error MalformedRow(int rowNumber, string reason) =>
        Error.Problem(
            "Network.Ingestion.MalformedRow",
            $"Row {rowNumber}: {reason}");

    public static Error MalformedFile(string reason) =>
        Error.Problem(
            "Network.Ingestion.MalformedFile",
            reason);
}
