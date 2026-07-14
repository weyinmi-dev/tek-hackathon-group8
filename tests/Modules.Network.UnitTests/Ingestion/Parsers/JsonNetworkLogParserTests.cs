using Modules.Network.Application.Ingestion.Stage1_Ingest;
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

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Events.Should().HaveCount(2);
        parsed.Value.Events[0].TowerCode.Should().Be("LOS-T-014");
        parsed.Value.Events[0].SignalPct.Should().Be(98);
        parsed.Value.Events[0].LatencyMs.Should().Be(18);
        parsed.Value.Events[1].LatencyMs.Should().BeNull();
        parsed.Value.Events[1].RawStatus.Should().BeNull();
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

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task ParseAsync_AcceptsNumericOrStringForOptionalFields()
    {
        const string json = """
        [
            { "timestamp": "2026-05-05T08:00:00Z", "tower_code": "LOS-T-014", "signal_pct": "98" }
        ]
        """;

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Events[0].SignalPct.Should().Be(98);
    }

    [Fact]
    public async Task ParseAsync_RejectsMalformedJson()
    {
        const string json = "{ this isn't json";

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        parsed.IsFailure.Should().BeTrue();
        parsed.Error.Code.Should().Be("Network.Ingestion.MalformedFile");
    }

    [Fact]
    public async Task ParseAsync_RejectsTopLevelScalar()
    {
        const string json = "\"just a string\"";

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        parsed.IsFailure.Should().BeTrue();
        parsed.Error.Code.Should().Be("Network.Ingestion.MalformedFile");
    }

    [Fact]
    public async Task ParseAsync_RejectsEmptyArray()
    {
        const string json = "[]";

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        parsed.IsFailure.Should().BeTrue();
        parsed.Error.Code.Should().Be("Network.Ingestion.EmptyFile");
    }

    [Fact]
    public async Task ParseAsync_RejectsRowMissingTowerCode()
    {
        const string json = """
        [ { "timestamp": "2026-05-05T08:00:00Z" } ]
        """;

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        parsed.IsFailure.Should().BeTrue();
        parsed.Error.Code.Should().Be("Network.Ingestion.MalformedRow");
        parsed.Error.Description.Should().Contain("Row 1");
        parsed.Error.Description.Should().Contain("tower_code");
    }

    [Fact]
    public async Task ParseAsync_PreservesRawPayload()
    {
        const string json = """
        [ { "timestamp": "2026-05-05T08:00:00Z", "tower_code": "LOS-T-014", "extra": "field" } ]
        """;

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(json), CancellationToken.None);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Events[0].RawPayload.Should().NotBeNull();
        parsed.Value.Events[0].RawPayload.Should().Contain("extra");
    }
}
