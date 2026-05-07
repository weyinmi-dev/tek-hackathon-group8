using FluentAssertions;
using Modules.Network.Domain.Ingestion;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion;

public sealed class FingerprintsTests
{
    [Fact]
    public void ContentHash_IsDeterministic_ForSameBytes()
    {
        byte[] payload = "the quick brown fox"u8.ToArray();

        string a = Fingerprints.ContentHash(payload);
        string b = Fingerprints.ContentHash(payload);

        a.Should().Be(b);
        a.Should().HaveLength(64);
        a.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public void ContentHash_DiffersForDifferentBytes()
    {
        string a = Fingerprints.ContentHash("a"u8.ToArray());
        string b = Fingerprints.ContentHash("b"u8.ToArray());

        a.Should().NotBe(b);
    }

    [Fact]
    public void AnomalyFingerprint_NormalisesTowerCodeCasing()
    {
        var ts = DateTimeOffset.Parse("2026-05-05T08:07:00Z");

        string lower = Fingerprints.AnomalyFingerprint("los-t-014", "SignalDrop", ts);
        string upper = Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", ts);
        string padded = Fingerprints.AnomalyFingerprint("  LOS-T-014  ", "SignalDrop", ts);

        lower.Should().Be(upper);
        lower.Should().Be(padded);
    }

    [Fact]
    public void AnomalyFingerprint_NormalisesAnomalyTypeCasing()
    {
        var ts = DateTimeOffset.Parse("2026-05-05T08:07:00Z");

        string a = Fingerprints.AnomalyFingerprint("LOS-T-014", "signaldrop", ts);
        string b = Fingerprints.AnomalyFingerprint("LOS-T-014", "SIGNALDROP", ts);

        a.Should().Be(b);
    }

    [Theory]
    [InlineData("2026-05-05T08:00:00Z", "2026-05-05T08:14:59Z")] // both inside the same 15-min bucket starting 08:00
    [InlineData("2026-05-05T08:00:01Z", "2026-05-05T08:14:30Z")]
    public void AnomalyFingerprint_CollapsesTimestampsWithinSameBucket(string firstIso, string secondIso)
    {
        var first = DateTimeOffset.Parse(firstIso);
        var second = DateTimeOffset.Parse(secondIso);

        string a = Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", first);
        string b = Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", second);

        a.Should().Be(b);
    }

    [Fact]
    public void AnomalyFingerprint_DiffersAcrossBucketBoundary()
    {
        var bucketEnd = DateTimeOffset.Parse("2026-05-05T08:14:59Z");
        var nextBucket = DateTimeOffset.Parse("2026-05-05T08:15:00Z");

        string a = Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", bucketEnd);
        string b = Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", nextBucket);

        a.Should().NotBe(b);
    }

    [Fact]
    public void AnomalyFingerprint_IsTimezoneIndependent()
    {
        var utc = new DateTimeOffset(2026, 5, 5, 8, 7, 0, TimeSpan.Zero);
        var withOffset = new DateTimeOffset(2026, 5, 5, 9, 7, 0, TimeSpan.FromHours(1)); // same instant

        string a = Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", utc);
        string b = Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", withOffset);

        a.Should().Be(b);
    }

    [Fact]
    public void AnomalyFingerprint_DiffersForDifferentTowers()
    {
        var ts = DateTimeOffset.Parse("2026-05-05T08:07:00Z");

        string a = Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", ts);
        string b = Fingerprints.AnomalyFingerprint("ABV-T-007", "SignalDrop", ts);

        a.Should().NotBe(b);
    }

    [Fact]
    public void AnomalyFingerprint_DiffersForDifferentAnomalyTypes()
    {
        var ts = DateTimeOffset.Parse("2026-05-05T08:07:00Z");

        string a = Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", ts);
        string b = Fingerprints.AnomalyFingerprint("LOS-T-014", "LoadSpike", ts);

        a.Should().NotBe(b);
    }

    [Fact]
    public void AnomalyFingerprint_RespectsCustomBucketSize()
    {
        var baseTs = DateTimeOffset.Parse("2026-05-05T08:00:00Z");
        var later = DateTimeOffset.Parse("2026-05-05T08:20:00Z");

        // 15-min default → different buckets
        Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", baseTs)
            .Should().NotBe(Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", later));

        // 1-hour custom bucket → same bucket
        Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", baseTs, TimeSpan.FromHours(1))
            .Should().Be(Fingerprints.AnomalyFingerprint("LOS-T-014", "SignalDrop", later, TimeSpan.FromHours(1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnomalyFingerprint_RejectsBlankTowerCode(string? towerCode)
    {
        Action act = () => Fingerprints.AnomalyFingerprint(towerCode!, "SignalDrop", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnomalyFingerprint_RejectsNonPositiveBucket()
    {
        Action act = () => Fingerprints.AnomalyFingerprint(
            "LOS-T-014", "SignalDrop", DateTimeOffset.UtcNow, TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
