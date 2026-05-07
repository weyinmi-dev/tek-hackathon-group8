using FluentAssertions;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Infrastructure.Ingestion.Parsers;
using SharedKernel;
using Xunit;
using static Modules.Network.UnitTests.Ingestion.Parsers.ParserTestHelpers;

namespace Modules.Network.UnitTests.Ingestion.Parsers;

public sealed class JsonNetworkLogParserTests
{
    private readonly JsonNetworkLogParser _parser = new();

    [Theory]
    [InlineData("application/json", "ops.json", true)]
    [InlineData("application/octet-stream", "ops.JSON", true)]
    [InlineData("text/csv", "ops.csv", false)]
    public void CanParse_RoutesByContentTypeOrExtension(string contentType, string fileName, bool expected) =>
        _parser.CanParse(contentType, fileName).Should().Be(expected);

    [Fact]
    public async Task ParseAsync_TopLevelArray_HappyPath()
    {
        const string json = """
        [
            { "timestamp": "2026-05-05T08:00:00Z", "tower_code": "LOS-T-014", "signal_pct": 98, "load_pct": 42, "latency_ms": 18, "status": "OK" },
            { "timestamp": "2026-05-05T08:05:00Z", "tower_code": "ABV-T-007", "signal_pct": 80, "load_pct": 60 }
        ]
        """;

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].TowerCode.Should().Be("LOS-T-014");
        result.Value[0].SignalPct.Should().Be(98);
        result.Value[0].LatencyMs.Should().Be(18);
        result.Value[1].LatencyMs.Should().BeNull();
        result.Value[1].RawStatus.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_EnvelopeWithEventsArray()
    {
        const string json = """
        {
            "uploadedBy": "ops",
            "events": [
                { "timestamp": "2026-05-05T08:00:00Z", "tower_code": "LOS-T-014" }
            ]
        }
        """;

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task ParseAsync_AcceptsNumericOrStringForOptionalFields()
    {
        const string json = """
        [
            { "timestamp": "2026-05-05T08:00:00Z", "tower_code": "LOS-T-014", "signal_pct": "98" }
        ]
        """;

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].SignalPct.Should().Be(98);
    }

    [Fact]
    public async Task ParseAsync_RejectsMalformedJson()
    {
        const string json = "{ this isn't json";

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.MalformedFile");
    }

    [Fact]
    public async Task ParseAsync_RejectsTopLevelScalar()
    {
        const string json = "\"just a string\"";

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.MalformedFile");
    }

    [Fact]
    public async Task ParseAsync_RejectsEmptyArray()
    {
        const string json = "[]";

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.EmptyFile");
    }

    [Fact]
    public async Task ParseAsync_RejectsRowMissingTowerCode()
    {
        const string json = """
        [ { "timestamp": "2026-05-05T08:00:00Z" } ]
        """;

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.MalformedRow");
        result.Error.Description.Should().Contain("Row 1");
        result.Error.Description.Should().Contain("tower_code");
    }

    [Fact]
    public async Task ParseAsync_PreservesRawPayload()
    {
        const string json = """
        [ { "timestamp": "2026-05-05T08:00:00Z", "tower_code": "LOS-T-014", "extra": "field" } ]
        """;

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].RawPayload.Should().NotBeNull();
        result.Value[0].RawPayload.Should().Contain("extra");
    }
}
