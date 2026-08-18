namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Domain;

public sealed class AnalyzerEngineTests
{
    [Fact]
    public async Task RegistryCompositionExecutesModulesWithoutEngineDispatchEdits()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(Module("generic.beta", results => results.Add(Result("generic.beta", 2))));
        registry.Register(Module("generic.alpha", results => results.Add(Result("generic.alpha", 1))));
        var engine = new AnalyzerEngine(registry);

        var run = await engine.AnalyzeAsync(CreatePull());

        Assert.Equal(new[] { "generic.alpha", "generic.beta" }, run.Results.Select(result => result.AnalyzerId));
        Assert.Empty(run.Failures);
        Assert.Empty(run.Skipped);
    }

    [Fact]
    public void RegistryRejectsDuplicateAnalyzerIds()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(Module("generic.same"));

        var error = Assert.Throws<InvalidOperationException>(() => registry.Register(Module("generic.same")));

        Assert.Contains("already registered", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DependenciesExecuteFirstAndExposeOnlyDeclaredDependencyResults()
    {
        var execution = new List<string>();
        var registry = new AnalyzerRegistry();
        registry.Register(Module(
            "generic.consumer",
            analyze: (context, results, _) =>
            {
                execution.Add("consumer");
                var dependency = Assert.Single(context.DependencyResults.GetResults("generic.base"));
                results.Add(Result("generic.consumer", 2, summary: $"saw:{dependency.Summary}"));
                return ValueTask.CompletedTask;
            },
            dependencies: ["generic.base"]));
        registry.Register(Module(
            "generic.base",
            analyze: (_, results, _) =>
            {
                execution.Add("base");
                results.Add(Result("generic.base", 1, summary: "base-result"));
                return ValueTask.CompletedTask;
            }));
        var engine = new AnalyzerEngine(registry);

        var run = await engine.AnalyzeAsync(CreatePull());

        Assert.Equal(new[] { "base", "consumer" }, execution);
        Assert.Equal("saw:base-result", Assert.Single(run.Results.Where(result => result.AnalyzerId == "generic.consumer")).Summary);
        Assert.Empty(run.Failures);
        Assert.Empty(run.Skipped);
    }

    [Fact]
    public async Task UndeclaredDependencyAccessFailsOnlyThatModule()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(Module("generic.base", results => results.Add(Result("generic.base", 1))));
        registry.Register(Module(
            "generic.bad-reader",
            analyze: (context, _, _) =>
            {
                _ = context.DependencyResults.GetResults("generic.base");
                return ValueTask.CompletedTask;
            }));
        registry.Register(Module("generic.independent", results => results.Add(Result("generic.independent", 3))));
        var engine = new AnalyzerEngine(registry);

        var run = await engine.AnalyzeAsync(CreatePull());

        Assert.Contains(run.Results, result => result.AnalyzerId == "generic.base");
        Assert.Contains(run.Results, result => result.AnalyzerId == "generic.independent");
        Assert.Contains(run.Failures, failure => failure.AnalyzerId == "generic.bad-reader");
    }

    [Fact]
    public void MissingDependencyIsExplicitBeforeExecution()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(Module("generic.consumer", dependencies: ["generic.missing"]));
        var engine = new AnalyzerEngine(registry);

        var error = Assert.Throws<AnalyzerDependencyException>(
            () => engine.AnalyzeAsync(CreatePull()).AsTask().GetAwaiter().GetResult());

        Assert.Contains("missing dependency 'generic.missing'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DependencyCycleIsExplicitBeforeExecution()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(Module("generic.a", dependencies: ["generic.b"]));
        registry.Register(Module("generic.b", dependencies: ["generic.a"]));
        var engine = new AnalyzerEngine(registry);

        var error = Assert.Throws<AnalyzerDependencyException>(
            () => engine.AnalyzeAsync(CreatePull()).AsTask().GetAwaiter().GetResult());

        Assert.Contains("cycle", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("generic.a", error.Message, StringComparison.Ordinal);
        Assert.Contains("generic.b", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModuleFailureDoesNotDestroyIndependentResultsAndDependentModuleIsSkipped()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(Module(
            "generic.fail",
            analyze: (_, results, _) =>
            {
                results.Add(Result("generic.fail", 1));
                throw new InvalidOperationException("intentional failure");
            }));
        registry.Register(Module("generic.independent", results => results.Add(Result("generic.independent", 2))));
        registry.Register(Module(
            "generic.dependent",
            results => results.Add(Result("generic.dependent", 3)),
            dependencies: ["generic.fail"]));
        var engine = new AnalyzerEngine(registry);

        var run = await engine.AnalyzeAsync(CreatePull());

        Assert.DoesNotContain(run.Results, result => result.AnalyzerId == "generic.fail");
        Assert.Contains(run.Results, result => result.AnalyzerId == "generic.independent");
        var failure = Assert.Single(run.Failures);
        Assert.Equal("generic.fail", failure.AnalyzerId);
        Assert.Contains("intentional failure", failure.Message, StringComparison.Ordinal);
        var skip = Assert.Single(run.Skipped);
        Assert.Equal("generic.dependent", skip.AnalyzerId);
        Assert.Equal(AnalyzerSkipReason.DependencyUnavailable, skip.Reason);
        Assert.Equal(new[] { "generic.fail" }, skip.UnavailableDependencies);
    }

    [Fact]
    public async Task UnsupportedModuleMakesDeclaredDependentsUnavailableWithoutBeingFailure()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(Module("generic.unsupported", supports: _ => false));
        registry.Register(Module("generic.dependent", dependencies: ["generic.unsupported"]));
        var engine = new AnalyzerEngine(registry);

        var run = await engine.AnalyzeAsync(CreatePull());

        Assert.Empty(run.Results);
        Assert.Empty(run.Failures);
        Assert.Equal(2, run.Skipped.Count);
        Assert.Contains(run.Skipped, skip => skip.AnalyzerId == "generic.unsupported" && skip.Reason == AnalyzerSkipReason.Unsupported);
        Assert.Contains(run.Skipped, skip => skip.AnalyzerId == "generic.dependent" && skip.Reason == AnalyzerSkipReason.DependencyUnavailable);
    }

    [Fact]
    public async Task RequestedCancellationPropagatesInsteadOfBecomingAnalyzerFailure()
    {
        using var cts = new CancellationTokenSource();
        var registry = new AnalyzerRegistry();
        registry.Register(Module(
            "generic.cancel",
            analyze: (_, _, cancellationToken) =>
            {
                cts.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            }));
        var engine = new AnalyzerEngine(registry);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.AnalyzeAsync(CreatePull(), cancellationToken: cts.Token).AsTask());
    }

    [Fact]
    public async Task ResultOwnershipMismatchFailsOnlyEmittingModule()
    {
        var registry = new AnalyzerRegistry();
        registry.Register(Module("generic.bad", results => results.Add(Result("generic.someone-else", 1))));
        registry.Register(Module("generic.good", results => results.Add(Result("generic.good", 2))));
        var engine = new AnalyzerEngine(registry);

        var run = await engine.AnalyzeAsync(CreatePull());

        Assert.Single(run.Results);
        Assert.Equal("generic.good", run.Results[0].AnalyzerId);
        Assert.Equal("generic.bad", Assert.Single(run.Failures).AnalyzerId);
    }

    [Fact]
    public async Task FailedModuleDoesNotReserveOtherwiseValidResultIds()
    {
        var firstId = ResultId(1);
        var transientId = ResultId(2);
        var registry = new AnalyzerRegistry();
        registry.Register(Module("generic.a", results => results.Add(Result("generic.a", firstId))));
        registry.Register(Module(
            "generic.b",
            analyze: (_, results, _) =>
            {
                results.Add(Result("generic.b", transientId));
                results.Add(Result("generic.b", firstId));
                return ValueTask.CompletedTask;
            }));
        registry.Register(Module("generic.c", results => results.Add(Result("generic.c", transientId))));
        var engine = new AnalyzerEngine(registry);

        var run = await engine.AnalyzeAsync(CreatePull());

        Assert.Contains(run.Results, result => result.AnalyzerId == "generic.a");
        Assert.DoesNotContain(run.Results, result => result.AnalyzerId == "generic.b");
        Assert.Contains(run.Results, result => result.AnalyzerId == "generic.c");
        Assert.Contains(run.Failures, failure => failure.AnalyzerId == "generic.b");
        Assert.DoesNotContain(run.Failures, failure => failure.AnalyzerId == "generic.c");
    }

    private static TestAnalyzerModule Module(
        string id,
        Action<IAnalysisResultSink>? simpleAnalyze = null,
        IReadOnlyCollection<string>? dependencies = null,
        Func<AnalyzerContext, bool>? supports = null,
        Func<AnalyzerContext, IAnalysisResultSink, CancellationToken, ValueTask>? analyze = null)
    {
        return new TestAnalyzerModule(
            id,
            dependencies ?? Array.Empty<string>(),
            supports ?? (_ => true),
            analyze ?? ((_, results, _) =>
            {
                simpleAnalyze?.Invoke(results);
                return ValueTask.CompletedTask;
            }));
    }

    private static AnalysisResult Result(string analyzerId, int id, string? summary = null)
    {
        return Result(analyzerId, ResultId(id), summary);
    }

    private static AnalysisResult Result(string analyzerId, AnalysisResultId id, string? summary = null)
    {
        return new AnalysisResult
        {
            Id = id,
            AnalyzerId = analyzerId,
            Severity = AnalysisSeverity.Info,
            Category = AnalysisCategory.DataQuality,
            Title = analyzerId,
            Summary = summary ?? analyzerId,
            Evidence = Array.Empty<AnalysisEvidence>(),
        };
    }

    private static AnalysisResultId ResultId(int id)
    {
        return new AnalysisResultId(Guid.Parse($"00000000-0000-0000-0000-{id:D12}"));
    }

    private static RecordedPull CreatePull()
    {
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1,
                TerritoryName = "Test Duty",
                Duration = TimeSpan.FromSeconds(10),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors = Array.Empty<ActorRecord>(),
            Events = Array.Empty<NormalizedEvent>(),
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                Fidelity = CaptureFidelity.Exact,
            },
        };
    }

    private sealed class TestAnalyzerModule : IAnalyzerModule
    {
        private readonly Func<AnalyzerContext, bool> supports;
        private readonly Func<AnalyzerContext, IAnalysisResultSink, CancellationToken, ValueTask> analyze;

        public TestAnalyzerModule(
            string id,
            IReadOnlyCollection<string> dependencies,
            Func<AnalyzerContext, bool> supports,
            Func<AnalyzerContext, IAnalysisResultSink, CancellationToken, ValueTask> analyze)
        {
            Id = id;
            Dependencies = dependencies;
            this.supports = supports;
            this.analyze = analyze;
        }

        public string Id { get; }

        public AnalyzerScope Scope => AnalyzerScope.Generic;

        public IReadOnlyCollection<string> Dependencies { get; }

        public bool Supports(AnalyzerContext context) => supports(context);

        public ValueTask AnalyzeAsync(
            AnalyzerContext context,
            IAnalysisResultSink results,
            CancellationToken cancellationToken) => analyze(context, results, cancellationToken);
    }
}
