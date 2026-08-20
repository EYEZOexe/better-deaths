namespace BetterDeaths;

using BetterDeaths.Sources.FFLogs;
using System.Runtime.CompilerServices;

public sealed class FFLogsCredentialInputTests
{
    [Fact]
    public void NormalizesAccidentalClipboardWhitespaceWithoutEchoingValues()
    {
        const string clientId = "TEST_CLIENT_ID";
        const string clientSecret = "NOT_A_REAL_SECRET";

        Assert.Equal(clientId, FFLogsCredentialInput.NormalizeClientId($"  {clientId}\r\n"));
        Assert.Equal(clientSecret, FFLogsCredentialInput.NormalizeClientSecret($"\t{clientSecret}  "));
    }

    [Fact]
    public void RejectsWhitespaceOnlyCredentialInputs()
    {
        Assert.Throws<ArgumentException>(() => FFLogsCredentialInput.NormalizeClientId("   \r\n"));
        Assert.Throws<ArgumentException>(() => FFLogsCredentialInput.NormalizeClientSecret("\t  "));
    }

    [Fact]
    public void PublicImportSessionNormalizesBothValuesBeforeConstructingCredentials()
    {
        var source = ReadRepositoryFile("BetterDeaths/Sources/FFLogs/FFLogsPublicImportSession.cs");

        Assert.Contains("FFLogsCredentialInput.NormalizeClientId(clientId)", source, StringComparison.Ordinal);
        Assert.Contains("FFLogsCredentialInput.NormalizeClientSecret(clientSecret)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new FFLogsClientCredentials(clientId, clientSecret)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticationFailureMessageIsActionableAndSecretSafe()
    {
        var error = FFLogsIntegrationErrors.AuthenticationFailed();

        Assert.Equal("fflogs.authentication_failed", error.Code);
        Assert.Contains("re-enter", error.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TEST_CLIENT_ID", error.SafeMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT_A_REAL_SECRET", error.SafeMessage, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(
        string relativePath,
        [CallerFilePath] string testSourcePath = "")
    {
        var testDirectory = Path.GetDirectoryName(testSourcePath)
            ?? throw new InvalidOperationException("Could not resolve test source directory.");
        var repositoryRoot = Path.GetFullPath(Path.Combine(testDirectory, "..", ".."));
        return File.ReadAllText(Path.Combine(
            repositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
