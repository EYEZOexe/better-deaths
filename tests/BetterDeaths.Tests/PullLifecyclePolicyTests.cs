namespace BetterDeaths;

public sealed class PullLifecyclePolicyTests
{
    [Fact]
    public void DeathContainingPullArchivesForReview()
    {
        var action = PullLifecyclePolicy.GetArchiveAction(
            deathCount: 1,
            hasStartedPull: true,
            hasElapsedPull: true);

        Assert.Equal(PullArchiveAction.ArchiveForReview, action);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void StartedOrElapsedZeroDeathPullResetsInsteadOfArchiving(
        bool hasStartedPull,
        bool hasElapsedPull)
    {
        var action = PullLifecyclePolicy.GetArchiveAction(
            deathCount: 0,
            hasStartedPull,
            hasElapsedPull);

        Assert.Equal(PullArchiveAction.Reset, action);
    }

    [Fact]
    public void EmptyInactivePullHasNoArchiveWork()
    {
        var action = PullLifecyclePolicy.GetArchiveAction(
            deathCount: 0,
            hasStartedPull: false,
            hasElapsedPull: false);

        Assert.Equal(PullArchiveAction.None, action);
    }

    [Theory]
    [InlineData(false, 1, true)]
    [InlineData(false, 8, true)]
    [InlineData(false, 0, false)]
    [InlineData(true, 1, false)]
    [InlineData(true, 8, false)]
    public void SnapshotCaptureRemainsDeathGatedAndSingleShot(
        bool snapshotAlreadyCaptured,
        int deathCount,
        bool expected)
    {
        Assert.Equal(
            expected,
            PullLifecyclePolicy.ShouldCaptureSnapshot(snapshotAlreadyCaptured, deathCount));
    }
}
