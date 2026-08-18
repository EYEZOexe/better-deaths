namespace BetterDeaths;

internal enum PullArchiveAction
{
    None,
    ArchiveForReview,
    Reset,
}

internal static class PullLifecyclePolicy
{
    public static PullArchiveAction GetArchiveAction(
        int deathCount,
        bool hasStartedPull,
        bool hasElapsedPull)
    {
        if (deathCount > 0)
        {
            return PullArchiveAction.ArchiveForReview;
        }

        return hasStartedPull || hasElapsedPull
            ? PullArchiveAction.Reset
            : PullArchiveAction.None;
    }

    public static bool ShouldCaptureSnapshot(bool snapshotAlreadyCaptured, int deathCount)
    {
        return !snapshotAlreadyCaptured && deathCount > 0;
    }
}
