namespace BetterDeaths;

public sealed class ReplaySampleSelectionTests
{
    private static readonly DateTime Origin = new(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan TrailingHold = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan LeadingHold = TimeSpan.FromSeconds(0.75);
    private static readonly TimeSpan MaximumInterpolationGap = TimeSpan.FromSeconds(2);

    [Fact]
    public void InterpolatesAcrossContinuousSamples()
    {
        var selection = Select(0, 1, 0.25);

        Assert.Equal(ReplaySampleSelectionKind.Interpolate, selection.Kind);
        Assert.Equal(0.25f, selection.Interpolation, precision: 3);
    }

    [Fact]
    public void HidesActorInMiddleOfTrackingDiscontinuity()
    {
        var selection = Select(0, 10, 5);

        Assert.Equal(ReplaySampleSelectionKind.None, selection.Kind);
    }

    [Fact]
    public void BrieflyHoldsPreviousActorAtStartOfDiscontinuity()
    {
        var selection = Select(0, 10, 1.5);

        Assert.Equal(ReplaySampleSelectionKind.Previous, selection.Kind);
    }

    [Fact]
    public void UsesUpcomingActorOnlyInsideLeadWindow()
    {
        Assert.Equal(ReplaySampleSelectionKind.None, Select(0, 10, 9.249).Kind);
        Assert.Equal(ReplaySampleSelectionKind.Next, Select(0, 10, 9.25).Kind);
    }

    [Fact]
    public void HidesExpiredTrailingSampleWithoutNextSample()
    {
        var selection = ReplaySampleSelection.Select(
            Origin,
            null,
            Origin.AddSeconds(1.501),
            TrailingHold,
            LeadingHold,
            MaximumInterpolationGap);

        Assert.Equal(ReplaySampleSelectionKind.None, selection.Kind);
    }

    [Fact]
    public void UsesNearbyFirstSample()
    {
        var selection = ReplaySampleSelection.Select(
            null,
            Origin.AddSeconds(0.75),
            Origin,
            TrailingHold,
            LeadingHold,
            MaximumInterpolationGap);

        Assert.Equal(ReplaySampleSelectionKind.Next, selection.Kind);
    }

    private static ReplaySampleSelection Select(double previousSeconds, double nextSeconds, double selectedSeconds)
    {
        return ReplaySampleSelection.Select(
            Origin.AddSeconds(previousSeconds),
            Origin.AddSeconds(nextSeconds),
            Origin.AddSeconds(selectedSeconds),
            TrailingHold,
            LeadingHold,
            MaximumInterpolationGap);
    }
}
