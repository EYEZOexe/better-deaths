namespace BetterDeaths;

internal static class LeadUpTimingPolicy
{
    public const int ShortDisplaySeconds = 10;
    public const int DefaultDisplaySeconds = 30;
    public const int MaximumDisplaySeconds = 60;
    public const int CaptureSeconds = MaximumDisplaySeconds + 10;
    public const int LiveRetentionSeconds = CaptureSeconds + 5;
    public const int LateFatalCauseLookbackSeconds = 10;

    public static int NormalizeDisplaySeconds(int seconds)
    {
        return seconds switch
        {
            ShortDisplaySeconds => ShortDisplaySeconds,
            MaximumDisplaySeconds => MaximumDisplaySeconds,
            _ => DefaultDisplaySeconds,
        };
    }
}
