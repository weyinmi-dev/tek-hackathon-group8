using Modules.Network.Application.Ingestion.Stage1_Ingest;
using FluentAssertions;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Infrastructure.Ingestion.Parsers;
using SharedKernel;
using Xunit;
using static Modules.Network.UnitTests.Ingestion.Parsers.ParserTestHelpers;

namespace Modules.Network.UnitTests.Ingestion.Parsers;

public sealed class CsvNetworkLogParserTests
{
    private readonly CsvNetworkLogParser _parser = new();

    [Theory]
    [InlineData("text/csv", "ops.csv", true)]
    [InlineData("application/octet-stream", "ops.CSV", true)]
    [InlineData("application/json", "ops.json", false)]
    [InlineData("text/plain", "ops.txt", false)]
    public void CanParse_RoutesByContentTypeOrExtension(string contentType, string fileName, bool expected) =>
        _parser.CanParse(contentType, fileName).Should().Be(expected);

    [Fact]
    public async Task ParseAsync_HappyPath_ReturnsAllRowsWithNormalisedFields()
    {
        const string csv =
            "timestamp,tower_code,signal_pct,load_pct,latency_ms,status\n" +
            "2026-05-05T08:00:00Z,LOS-T-014,98,42,18,OK\n" +
            "2026-05-05T08:05:00Z,los-t-014,71,87,42,Degraded\n";

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(csv), CancellationToken.None);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Events.Should().HaveCount(2);

        NetworkEvent first = parsed.Value.Events[0];
        first.IngestionRunId.Should().Be(SampleRunId);
        first.TowerCode.Should().Be("LOS-T-014");
        first.SignalPct.Should().Be(98);
        first.LoadPct.Should().Be(42);
        first.LatencyMs.Should().Be(18);
        first.RawStatus.Should().Be("OK");
        first.OccurredAt.Should().Be(DateTimeOffset.Parse("2026-05-05T08:00:00Z"));

        // Lower-case input gets uppercased by NetworkEvent.Create
        parsed.Value.Events[1].TowerCode.Should().Be("LOS-T-014");
    }

    [Fact]
    public async Task ParseAsync_AcceptsMissingOptionalColumns()
    {
        const string csv =
            "timestamp,tower_code\n" +
            "2026-05-05T08:00:00Z,LOS-T-014\n";

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(csv), CancellationToken.None);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Events.Should().HaveCount(1);
        parsed.Value.Events[0].SignalPct.Should().BeNull();
        parsed.Value.Events[0].LoadPct.Should().BeNull();
        parsed.Value.Events[0].LatencyMs.Should().BeNull();
        parsed.Value.Events[0].RawStatus.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_RejectsMissingRequiredColumn()
    {
        const string csv =
            "timestamp,signal_pct\n" +
            "2026-05-05T08:00:00Z,98\n";

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(csv), CancellationToken.None);

        parsed.IsFailure.Should().BeTrue();
        parsed.Error.Code.Should().Be("Network.Ingestion.MissingColumn");
        parsed.Error.Description.Should().Contain("tower_code");
    }

    [Fact]
    public async Task ParseAsync_RejectsMalformedTimestampWithRowNumber()
    {
        const string csv =
            "timestamp,tower_code\n" +
            "2026-05-05T08:00:00Z,LOS-T-014\n" +
            "not-a-date,LOS-T-014\n";

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(csv), CancellationToken.None);

        parsed.IsFailure.Should().BeTrue();
        parsed.Error.Code.Should().Be("Network.Ingestion.MalformedRow");
        parsed.Error.Description.Should().Contain("Row 3");
        parsed.Error.Description.Should().Contain("not-a-date");
    }

    [Theory]
    [InlineData("signal_pct", "150", "outside 0..100")]
    [InlineData("load_pct", "-5", "outside 0..100")]
    [InlineData("signal_pct", "abc", "non-integer")]
    public async Task ParseAsync_RejectsOutOfRangeOrNonNumericPercents(string column, string badValue, string expectedReason)
    {
        string csv =
            $"timestamp,tower_code,{column}\n" +
            $"2026-05-05T08:00:00Z,LOS-T-014,{badValue}\n";

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(csv), CancellationToken.None);

        parsed.IsFailure.Should().BeTrue();
        parsed.Error.Code.Should().Be("Network.Ingestion.MalformedRow");
        parsed.Error.Description.Should().Contain(expectedReason);
    }

    [Fact]
    public async Task ParseAsync_RejectsNegativeLatency()
    {
        const string csv =
            "timestamp,tower_code,latency_ms\n" +
            "2026-05-05T08:00:00Z,LOS-T-014,-1\n";

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(csv), CancellationToken.None);

        parsed.IsFailure.Should().BeTrue();
        parsed.Error.Description.Should().Contain("cannot be negative");
    }

    [Fact]
    public async Task ParseAsync_HeaderOnlyFileFailsAsEmpty()
    {
        const string csv = "timestamp,tower_code\n";

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(csv), CancellationToken.None);

        parsed.IsFailure.Should().BeTrue();
        parsed.Error.Code.Should().Be("Network.Ingestion.EmptyFile");
    }

    [Fact]
    public async Task ParseAsync_TotallyEmptyStreamFailsAsEmpty()
    {
        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(""), CancellationToken.None);

        parsed.IsFailure.Should().BeTrue();
        parsed.Error.Code.Should().Be("Network.Ingestion.EmptyFile");
    }

    [Fact]
    public async Task ParseAsync_AcceptsHeaderOrderingPermutations()
    {
        const string csv =
            "status,signal_pct,latency_ms,tower_code,load_pct,timestamp\n" +
            "OK,98,18,LOS-T-014,42,2026-05-05T08:00:00Z\n";

        Result<NetworkLogParseResult> parsed =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(csv), CancellationToken.None);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Events[0].SignalPct.Should().Be(98);
        parsed.Value.Events[0].TowerCode.Should().Be("LOS-T-014");
    }
}
