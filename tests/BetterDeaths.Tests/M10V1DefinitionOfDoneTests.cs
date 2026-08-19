namespace BetterDeaths;

using BetterDeaths.Capture.FullPull;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using BetterDeaths.Windows.Analyzer;
using System.Reflection;

public sealed class M10V1DefinitionOfDoneTests
{
    private static readonly DateTimeOffset PullStart = new(2026, 8, 19, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MeaningfulZeroDeathLocalPullPersistsReloadsAndRunsDefaultAnalyzerEngine()
    {
        var recorder = new FullPullRecorder();
        var pullId = new PullId(Guid.Parse("de10de10-1111-2222-3333-444444444444"));
        recorder.Begin(new PullStartContext
        {
            PullId = pullId,
            Metadata = new PullMetadata
            {
                TerritoryId = 777,
                TerritoryName = "M10 v1 zero-death fixture",
                StartedAt = PullStart,
            },
            SchemaVersion = new PullSchemaVersion(CanonicalPullSerializer.CurrentPullSchemaVersion),
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = $"local:{pullId.Value:N}",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
            DutyActive = true,
        });
        recorder.MarkCombatObserved();

        var normalizer = new DalamudLiveEventNormalizer(
            recorder,
            PullStart,
            $"local:{pullId.Value:N}");
        var player = new LiveActorReference
        {
            StableKey = "party:m10-v1-player",
            Name = "M10 Player",
            Kind = ActorKind.Player,
            ClassJobId = 19,
        };
        var boss = new LiveActorReference
        {
            StableKey = "object:m10-v1-boss",
            Name = "M10 Boss",
            Kind = ActorKind.Enemy,
        };

        normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart.AddSeconds(1),
            Kind = LiveActionEffectKind.ActionUse,
            Source = player,
            Target = boss,
            ActionId = 100,
            Fidelity = CaptureFidelity.Exact,
        });
        normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart.AddSeconds(1),
            Kind = LiveActionEffectKind.Damage,
            Source = player,
            Target = boss,
            ActionId = 100,
            Amount = 25_000,
            Fidelity = CaptureFidelity.Exact,
        });

        Assert.True(recorder.TryFinalize(new PullEndContext(TimeSpan.FromSeconds(20)), out var finalized));
        Assert.NotNull(finalized);
        Assert.DoesNotContain(finalized.Events, evt => evt is DeathEvent);

        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        await store.SaveAsync(finalized);
        var loaded = await store.LoadAsync(pullId);

        Assert.NotNull(loaded);
        Assert.Equal(pullId, loaded.Id);
        Assert.Equal(finalized.Events.Select(evt => evt.Id), loaded.Events.Select(evt => evt.Id));
        Assert.Equal(finalized.Events.Select(evt => evt.Sequence), loaded.Events.Select(evt => evt.Sequence));
        Assert.DoesNotContain(loaded.Events, evt => evt is DeathEvent);

        var run = await AnalyzerWorkspaceEngineComposition.CreateDefault().AnalyzeAsync(loaded);

        Assert.Empty(run.Failures);
    }

    [Fact]
    public void DomainAndAnalysisImplementationTypesDoNotDependOnSourcePersistenceUiOrDalamudLayers()
    {
        var assembly = typeof(RecordedPull).Assembly;
        var protectedTypes = assembly.GetTypes()
            .Where(type => type.Namespace is not null &&
                (type.Namespace.StartsWith("BetterDeaths.Domain", StringComparison.Ordinal) ||
                 type.Namespace.StartsWith("BetterDeaths.Analysis", StringComparison.Ordinal)))
            .ToArray();
        var forbiddenTokens = new[]
        {
            "BetterDeaths.Sources",
            "BetterDeaths.Persistence",
            "BetterDeaths.Windows",
            "Dalamud",
            "ImGui",
        };

        Assert.NotEmpty(protectedTypes);
        foreach (var type in protectedTypes)
        {
            AssertContractTypeIsPure(type, forbiddenTokens);
        }
    }

    private static void AssertContractTypeIsPure(Type type, IReadOnlyList<string> forbiddenTokens)
    {
        Assert.DoesNotContain(forbiddenTokens, token => ContainsToken(type, token));

        if (type.BaseType is not null)
        {
            Assert.DoesNotContain(forbiddenTokens, token => ContainsToken(type.BaseType, token));
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            Assert.DoesNotContain(forbiddenTokens, token => ContainsToken(interfaceType, token));
        }

        foreach (var property in type.GetProperties(
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            Assert.DoesNotContain(forbiddenTokens, token => ContainsToken(property.PropertyType, token));
        }

        foreach (var field in type.GetFields(
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            Assert.DoesNotContain(forbiddenTokens, token => ContainsToken(field.FieldType, token));
        }

        foreach (var method in type.GetMethods(
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            Assert.DoesNotContain(forbiddenTokens, token => ContainsToken(method.ReturnType, token));
            foreach (var parameter in method.GetParameters())
            {
                Assert.DoesNotContain(forbiddenTokens, token => ContainsToken(parameter.ParameterType, token));
            }
        }
    }

    private static bool ContainsToken(Type type, string token)
    {
        if ((type.FullName ?? type.Name).Contains(token, StringComparison.Ordinal))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(argument => ContainsToken(argument, token));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "BetterDeaths.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
