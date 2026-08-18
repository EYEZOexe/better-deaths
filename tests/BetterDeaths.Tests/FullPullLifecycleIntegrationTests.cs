namespace BetterDeaths;

using BetterDeaths.Capture.FullPull;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using System.Runtime.CompilerServices;

public sealed class FullPullLifecycleIntegrationTests
{
    private static readonly DateTimeOffset PullStart = new(2026, 8, 18, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MeaningfulZeroDeathLivePipelineFinalizesPersistsAndReloads()
    {
        var recorder = new FullPullRecorder();
        var pullId = new PullId(Guid.Parse("11111111-aaaa-bbbb-cccc-222222222222"));
        recorder.Begin(new PullStartContext
        {
            PullId = pullId,
            Metadata = new PullMetadata
            {
                TerritoryId = 1234,
                TerritoryName = "Test Ultimate",
                StartedAt = PullStart,
            },
            SchemaVersion = new PullSchemaVersion(CanonicalPullSerializer.CurrentPullSchemaVersion),
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = $"local:{pullId.Value:N}",
                Fidelity = CaptureFidelity.Exact,
            },
            DutyActive = true,
        });
        recorder.MarkCombatObserved();
        var normalizer = new DalamudLiveEventNormalizer(recorder, PullStart, $"local:{pullId.Value:N}");
        var player = Actor("party:1", "Player", ActorKind.Player);
        var boss = Actor("object:boss:1", "Boss", ActorKind.Enemy);

        normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart.AddSeconds(2),
            Kind = LiveActionEffectKind.ActionUse,
            Source = player,
            Target = boss,
            ActionId = 100,
        });
        normalizer.Append(new LiveActionEffectFact
        {
            ObservedAt = PullStart.AddSeconds(2),
            Kind = LiveActionEffectKind.Damage,
            Source = player,
            Target = boss,
            ActionId = 100,
            Amount = 25000,
        });

        Assert.True(recorder.TryFinalize(new PullEndContext(TimeSpan.FromSeconds(15)), out var finalized));
        Assert.NotNull(finalized);
        Assert.DoesNotContain(finalized.Events, evt => evt is DeathEvent);

        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        await store.SaveAsync(finalized);
        var loaded = await store.LoadAsync(pullId);

        Assert.NotNull(loaded);
        Assert.Equal(pullId, loaded.Id);
        Assert.Equal(TimeSpan.FromSeconds(15), loaded.Metadata.Duration);
        Assert.Collection(
            loaded.Events,
            evt => Assert.IsType<ActionUseEvent>(evt),
            evt => Assert.IsType<DamageEvent>(evt));
        Assert.DoesNotContain(loaded.Events, evt => evt is DeathEvent);
        Assert.Equal(new long[] { 1, 2 }, loaded.Events.Select(evt => evt.Sequence));
    }

    [Fact]
    public void PluginArchiveFinalizesCanonicalPullBeforeLegacyDeathGatedDecision()
    {
        var source = ReadRepositoryFile("BetterDeaths/Plugin.PullLifecycle.cs");
        var canonicalIndex = source.IndexOf("FinalizeCurrentFullPull(reason);", StringComparison.Ordinal);
        var legacyDecisionIndex = source.IndexOf("PullLifecyclePolicy.GetArchiveAction", StringComparison.Ordinal);

        Assert.True(canonicalIndex >= 0, "Canonical pull finalization call was not found in archive flow.");
        Assert.True(legacyDecisionIndex > canonicalIndex, "Legacy archive/reset decision must happen after canonical finalization.");
        Assert.Contains("CaptureCurrentPullSnapshot(reason);", source);
        Assert.Contains("ResetCurrentPull(suppressResetStateDeaths);", source);
    }

    [Fact]
    public void ExistingLegacySnapshotRemainsDeathGated()
    {
        Assert.False(PullLifecyclePolicy.ShouldCaptureSnapshot(snapshotAlreadyCaptured: false, deathCount: 0));
        Assert.True(PullLifecyclePolicy.ShouldCaptureSnapshot(snapshotAlreadyCaptured: false, deathCount: 1));
    }

    [Fact]
    public void RawQueueFeedsCanonicalCaptureWithoutReplacingExistingReplayOrDeathResolution()
    {
        var source = ReadRepositoryFile("BetterDeaths/Plugin.RawEvents.cs");
        var canonicalIndex = source.IndexOf("CaptureFullPullActionEffects(packet);", StringComparison.Ordinal);
        var replayIndex = source.IndexOf("AddActionEffectReplayPoseSamples(packet);", StringComparison.Ordinal);
        var legacyIndex = source.IndexOf("ResolveRawActionEffectPacket(packet);", StringComparison.Ordinal);

        Assert.True(canonicalIndex >= 0);
        Assert.True(replayIndex > canonicalIndex);
        Assert.True(legacyIndex > replayIndex);
    }

    [Fact]
    public void LegacyLeadUpRetentionRemainsBoundedAfterFullPullIntegration()
    {
        Assert.Equal(70, LeadUpTimingPolicy.CaptureSeconds);
        Assert.Equal(75, LeadUpTimingPolicy.LiveRetentionSeconds);
    }

    private static LiveActorReference Actor(string key, string name, ActorKind kind)
    {
        return new LiveActorReference
        {
            StableKey = key,
            Name = name,
            Kind = kind,
        };
    }

    private static string ReadRepositoryFile(
        string relativePath,
        [CallerFilePath] string testSourcePath = "")
    {
        var testDirectory = Path.GetDirectoryName(testSourcePath)
            ?? throw new InvalidOperationException("Could not resolve test source directory.");
        var repositoryRoot = Path.GetFullPath(Path.Combine(testDirectory, "..", ".."));
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return File.ReadAllText(Path.Combine(repositoryRoot, normalizedRelativePath));
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
