namespace BetterDeaths.Analysis.Engine;

using BetterDeaths.Domain;
using System.Collections.Generic;

internal enum AnalyzerSkipReason
{
    Unsupported,
    DependencyUnavailable,
}

internal sealed record AnalyzerModuleFailure(
    string AnalyzerId,
    string ExceptionType,
    string Message);

internal sealed record AnalyzerModuleSkip(
    string AnalyzerId,
    AnalyzerSkipReason Reason,
    IReadOnlyList<string> UnavailableDependencies);

internal sealed record AnalyzerRunResult
{
    public required IReadOnlyList<AnalysisResult> Results { get; init; }

    public required IReadOnlyList<AnalyzerModuleFailure> Failures { get; init; }

    public required IReadOnlyList<AnalyzerModuleSkip> Skipped { get; init; }
}
