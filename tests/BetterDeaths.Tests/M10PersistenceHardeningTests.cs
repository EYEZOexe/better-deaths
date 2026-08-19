namespace BetterDeaths;

using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using System.Text.Json.Nodes;

public sealed class M10PersistenceHardeningTests
{
    [Fact]
    public void SerializerRejectsUnsupportedPullSchemaDuringDeserialization()
    {
        var pull = CreatePull(
            "10101010-1010-1010-1010-101010101010",
            TimeSpan.FromSeconds(12));
        var root = JsonNode.Parse(CanonicalPullSerializer.Serialize(pull))!.AsObject();
        var pullNode = root["Pull"]!.AsObject();
        var schemaNode = pullNode["SchemaVersion"]!.AsObject();
        schemaNode["Value"] = 999;

        var error = Assert.Throws<CanonicalPullCompatibilityException>(
            () => CanonicalPullSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("Unsupported canonical pull schema", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryFallsBackToValidIndexBackupWhenPrimaryIndexIsCorrupt()
    {
        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        var first = CreatePull(
            "20202020-2020-2020-2020-202020202020",
            TimeSpan.FromSeconds(10));
        var second = CreatePull(
            "30303030-3030-3030-3030-303030303030",
            TimeSpan.FromSeconds(20));

        await store.SaveAsync(first);
        await store.SaveAsync(second);

        var indexPath = GetIndexPath(directory.Path);
        Assert.True(File.Exists(indexPath + ".bak"));
        await File.WriteAllTextAsync(indexPath, "{ corrupt primary index");

        var summaries = await store.QueryAsync(new PullQuery { Limit = 10 });

        var summary = Assert.Single(summaries);
        Assert.Equal(first.Id, summary.Id);
        Assert.DoesNotContain(summaries, candidate => candidate.Id == second.Id);
    }

    [Fact]
    public async Task IndexRebuildIgnoresStaleTemporaryDetailAndIndexFiles()
    {
        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        var persisted = CreatePull(
            "40404040-4040-4040-4040-404040404040",
            TimeSpan.FromSeconds(10));
        var stale = CreatePull(
            "50505050-5050-5050-5050-505050505050",
            TimeSpan.FromSeconds(99));

        await store.SaveAsync(persisted);

        var staleDetailPath = GetDetailPath(directory.Path, stale.Id) + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(staleDetailPath)!);
        await File.WriteAllTextAsync(staleDetailPath, CanonicalPullSerializer.Serialize(stale));
        await File.WriteAllTextAsync(GetIndexPath(directory.Path) + ".tmp", "{ stale temporary index");

        DeleteIfExists(GetIndexPath(directory.Path));
        DeleteIfExists(GetIndexPath(directory.Path) + ".bak");

        var summaries = await store.QueryAsync(new PullQuery { Limit = 10 });

        var summary = Assert.Single(summaries);
        Assert.Equal(persisted.Id, summary.Id);
        Assert.DoesNotContain(summaries, candidate => candidate.Id == stale.Id);
    }

    [Fact]
    public async Task StaleTemporaryDetailDoesNotReplaceLastKnownGoodPrimary()
    {
        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        var id = "41414141-4141-4141-4141-414141414141";
        var persisted = CreatePull(id, TimeSpan.FromSeconds(10));
        var interruptedReplacement = CreatePull(id, TimeSpan.FromSeconds(99));

        await store.SaveAsync(persisted);

        var tempPath = GetDetailPath(directory.Path, persisted.Id) + ".tmp";
        await File.WriteAllTextAsync(tempPath, CanonicalPullSerializer.Serialize(interruptedReplacement));

        var loaded = await store.LoadAsync(persisted.Id);

        Assert.NotNull(loaded);
        Assert.Equal(TimeSpan.FromSeconds(10), loaded.Metadata.Duration);
        Assert.True(File.Exists(tempPath));
    }

    [Fact]
    public async Task IndexRebuildIgnoresDetailWhosePayloadIdentityDoesNotMatchItsFileName()
    {
        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        var persisted = CreatePull(
            "51515151-5151-5151-5151-515151515151",
            TimeSpan.FromSeconds(10));
        var mismatched = CreatePull(
            "52525252-5252-5252-5252-525252525252",
            TimeSpan.FromSeconds(20));

        await store.SaveAsync(persisted);
        await File.WriteAllTextAsync(
            GetDetailPath(directory.Path, new PullId(Guid.Parse("53535353-5353-5353-5353-535353535353"))),
            CanonicalPullSerializer.Serialize(mismatched));

        DeleteIfExists(GetIndexPath(directory.Path));
        DeleteIfExists(GetIndexPath(directory.Path) + ".bak");

        var summaries = await store.QueryAsync(new PullQuery { Limit = 10 });

        var summary = Assert.Single(summaries);
        Assert.Equal(persisted.Id, summary.Id);
        Assert.DoesNotContain(summaries, candidate => candidate.Id == mismatched.Id);
    }

    [Fact]
    public async Task LoadRejectsMismatchedPrimaryDetailIdentityAndUsesMatchingBackup()
    {
        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        var id = "60606060-6060-6060-6060-606060606060";
        var first = CreatePull(id, TimeSpan.FromSeconds(10));
        var second = CreatePull(id, TimeSpan.FromSeconds(20));
        var mismatched = CreatePull(
            "70707070-7070-7070-7070-707070707070",
            TimeSpan.FromSeconds(30));

        await store.SaveAsync(first);
        await store.SaveAsync(second);

        var detailPath = GetDetailPath(directory.Path, first.Id);
        Assert.True(File.Exists(detailPath + ".bak"));
        await File.WriteAllTextAsync(detailPath, CanonicalPullSerializer.Serialize(mismatched));

        var recovered = await store.LoadAsync(first.Id);

        Assert.NotNull(recovered);
        Assert.Equal(first.Id, recovered.Id);
        Assert.Equal(TimeSpan.FromSeconds(10), recovered.Metadata.Duration);
        Assert.NotEqual(mismatched.Id, recovered.Id);
    }

    private static RecordedPull CreatePull(string id, TimeSpan duration)
    {
        var player = new ActorRecord
        {
            Id = new ActorId(1),
            Name = "Player",
            Kind = ActorKind.Player,
        };
        var enemy = new ActorRecord
        {
            Id = new ActorId(2),
            Name = "Boss",
            Kind = ActorKind.Enemy,
        };

        return new RecordedPull
        {
            Id = new PullId(Guid.Parse(id)),
            Metadata = new PullMetadata
            {
                TerritoryId = 777,
                TerritoryName = "M10 Persistence Fixture",
                Duration = duration,
                StartedAt = new DateTimeOffset(2026, 8, 19, 18, 0, 0, TimeSpan.Zero),
            },
            SchemaVersion = new PullSchemaVersion(CanonicalPullSerializer.CurrentPullSchemaVersion),
            Actors = [player, enemy],
            Events =
            [
                new DamageEvent
                {
                    Id = new EventId(1),
                    Sequence = 1,
                    PullTime = TimeSpan.FromSeconds(1),
                    SourceActorId = enemy.Id,
                    TargetActorId = player.Id,
                    Provenance = new EventProvenance
                    {
                        SourceKind = PullDataSourceKind.DalamudLive,
                        SourceReference = "m10:persistence",
                        Fidelity = CaptureFidelity.Exact,
                    },
                    Amount = 1000,
                    ActionId = 100,
                },
            ],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "m10:persistence",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static string GetIndexPath(string root) =>
        Path.Combine(root, "canonical-pulls.index.json");

    private static string GetDetailPath(string root, PullId id) =>
        Path.Combine(root, "canonical-pull-details", $"{id.Value:N}.json");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
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
