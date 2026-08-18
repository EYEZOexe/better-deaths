namespace BetterDeaths;

public sealed class LeadUpTimingPolicyTests
{
    [Fact]
    public void TimingConstantsRemainBoundedForDeathRecapCapture()
    {
        Assert.Equal(10, LeadUpTimingPolicy.ShortDisplaySeconds);
        Assert.Equal(30, LeadUpTimingPolicy.DefaultDisplaySeconds);
        Assert.Equal(60, LeadUpTimingPolicy.MaximumDisplaySeconds);
        Assert.Equal(70, LeadUpTimingPolicy.CaptureSeconds);
        Assert.Equal(75, LeadUpTimingPolicy.LiveRetentionSeconds);
        Assert.Equal(10, LeadUpTimingPolicy.LateFatalCauseLookbackSeconds);
    }

    [Fact]
    public void CaptureAndRetentionWindowsKeepExistingSafetyMargins()
    {
        Assert.Equal(
            LeadUpTimingPolicy.MaximumDisplaySeconds + 10,
            LeadUpTimingPolicy.CaptureSeconds);
        Assert.Equal(
            LeadUpTimingPolicy.CaptureSeconds + 5,
            LeadUpTimingPolicy.LiveRetentionSeconds);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(30, 30)]
    [InlineData(60, 60)]
    [InlineData(0, 30)]
    [InlineData(15, 30)]
    [InlineData(59, 30)]
    [InlineData(61, 30)]
    [InlineData(120, 30)]
    public void NormalizeDisplaySecondsPreservesOnlySupportedChoices(int input, int expected)
    {
        Assert.Equal(expected, LeadUpTimingPolicy.NormalizeDisplaySeconds(input));
    }
}
