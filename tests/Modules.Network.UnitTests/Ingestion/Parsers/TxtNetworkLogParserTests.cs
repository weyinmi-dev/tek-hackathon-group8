using FluentAssertions;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Infrastructure.Ingestion.Parsers;
using SharedKernel;
using Xunit;
using static Modules.Network.UnitTests.Ingestion.Parsers.ParserTestHelpers;

namespace Modules.Network.UnitTests.Ingestion.Parsers;

public sealed class TxtNetworkLogParserTests
{
    private readonly TxtNetworkLogParser _parser = new();

    [Theory]
    [InlineData("text/plain", "log.txt", true)]
    [InlineData("text/tab-separated-values", "log.tsv", true)]
    [InlineData("text/csv", "log.csv", false)]
    public void CanParse_RoutesByContentTypeOrExtension(string contentType, string fileName, bool expected) =>
        _parser.CanParse(contentType, fileName).Should().Be(expected);

    [Fact]
    public async Task ParseAsync_TabDelimitedHappyPath()
    {
        string txt =
            "timestamp\ttower_code\tsignal_pct\tload_pct\tlatency_ms\tstatus\n" +
            "2026-05-05T08:00:00Z\tLOS-T-014\t98\t42\t18\tOK\n" +
            "2026-05-05T08:05:00Z\tLOS-T-014\t34\t93\t118\tCritical\n";

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(txt), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[1].RawStatus.Should().Be("Critical");
        result.Value[1].SignalPct.Should().Be(34);
    }

    [Fact]
    public async Task ParseAsync_RejectsCommaDelimitedInput()
    {
        // Same content but comma-delimited — should fail because no tab found means
        // header parses as a single column and required tower_code is missing.
        const string txt =
            "timestamp,tower_code\n" +
            "2026-05-05T08:00:00Z,LOS-T-014\n";

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, Utf8Stream(txt), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.MissingColumn");
    }
}
