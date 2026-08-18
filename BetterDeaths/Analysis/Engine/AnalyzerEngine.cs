namespace BetterDeaths.Analysis.Engine;

using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed class AnalyzerEngine
{
    private readonly AnalyzerRegistry registry;

    public AnalyzerEngine(AnalyzerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        this.registry = registry;
    }

    public async ValueTask<AnalyzerRunResult> AnalyzeAsync(
        RecordedPull pull,
        AnalysisConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pull);
        cancellationToken.ThrowIfCancellationRequested();

        var orderedModules = DependencyResolver.Resolve(registry.Modules);
        var eventIndex = new EventIndex(pull.Events);
        var actorIndex = new ActorIndex(pull.Actors);
        var resolvedConfiguration = configuration ?? AnalysisConfiguration.Default;
        var allResults = new List<AnalysisResult>();
        var failures = new List<AnalyzerModuleFailure>();
        var skipped = new List<AnalyzerModuleSkip>();
        var successfulModules = new HashSet<string>(StringComparer.Ordinal);
        var unavailableModules = new HashSet<string>(StringComparer.Ordinal);
        var resultsByAnalyzer = new Dictionary<string, IReadOnlyList<AnalysisResult>>(StringComparer.Ordinal);
        var resultIds = new HashSet<AnalysisResultId>();

        foreach (var module in orderedModules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dependencies = DependencyResolver.NormalizeDependencies(module);
            var unavailableDependencies = dependencies
                .Where(dependency => unavailableModules.Contains(dependency))
                .ToArray();
            if (unavailableDependencies.Length > 0)
            {
                skipped.Add(new AnalyzerModuleSkip(
                    module.Id,
                    AnalyzerSkipReason.DependencyUnavailable,
                    unavailableDependencies));
                unavailableModules.Add(module.Id);
                resultsByAnalyzer[module.Id] = Array.Empty<AnalysisResult>();
                continue;
            }

            var dependencyResults = new AnalysisDependencyResults(dependencies, resultsByAnalyzer, successfulModules);
            var context = new AnalyzerContext
            {
                Pull = pull,
                Events = eventIndex,
                Actors = actorIndex,
                Configuration = resolvedConfiguration,
                DependencyResults = dependencyResults,
            };

            bool supported;
            try
            {
                supported = module.Supports(context);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(CreateFailure(module.Id, exception));
                unavailableModules.Add(module.Id);
                resultsByAnalyzer[module.Id] = Array.Empty<AnalysisResult>();
                continue;
            }

            if (!supported)
            {
                skipped.Add(new AnalyzerModuleSkip(
                    module.Id,
                    AnalyzerSkipReason.Unsupported,
                    Array.Empty<string>()));
                unavailableModules.Add(module.Id);
                resultsByAnalyzer[module.Id] = Array.Empty<AnalysisResult>();
                continue;
            }

            var moduleSink = new ModuleAnalysisResultSink(module.Id);
            try
            {
                await module.AnalyzeAsync(context, moduleSink, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                var committed = moduleSink.Results.ToArray();
                var duplicateResult = committed.FirstOrDefault(result => resultIds.Contains(result.Id));
                if (duplicateResult is not null)
                {
                    throw new InvalidOperationException(
                        $"Analysis result ID '{duplicateResult.Id.Value}' was emitted more than once in one analysis run.");
                }

                foreach (var result in committed)
                {
                    resultIds.Add(result.Id);
                }

                resultsByAnalyzer[module.Id] = committed;
                allResults.AddRange(committed);
                successfulModules.Add(module.Id);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(CreateFailure(module.Id, exception));
                unavailableModules.Add(module.Id);
                resultsByAnalyzer[module.Id] = Array.Empty<AnalysisResult>();
            }
        }

        return new AnalyzerRunResult
        {
            Results = allResults.ToArray(),
            Failures = failures.ToArray(),
            Skipped = skipped.ToArray(),
        };
    }

    private static AnalyzerModuleFailure CreateFailure(string analyzerId, Exception exception)
    {
        return new AnalyzerModuleFailure(
            analyzerId,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message);
    }

    private sealed class ModuleAnalysisResultSink : IAnalysisResultSink
    {
        private readonly string analyzerId;
        private readonly HashSet<AnalysisResultId> resultIds = [];
        private readonly List<AnalysisResult> results = [];

        public ModuleAnalysisResultSink(string analyzerId)
        {
            this.analyzerId = analyzerId;
        }

        public IReadOnlyList<AnalysisResult> Results => results;

        public void Add(AnalysisResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (!string.Equals(result.AnalyzerId, analyzerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Analyzer '{analyzerId}' attempted to emit a result owned by '{result.AnalyzerId}'.");
            }

            if (!resultIds.Add(result.Id))
            {
                throw new InvalidOperationException(
                    $"Analyzer '{analyzerId}' emitted duplicate result ID '{result.Id.Value}'.");
            }

            results.Add(result);
        }
    }

    private sealed class AnalysisDependencyResults : IAnalysisDependencyResults
    {
        private readonly HashSet<string> declaredDependencies;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<AnalysisResult>> resultsByAnalyzer;
        private readonly IReadOnlySet<string> successfulModules;

        public AnalysisDependencyResults(
            IReadOnlyList<string> declaredDependencies,
            IReadOnlyDictionary<string, IReadOnlyList<AnalysisResult>> resultsByAnalyzer,
            IReadOnlySet<string> successfulModules)
        {
            this.declaredDependencies = new HashSet<string>(declaredDependencies, StringComparer.Ordinal);
            this.resultsByAnalyzer = resultsByAnalyzer;
            this.successfulModules = successfulModules;
        }

        public IReadOnlyList<AnalysisResult> GetResults(string analyzerId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);
            if (!declaredDependencies.Contains(analyzerId))
            {
                throw new InvalidOperationException(
                    $"Analyzer dependency results for '{analyzerId}' were requested without declaring that dependency.");
            }

            if (!successfulModules.Contains(analyzerId) || !resultsByAnalyzer.TryGetValue(analyzerId, out var results))
            {
                throw new InvalidOperationException(
                    $"Declared analyzer dependency '{analyzerId}' is not available for this module execution.");
            }

            return results;
        }
    }
}
