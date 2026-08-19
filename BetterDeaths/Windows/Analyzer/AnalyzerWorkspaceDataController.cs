namespace BetterDeaths.Windows.Analyzer;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Generic;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed record AnalyzerWorkspaceLoadedPull
{
    public required RecordedPull Pull { get; init; }

    public required IReadOnlyList<AnalysisResult> Results { get; init; }

    public required IReadOnlyList<DeathEvent> DeathEvents { get; init; }

    public required IReadOnlyList<AnalyzerModuleFailure> Failures { get; init; }

    public required IReadOnlyList<AnalyzerModuleSkip> Skipped { get; init; }
}

internal sealed class AnalyzerWorkspaceDataController
{
    private readonly IPullStore pullStore;
    private readonly AnalyzerEngine analyzerEngine;

    public AnalyzerWorkspaceDataController(IPullStore pullStore, AnalyzerEngine analyzerEngine)
    {
        ArgumentNullException.ThrowIfNull(pullStore);
        ArgumentNullException.ThrowIfNull(analyzerEngine);
        this.pullStore = pullStore;
        this.analyzerEngine = analyzerEngine;
    }

    public static AnalyzerWorkspaceDataController CreateDefault(IPullStore pullStore)
    {
        ArgumentNullException.ThrowIfNull(pullStore);
        var registry = new AnalyzerRegistry();

        // M3's DeathEventAnalyzer was deliberately a minimal vertical slice. M5 replaces it in
        // the default workspace composition with the richer evidence-first death/raise context
        // analyzer and adds only analyzers that need no source/job/encounter-specific semantic
        // catalog. Configured mitigation and buff/cooldown definitions remain explicit inputs.
        registry.Register(new DeathRaiseContextAnalyzer());
        registry.Register(new HealingActivityAnalyzer());
        registry.Register(new TargetabilityAwareUptimeAnalyzer());

        return new AnalyzerWorkspaceDataController(pullStore, new AnalyzerEngine(registry));
    }

    public Task<IReadOnlyList<PullSummary>> QueryPullsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        return pullStore.QueryAsync(new PullQuery { Limit = limit }, cancellationToken);
    }

    public async Task<AnalyzerWorkspaceLoadedPull?> LoadPullAsync(
        PullId pullId,
        CancellationToken cancellationToken = default)
    {
        var pull = await pullStore.LoadAsync(pullId, cancellationToken).ConfigureAwait(false);
        if (pull is null)
        {
            return null;
        }

        var run = await analyzerEngine.AnalyzeAsync(pull, cancellationToken: cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return new AnalyzerWorkspaceLoadedPull
        {
            Pull = pull,
            Results = run.Results,
            DeathEvents = pull.Events.OfType<DeathEvent>().ToArray(),
            Failures = run.Failures,
            Skipped = run.Skipped,
        };
    }
}
