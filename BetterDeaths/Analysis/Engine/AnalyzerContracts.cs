namespace BetterDeaths.Analysis.Engine;

using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

internal enum AnalyzerScope
{
    Generic,
    Job,
    Encounter,
    Session,
}

internal sealed record AnalysisConfiguration
{
    public static AnalysisConfiguration Default { get; } = new();
}

internal interface IAnalysisResultSink
{
    void Add(AnalysisResult result);
}

internal interface IAnalysisDependencyResults
{
    IReadOnlyList<AnalysisResult> GetResults(string analyzerId);
}

internal sealed record AnalyzerContext
{
    public required RecordedPull Pull { get; init; }

    public required EventIndex Events { get; init; }

    public required ActorIndex Actors { get; init; }

    public required TargetabilityIndex Targetability { get; init; }

    public required StatusIntervalIndex Statuses { get; init; }

    public required AnalysisConfiguration Configuration { get; init; }

    public required IAnalysisDependencyResults DependencyResults { get; init; }
}

internal interface IAnalyzerModule
{
    string Id { get; }

    AnalyzerScope Scope { get; }

    IReadOnlyCollection<string> Dependencies { get; }

    bool Supports(AnalyzerContext context);

    ValueTask AnalyzeAsync(
        AnalyzerContext context,
        IAnalysisResultSink results,
        CancellationToken cancellationToken);
}
