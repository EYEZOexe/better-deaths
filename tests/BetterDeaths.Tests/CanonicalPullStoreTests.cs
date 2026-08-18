namespace BetterDeaths;

using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using System.IO;
using System.Text.Json;

public sealed class CanonicalPullStoreTests
{
    [Fact]
    public void SerializerRoundTripPreservesZeroDeathCanonicalPull()
    {
        var pull = CreatePull(
            "11111111-1111-1111-1111-111111111111",
            territoryId: 100,
            startedAt: new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero));

        var json = CanonicalPullSerializer.Serialize(pull);
        var loaded = CanonicalPullSerializer.Deserialize(json);

        Assert.Equal(pull.Id, loaded.Id);
        Assert.Equal(new PullSchemaVersion(1), loaded.SchemaVersion);
        Assert.Single(loaded.Events);
        Assert.DoesNotContain(loaded.Events, evt => evt is DeathEvent);
        Assert.IsType<DamageEvent>(loaded.Events[0]);
    }

    [Fact]
    public void SerializerRejectsUnknownFileSchemaInsteadOfReinterpretingIt()
    {
        var pull = CreatePull(
            "22222222-2222-2222-2222-222222222222",
            territoryId: 100,
            startedAt: null);
        var json = CanonicalPullSerializer.Serialize(pull);
        var incompatible = json.Replace(
            $"\"FileSchemaVersion\":{CanonicalPullSerializer.CurrentFileSchemaVersion}",
            "\"FileSchemaVersion\":999",
            StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() => CanonicalPullSerializer.Deserialize(incompatible));

        Assert.Contains("Unsupported canonical pull file schema", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializerRejectsUnknownCanonicalPullSchema()
    {
        var pull = CreatePull(
            "33333333-3333-3333-3333-333333333333",
            territoryId: 100,
            startedAt: null) with
        {
            SchemaVersion = new PullSchemaVersion(999),
        };

        var error = Assert.Throws<InvalidDataException>(() => CanonicalPullSerializer.Serialize(pull));

        Assert.Contains("Unsupported canonical pull schema", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoreSavesLoadsQueriesAndDeletesZeroDeathPull()
    {
        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        var pull = CreatePull(
            "44444444-4444-4444-4444-444444444444",
            territoryId: 777,
            startedAt: new DateTimeOffset(2026, 8, 18, 18, 10, 0, TimeSpan.Zero));

        await store.SaveAsync(pull);

        var loaded = await store.LoadAsync(pull.Id);
        Assert.NotNull(loaded);
        Assert.Equal(pull.Id, loaded.Id);
        Assert.Single(loaded.Events);
        Assert.DoesNotContain(loaded.Events, evt => evt is DeathEvent);

        var summaries = await store.QueryAsync(new PullQuery { TerritoryId = 777, Limit = 10 });
        var summary = Assert.Single(summaries);
        Assert.Equal(pull.Id, summary.Id);
        Assert.Equal(1, summary.EventCount);
        Assert.Equal(2, summary.ActorCount);
        Assert.Equal(PullDataSourceKind.DalamudLive, summary.SourceKind);

        await store.DeleteAsync(pull.Id);

        Assert.Null(await store.LoadAsync(pull.Id));
        Assert.Empty(await store.QueryAsync(new PullQuery { Limit = 10 }));
    }

    [Fact]
    public async Task LoadFallsBackToPreviousDetailBackupWhenPrimaryIsCorrupt()
    {
        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        var id = "55555555-5555-5555-5555-555555555555";
        var first = CreatePull(id, 100, null) with
        {
            Metadata = CreateMetadata(100, TimeSpan.FromSeconds(10), null),
        };
        var second = first with
        {
            Metadata = CreateMetadata(100, TimeSpan.FromSeconds(20), null),
        };

        await store.SaveAsync(first);
        await store.SaveAsync(second);

        var detailPath = GetDetailPath(directory.Path, first.Id);
        Assert.True(File.Exists(detailPath + ".bak"));
        await File.WriteAllTextAsync(detailPath, "{ definitely not valid json");

        var recovered = await store.LoadAsync(first.Id);

        Assert.NotNull(recovered);
        Assert.Equal(TimeSpan.FromSeconds(10), recovered.Metadata.Duration);
    }

    [Fact]
    public async Task QueryRebuildsIndexFromDetailsWhenPrimaryAndBackupAreCorrupt()
    {
        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        var first = CreatePull(
            "66666666-6666-6666-6666-666666666666",
            100,
            new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero));
        var second = CreatePull(
            "77777777-7777-7777-7777-777777777777",
            200,
            new DateTimeOffset(2026, 8, 18, 18, 5, 0, TimeSpan.Zero));

        await store.SaveAsync(first);
        await store.SaveAsync(second);

        var indexPath = System.IO.Path.Combine(directory.Path, "canonical-pulls.index.json");
        Assert.True(File.Exists(indexPath));
        Assert.True(File.Exists(indexPath + ".bak"));
        await File.WriteAllTextAsync(indexPath, "corrupt primary");
        await File.WriteAllTextAsync(indexPath + ".bak", "corrupt backup");

        var summaries = await store.QueryAsync(new PullQuery { Limit = 10 });

        Assert.Equal(2, summaries.Count);
        Assert.Contains(summaries, summary => summary.Id == first.Id);
        Assert.Contains(summaries, summary => summary.Id == second.Id);
    }

    [Fact]
    public async Task QueryFiltersTerritoryOrdersNewestFirstAndHonorsLimit()
    {
        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        var older = CreatePull(
            "88888888-8888-8888-8888-888888888888",
            321,
            new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero));
        var newer = CreatePull(
            "99999999-9999-9999-9999-999999999999",
            321,
            new DateTimeOffset(2026, 8, 18, 18, 30, 0, TimeSpan.Zero));
        var other = CreatePull(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            654,
            new DateTimeOffset(2026, 8, 18, 19, 0, 0, TimeSpan.Zero));

        await store.SaveAsync(older);
        await store.SaveAsync(newer);
        await store.SaveAsync(other);

        var summaries = await store.QueryAsync(new PullQuery { TerritoryId = 321, Limit = 1 });

        Assert.Equal(newer.Id, Assert.Single(summaries).Id);
    }

    [Fact]
    public void CanonicalPersistenceSchemaNumbersAreDistinctFromLegacyM0Schemas()
    {
        Assert.Equal(1, CanonicalPullSerializer.CurrentFileSchemaVersion);
        Assert.Equal(1, CanonicalPullSerializer.CurrentPullSchemaVersion);
        Assert.Equal(1, FileCanonicalPullStore.CurrentIndexSchemaVersion);
        Assert.NotEqual(3, CanonicalPullSerializer.CurrentFileSchemaVersion);
        Assert.NotEqual(7, FileCanonicalPullStore.CurrentIndexSchemaVersion);
    }

    private static RecordedPull CreatePull(string id, uint territoryId, DateTimeOffset? startedAt)
    {
        var player = new ActorRecord
        {
            Id = new ActorId(1),
            Name = "Player",
            Kind = ActorKind.Player,
            ClassJobId = 38,
            JobAbbreviation = "DNC",
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
            Metadata = CreateMetadata(territoryId, TimeSpan.FromSeconds(12), startedAt),
            SchemaVersion = new PullSchemaVersion(CanonicalPullSerializer.CurrentPullSchemaVersion),
            Actors = [player, enemy],
            Events =
            [
                new DamageEvent
                {
                    Id = new EventId(1),
                    Sequence = 1,
                    PullTime = TimeSpan.FromSeconds(1),
                    ObservedAt = startedAt?.AddSeconds(1),
                    SourceActorId = enemy.Id,
                    TargetActorId = player.Id,
                    Provenance = EventProvenance(),
                    Amount = 12345,
                    ActionId = 100,
                },
            ],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = "test-live-pull",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
        };
    }

    private static PullMetadata CreateMetadata(
        uint territoryId,
        TimeSpan duration,
        DateTimeOffset? startedAt)
    {
        return new PullMetadata
        {
            TerritoryId = territoryId,
            TerritoryName = $"Duty {territoryId}",
            Duration = duration,
            StartedAt = startedAt,
        };
    }

    private static EventProvenance EventProvenance()
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "test-live-pull",
            Fidelity = CaptureFidelity.Exact,
        };
    }

    private static string GetDetailPath(string root, PullId id)
    {
        return System.IO.Path.Combine(root, "canonical-pull-details", $"{id.Value:N}.json");
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
