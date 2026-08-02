namespace BetterDeaths;

public sealed class ThemeIdentityPolicyTests
{
    [Fact]
    public void StableKeySurvivesEnumReordering()
    {
        var key = ThemeIdentityPolicy.GetKey(LegacyTheme.Marble);

        Assert.NotEqual((int)LegacyTheme.Marble, (int)CurrentTheme.Marble);
        Assert.True(ThemeIdentityPolicy.TryResolve<CurrentTheme>(key, out var resolved));
        Assert.Equal(CurrentTheme.Marble, resolved);
    }

    [Theory]
    [InlineData("Marble")]
    [InlineData("marble")]
    [InlineData("MARBLE")]
    public void ThemeKeyResolutionIsCaseInsensitive(string key)
    {
        Assert.True(ThemeIdentityPolicy.TryResolve<CurrentTheme>(key, out var resolved));
        Assert.Equal(CurrentTheme.Marble, resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("Missing")]
    public void InvalidOrNumericKeysAreRejected(string key)
    {
        Assert.False(ThemeIdentityPolicy.TryResolve<CurrentTheme>(key, out _));
    }

    private enum LegacyTheme
    {
        Classic,
        Marble,
        PastelBlue,
    }

    private enum CurrentTheme
    {
        Classic,
        PastelBlue,
        Marble,
    }
}
