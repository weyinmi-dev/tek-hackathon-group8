using System.Text;

namespace Modules.Network.UnitTests.Ingestion.Parsers;

internal static class ParserTestHelpers
{
    public static MemoryStream Utf8Stream(string contents) =>
        new(Encoding.UTF8.GetBytes(contents));

    public static readonly Guid SampleRunId = Guid.Parse("11111111-2222-3333-4444-555555555555");
}
