using Application.Abstractions.Pipeline;
using FluentAssertions;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Infrastructure.Ingestion;
using Modules.Network.Infrastructure.Ingestion.Parsers;
using SharedKernel;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Parsers;

public sealed class NetworkLogParserRegistryTests
{
    private readonly NetworkLogParserRegistry _registry = new(
    [
        new CsvNetworkLogParser(),
        new JsonNetworkLogParser(new SnapshotCalibrationOptions()),
        new XlsxNetworkLogParser(),
        new TxtNetworkLogParser()
    ]);

    [Theory]
    [InlineData("text/csv", "ops.csv", "csv")]
    [InlineData("application/json", "ops.json", "json")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ops.xlsx", "xlsx")]
    [InlineData("text/plain", "ops.txt", "txt")]
    public void Resolve_PicksCorrectParserByContentTypeOrExtension(string contentType, string fileName, string expectedFormat)
    {
        Result<INetworkLogParser> result = _registry.Resolve(contentType, fileName);

        result.IsSuccess.Should().BeTrue();
        result.Value.Format.Should().Be(expectedFormat);
    }

    [Fact]
    public void Resolve_ReturnsNotFoundWhenNoParserMatches()
    {
        Result<INetworkLogParser> result = _registry.Resolve("application/zip", "ops.zip");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.UnsupportedFormat");
        result.Error.Description.Should().Contain("ops.zip");
    }

    [Fact]
    public void Resolve_RejectsNullArguments()
    {
        Action a = () => _registry.Resolve(contentType: null!, fileName: "x.csv");
        Action b = () => _registry.Resolve(contentType: "text/csv", fileName: null!);

        a.Should().Throw<ArgumentNullException>();
        b.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Resolve_PicksByExtensionWhenContentTypeIsGeneric()
    {
        // Many uploaders send application/octet-stream regardless of file type.
        // The registry must fall back to the file name extension.
        Result<INetworkLogParser> result = _registry.Resolve("application/octet-stream", "metrics.json");

        result.IsSuccess.Should().BeTrue();
        result.Value.Format.Should().Be("json");
    }
}
