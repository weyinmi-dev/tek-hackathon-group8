using ClosedXML.Excel;
using FluentAssertions;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Infrastructure.Ingestion.Parsers;
using SharedKernel;
using Xunit;
using static Modules.Network.UnitTests.Ingestion.Parsers.ParserTestHelpers;

namespace Modules.Network.UnitTests.Ingestion.Parsers;

public sealed class XlsxNetworkLogParserTests
{
    private readonly XlsxNetworkLogParser _parser = new();

    [Theory]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ops.xlsx", true)]
    [InlineData("application/octet-stream", "ops.XLSX", true)]
    [InlineData("text/csv", "ops.csv", false)]
    public void CanParse_RoutesByContentTypeOrExtension(string contentType, string fileName, bool expected) =>
        _parser.CanParse(contentType, fileName).Should().Be(expected);

    [Fact]
    public async Task ParseAsync_HappyPath_ReadsFirstWorksheet()
    {
        using MemoryStream xlsx = BuildWorkbook(rows:
        [
            ["timestamp", "tower_code", "signal_pct", "load_pct", "latency_ms", "status"],
            ["2026-05-05T08:00:00Z", "LOS-T-014", "98", "42", "18", "OK"],
            ["2026-05-05T08:05:00Z", "LOS-T-014", "34", "93", "118", "Critical"]
        ]);

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, xlsx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].SignalPct.Should().Be(98);
        result.Value[1].RawStatus.Should().Be("Critical");
    }

    [Fact]
    public async Task ParseAsync_AcceptsExcelDateCells()
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add("ops");
        sheet.Cell(1, 1).Value = "timestamp";
        sheet.Cell(1, 2).Value = "tower_code";
        sheet.Cell(2, 1).Value = new DateTime(2026, 5, 5, 8, 0, 0, DateTimeKind.Utc);
        sheet.Cell(2, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
        sheet.Cell(2, 2).Value = "LOS-T-014";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, ms, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].OccurredAt.Should().Be(new DateTimeOffset(2026, 5, 5, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ParseAsync_RejectsMissingRequiredColumn()
    {
        using MemoryStream xlsx = BuildWorkbook(rows:
        [
            ["timestamp"],
            ["2026-05-05T08:00:00Z"]
        ]);

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, xlsx, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.MissingColumn");
    }

    [Fact]
    public async Task ParseAsync_RejectsCorruptedFile()
    {
        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, Utf8Stream("not really xlsx"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.MalformedFile");
    }

    [Fact]
    public async Task ParseAsync_HeaderOnlyFileFailsAsEmpty()
    {
        using MemoryStream xlsx = BuildWorkbook(rows:
        [
            ["timestamp", "tower_code"]
        ]);

        Result<IReadOnlyList<NetworkEvent>> result =
            await _parser.ParseAsync(SampleRunId, xlsx, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.EmptyFile");
    }

    private static MemoryStream BuildWorkbook(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add("ops");
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < rows[r].Count; c++)
            {
                sheet.Cell(r + 1, c + 1).Value = rows[r][c];
            }
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }
}
