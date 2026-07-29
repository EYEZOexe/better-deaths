namespace BetterDeaths;

public sealed class RecordedPullLookupTests
{
    private static readonly DateTime CapturedAtUtc = new(2026, 7, 29, 12, 5, 0, DateTimeKind.Utc);

    [Fact]
    public void IndexedSummaryRequiresExactDeathReference()
    {
        var expected = new RecordedDeathReference(CapturedAtUtc.AddMinutes(-1).Ticks, 42);
        var summary = CreateSummary() with
        {
            DeathReferences = [expected],
            DeathReferencesIndexed = true,
        };

        Assert.True(RecordedPullLookup.MayContainDeath(summary, expected.SeenAtUtcTicks, expected.MemberKeyHash));
        Assert.False(RecordedPullLookup.MayContainDeath(summary, expected.SeenAtUtcTicks, 41));
        Assert.False(RecordedPullLookup.MayContainDeath(summary, expected.SeenAtUtcTicks + 1, expected.MemberKeyHash));
    }

    [Fact]
    public void IndexedSummaryDoesNotFallBackToBroadTimestampMatch()
    {
        var summary = CreateSummary() with { DeathReferencesIndexed = true };

        Assert.False(RecordedPullLookup.MayContainDeath(summary, CapturedAtUtc.AddMinutes(-1).Ticks, 42));
    }

    [Theory]
    [InlineData(-310, true)]
    [InlineData(-310.001, false)]
    [InlineData(-309.999, true)]
    [InlineData(10, true)]
    [InlineData(10.001, false)]
    public void LegacySummaryUsesBoundedTimestampFallback(double secondsFromCapture, bool expected)
    {
        var summary = CreateSummary();

        Assert.Equal(
            expected,
            RecordedPullLookup.MayContainDeath(
                summary,
                CapturedAtUtc.AddSeconds(secondsFromCapture).Ticks,
                memberKeyHash: 42));
    }

    private static RecordedPullSummary CreateSummary()
    {
        return new RecordedPullSummary(
            CapturedAtUtc,
            "Combat ended",
            1,
            "Duty",
            PullElapsedSeconds: 300,
            DeathCount: 1);
    }
}
