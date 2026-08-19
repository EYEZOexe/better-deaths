namespace BetterDeaths;

using System.Runtime.CompilerServices;

public sealed class AnalyzerExportPanelContractTests
{
    [Fact]
    public void ExportActionsRequireSelectedPullAndDisableWithoutOne()
    {
        var source = ReadRepositoryFile("BetterDeaths/Windows/Analyzer/Panels/AnalyzerExportPanel.cs");

        Assert.Contains("var pull = context.Pull;", source, StringComparison.Ordinal);
        Assert.Contains("var buttonsDisabled = isExporting || pull is null;", source, StringComparison.Ordinal);
        Assert.Contains(
            "if (ImGui.Button(\"Export canonical\") && pull is not null)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (ImGui.Button(\"Export anonymized\") && pull is not null)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("ImGui.TextDisabled(\"Select a pull to export.\");", source, StringComparison.Ordinal);
        Assert.DoesNotContain("QueueExport(context.Pull", source, StringComparison.Ordinal);
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
